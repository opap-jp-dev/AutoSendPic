using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using Android.OS.Storage;
using Android.Media;
using Android.Locations;

using AutoSendPic.Model;

namespace AutoSendPic
{
    [Activity(Label = "AutoSendPic", MainLauncher = true, Icon = "@drawable/icon",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard
            | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout | ConfigChanges.UiMode,
        ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : Activity, ISurfaceHolderCallback
    {
        /// <summary>
        /// カメラのプレビューを表示する所
        /// </summary>
        SurfaceView cameraSurfaceView;

        private PowerManager.WakeLock wakeLock = null;

        /// <summary>
        /// 送信中かどうかを格納するフラグ
        /// </summary>
        private volatile bool enableSend = false;


        /// <summary>
        /// カメラへの参照
        /// </summary>
        private CameraManager cameraManager;

        /// <summary>
        /// 位置情報への参照
        /// </summary>
        private LocationTracker locTrackerGPS;

        /// <summary>
        /// 位置情報への参照
        /// </summary>
        private LocationTracker locTrackerNetwork;

        /// <summary>
        /// 送信用オブジェクト
        /// </summary>
        private volatile SendJobManager sendJobManager;

        /// <summary>
        /// カメラ操作同期用オブジェクト
        /// </summary>
        private volatile object timerSyncObj = new object();

        /// <summary>
        /// 汎用同期用オブジェクト
        /// </summary>
        private volatile object syncObj = new object();
        /// <summary>
        /// タイマー
        /// </summary>
        private volatile Timer mainTimer;

        /// <summary>
        /// 設定
        /// </summary>
        private volatile Settings settings;

        /// <summary>
        /// カメラ表示の初期の幅
        /// </summary>
        private volatile int originalWidth;

        /// <summary>
        /// カメラ表示の初期の高さ
        /// </summary>
        private volatile int originalHeight;

        /// <summary>
        /// 成功カウント
        /// </summary>
        private volatile int okCountHttp = 0;
        /// <summary>
        /// 成功カウント
        /// </summary>
        private volatile int okCountLocal = 0;

        /// <summary>
        /// 合計カウント
        /// </summary>
        private volatile int totalCountHttp = 0;
        /// <summary>
        /// 合計カウント
        /// </summary>
        private volatile int totalCountLocal = 0;

        /// <summary>
        /// 最後に測位した位置
        /// </summary>
        private LocationData lastLocation;

        /// <summary>
        /// 
        /// </summary>
        private volatile Handler handler = new Handler();

        #region Activityイベント
        /// <summary>
        /// 起動時
        /// </summary>
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // 画面の向きを固定する
            RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;

            // 各種フラグ設定
            this.Window.AddFlags(WindowManagerFlags.KeepScreenOn);

            //ウェイクロック
            using (PowerManager pm = (PowerManager)GetSystemService(Service.PowerService))
            {
                wakeLock = pm.NewWakeLock(WakeLockFlags.ScreenDim, PackageName);
                wakeLock.Acquire();
            }

            //設定読み込み
            settings = Settings.Load(this);

            //カメラを開く
            cameraManager = new CameraManager();
            cameraManager.PictureTaken += CameraManager_PictureTaken;
            cameraManager.Open();

            // カメラ表示設定
            cameraSurfaceView = FindViewById<SurfaceView>(Resource.Id.surfaceView1);
            cameraSurfaceView.Holder.AddCallback(this);
            cameraSurfaceView.Holder.SetType(SurfaceType.PushBuffers);

            //位置情報を開く
            locTrackerGPS = new LocationTracker(this, LocationManager.GpsProvider);
            locTrackerGPS.Start();
            locTrackerNetwork = new LocationTracker(this, LocationManager.NetworkProvider);
            locTrackerNetwork.Start();

            // 各種イベントを登録する
            SetupEvents();

        }

        public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);

        }

        void CameraManager_PictureTaken(object sender, DataEventArgs e)
        {
            try
            {
                //2つの位置情報を取得する
                LocationData gps = locTrackerGPS.LastLocation;
                LocationData net = locTrackerNetwork.LastLocation;

                //どちらの位置情報を使うか決定する

                LocationData _lastLocation = gps;//基本はGPS使用
                if (gps.Time.AddMinutes(1) < net.Time)
                {
                    _lastLocation = net;//NetがGPSよりも1分以上新しい→Net
                }
                if (gps.Accuracy - net.Accuracy > 30)
                {
                    _lastLocation = net;//Netの方がGPSよりも30m以上精度が高い→Net
                }

                this.lastLocation = _lastLocation;
                e.Data.Location = _lastLocation;


                /*** 送信ジョブを作成 ***/

                const string FileNameFormat = "pic_{0:yyyy-MM-dd-HH-mm-ss}.jpg";
                DateTime expire = DateTime.Now.AddSeconds(settings.MinInterval * 3); //3倍の時間が掛かったら期限切れにする

                //ローカル
                sendJobManager.JobQueue.Enqueue(new LocalSendJob(e.Data, expire)
                {
                    OutputDir = settings.OutputDir,
                    FileNameFormat = FileNameFormat
                });
                totalCountLocal++;

                //HTTP
                if (settings.UseHttp)
                {
                    sendJobManager.JobQueue.Enqueue(new HttpSendJob(e.Data, expire)
                    {
                        Url = settings.HttpUrl,
                        Credentials = new System.Net.NetworkCredential(settings.HttpUser, settings.HttpPass),
                        FileNameFormat = FileNameFormat,
                        Timeout = 5 + (uint)settings.MinInterval * 2
                    });
                    totalCountHttp++;
                }




                //画面に位置情報を出す
                handler.Post(() =>
                {
                    try
                    {
                        RefreshUI();
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                showError(ex);
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            base.OnCreateOptionsMenu(menu);

            // メニューの作成
            IMenuItem menuItem1 = menu.Add(0, 0, 0, "設定");

            //アイコンの追加
            menuItem1.SetIcon(Android.Resource.Drawable.IcMenuPreferences);

            //アクションバーに表示            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            {
                menuItem1.SetShowAsAction(ShowAsAction.Always);
            }
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case 0:
                    StopSend();
                    Intent intent = new Intent(this, typeof(SettingsActivity));
                    StartActivity(intent);
                    break;
                default:
                    break;
            }

            return true;

        }

        protected override void OnPause()
        {
            base.OnPause();
            try
            {
                StopSend();
                ApplySendStatus();
                cameraManager.Close();
            }
            catch (Exception e)
            {
                showError(e);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            try
            {
                cameraManager.Open();

            }
            catch (Exception e)
            {
                showError(e);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            try
            {
                StopTimer();
                StopDataManager();
                if (cameraManager != null)
                {
                    cameraManager.Error -= HandleError;
                    cameraManager.PictureTaken -= CameraManager_PictureTaken;
                    cameraManager.Close();
                    cameraManager.Dispose();
                    cameraManager = null;
                }

                if (wakeLock != null)
                {
                    wakeLock.Release();
                    wakeLock.Dispose();
                    wakeLock = null;
                }

                //if (locTrackerGPS != null)
                //{
                //    locTrackerGPS.Dispose();
                //}

                //if (locTrackerNetwork != null)
                //{
                //    locTrackerNetwork.Dispose();
                //}
            }
            catch (Exception e)
            {
                showError(e);
            }
        }
        #endregion

        /// <summary>
        /// 各種イベントの登録
        /// </summary>
        void SetupEvents()
        {

            //カメラ関連
            cameraManager.Error += HandleError;


            //ボタン設定
            ToggleButton btnSendStart = FindViewById<ToggleButton>(Resource.Id.btnSendStart);
            btnSendStart.Click += delegate
            {
                enableSend = btnSendStart.Checked;
                ApplySendStatus();
            };


            Button btnExit = FindViewById<Button>(Resource.Id.btnExit);
            btnExit.Click += delegate
            {
                this.Finish();
                System.Environment.Exit(0);
            };

            Button btnFocus = FindViewById<Button>(Resource.Id.btnFocus);
            btnFocus.Click += delegate
            {
                try
                {
                    cameraManager.AutoFocus();
                }
                catch (Exception ex)
                {
                    showError(ex);
                }
            };

            ToggleButton btnToggleFlash = FindViewById<ToggleButton>(Resource.Id.btnToggleFlash);
            btnToggleFlash.Click += delegate
            {
                cameraManager.EnableFlash = btnToggleFlash.Checked;
                cameraManager.ApplyFlashStatus();
            };
        }

        /// <summary>
        /// 撮影タイマーを開始する
        /// </summary>
        void StartTimer()
        {
            StopTimer();
            lock (timerSyncObj)
            {
                mainTimer = new Timer(new TimerCallback(mainTimerCallback), null, 0, settings.MinInterval * 1000);
            }
        }

        /// <summary>
        /// 撮影タイマーを停止する
        /// </summary>
        void StopTimer()
        {
            lock (timerSyncObj)
            {
                if (mainTimer != null)
                {
                    mainTimer.Dispose();
                    mainTimer = null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void StartDataManager()
        {
            StopDataManager();
            lock (syncObj)
            {
                this.sendJobManager = new SendJobManager();
                //イベントの設定
                sendJobManager.Error += HandleError;
                sendJobManager.Success += HandleSuccess;
                //開始
                okCountHttp = 0;
                okCountLocal = 0;
                totalCountHttp = 0;
                totalCountLocal = 0;

                sendJobManager.Start();
            }
        }

        void StopDataManager()
        {
            lock (syncObj)
            {
                if (sendJobManager != null)
                {
                    sendJobManager.Error -= HandleError;
                    sendJobManager.Success -= HandleSuccess;
                    sendJobManager.Stop();
                    sendJobManager.Dispose();
                    sendJobManager = null;
                }
            }
        }

        void HandleError(object sender, ExceptionEventArgs e)
        {
            showError(e.Exception);
            RefreshUI();
        }

        void HandleSuccess(object sender, JobEventArgs e)
        {
            if (e.Job is HttpSendJob)
            {
                okCountHttp++;
            }
            else if (e.Job is LocalSendJob)
            {
                okCountLocal++;
            }
            RefreshUI();
        }


        void mainTimerCallback(object o)
        {
            try
            {

                lock (syncObj)
                {
                    //送信が有効かどうかチェック
                    if (enableSend)
                    {
                        cameraManager.RequestTakePicture();

                    }
                }


            }
            catch (Exception e)
            {
                showError(e);
            }
        }


        void showError(Exception ex)
        {

            try
            {

                StringBuilder sb = new StringBuilder();
                if (ex == null)
                {
                    sb.Append("NULL Error");
                }
                else
                {
                    sb.AppendFormat(
                        "{0}: {1}",
                        ex.GetType().Name,
                        ex.Message
                    );
                }

                handler.Post(() =>
                {
                    try
                    {
                        Toast toast = Toast.MakeText(BaseContext, sb.ToString(), Android.Widget.ToastLength.Short);
                        toast.Show();

                        if (settings.BeepOnError)
                        {

                            using (ToneGenerator toneGenerator = new ToneGenerator(
                                    Android.Media.Stream.System,
                                    Android.Media.Volume.Max
                                    ))
                            {
                                toneGenerator.StartTone(Android.Media.Tone.PropBeep);
                            }
                        }
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        public void ApplySendStatus()
        {
            if (enableSend)
            {
                StartSend();
            }
            else
            {
                StopSend();
            }
            handler.Post(() =>
            {
                try
                {
                    ToggleButton btnSendStart = FindViewById<ToggleButton>(Resource.Id.btnSendStart);
                    btnSendStart.Checked = enableSend;
                }
                catch { }
            });
        }

        public void StartSend()
        {
            StopSend();

            //設定を再ロード
            settings = Settings.Load(this);
            ApplyCammeraSettings();

            //スタート
            StartDataManager();
            StartTimer();
            enableSend = true;
        }

        public void StopSend()
        {
            //ストップ
            StopTimer();
            StopDataManager();
            enableSend = false;
        }

        public void ApplyCammeraSettings(bool startPreview = true)
        {

            if (originalHeight * originalWidth == 0)
            {
                return; //読み込まれていなかった場合は設定しない
            }


            cameraManager.Settings = this.settings;
            cameraManager.ApplySettings();



            Camera.Size sz = cameraManager.PictureSize;



            //レイアウト設定の編集
            handler.Post(() =>
            {
                try
                {
                    ViewGroup.LayoutParams lp = cameraSurfaceView.LayoutParameters;


                    double ch = sz.Height;
                    double cw = sz.Width;

                    double w_scale = (double)originalWidth / sz.Width;
                    double h_scale = (double)originalHeight / sz.Height ;
                   
                    double scale = Math.Min(w_scale, h_scale);
                    lp.Width = (int)(sz.Width * scale);
                    lp.Height = (int)(sz.Height * scale);
                    
                    cameraSurfaceView.LayoutParameters = lp;
                    cameraSurfaceView.Holder.SetFixedSize(lp.Width, lp.Height);

                    //プレビュー開始
                    cameraManager.StartPreview();

                }
                catch (Exception ex)
                {
                    showError(ex);
                }
            });
        }

        public void RefreshUI()
        {

            handler.Post(() =>
            {
                try
                {
                    TextView tvLocation = FindViewById<TextView>(Resource.Id.tvLocation);
                    tvLocation.Text = string.Format("Loc: {0}, {1} ({2})",
                                                    lastLocation.Latitude,
                                                    lastLocation.Longitude,
                                                    lastLocation.Provider);

                    TextView tvOKCount = FindViewById<TextView>(Resource.Id.tvOKCount);
                    tvOKCount.Text = string.Format("OK Count: [Local] {0}/{1} [HTTP] {2}/{3}",
                            okCountLocal,
                            totalCountLocal,
                            okCountHttp,
                            totalCountHttp);
                }
                catch { }
            });
        }



        #region ISurfaceHolderCallback メンバー

        public void SurfaceChanged(ISurfaceHolder holder, Android.Graphics.Format format, int width, int height)
        {

            try
            {
                holder.SetType(SurfaceType.PushBuffers);
                

                // 縦横が入れ替わっている場合の対処
                if (width < height)
                {
                    var t = width;
                    width = height;
                    height = t;
                }

                if (originalHeight * originalWidth == 0)
                {
                    originalWidth = width;
                    originalHeight = height;
                }


                // カメラ設定の編集
                ApplyCammeraSettings();


            }
            catch (System.IO.IOException)
            {
            }
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            if (!cameraManager.IsOpened)
            {
                cameraManager.Open();
            }
            cameraManager.SetPreviewDisplay(cameraSurfaceView.Holder);

        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            if (cameraManager != null)
            {
                cameraManager.StopPreview();
                cameraManager.SetPreviewDisplay(null);
            }
        }

        #endregion

    }
}

