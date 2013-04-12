using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading;


using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using Android.OS.Storage;
using Android.Media;

using AutoSendPic.Model;

namespace AutoSendPic
{
    [Activity(Label = "AutoSendPic", MainLauncher = true, Icon = "@drawable/icon")]
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
        private LocationTracker locTracker;

        /// <summary>
        /// 送信用オブジェクト
        /// </summary>
        private volatile DataManager dataManager;

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
        private volatile int okCount = 0;
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

            //位置情報を開く
            locTracker = new LocationTracker(this);
            locTracker.Start();

            // 各種イベントを登録する
            SetupEvents();

        }

        void CameraManager_PictureTaken(object sender, DataEventArgs e)
        {
            try
            {
                lock (syncObj)
                {
                    e.Data.Location = locTracker.LastLocation;
                    this.dataManager.PicDataQueue.Enqueue(e.Data);
                }
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
            menuItem1.SetShowAsAction(ShowAsAction.Always);
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
                // 送信モジュールを初期化
                const string FileNameFormat = "pic_{0:yyyy-MM-dd-HH-mm-ss}.jpg";
                this.dataManager = new DataManager();
                //ストレージモジュールの設定
                dataManager.PicStorages.Add(new LocalFilePicStorage() { OutputDir = settings.OutputDir, FileNameFormat = FileNameFormat });
                if (settings.UseHttp)
                {
                    dataManager.PicStorages.Add(new HttpPicStorage()
                    {
                        Url = settings.HttpUrl,
                        Credentials = new System.Net.NetworkCredential(settings.HttpUser, settings.HttpPass),
                        FileNameFormat = FileNameFormat
                    });
                }
                //イベントの設定
                dataManager.Error += HandleError;
                dataManager.Success += HandleSuccess;
                //開始
                okCount = 0;
                dataManager.Start();
            }
        }

        void StopDataManager()
        {
            lock (syncObj)
            {
                if (dataManager != null)
                {
                    dataManager.Error -= HandleError;
                    dataManager.Success -= HandleSuccess;
                    dataManager.Stop();
                    dataManager.Dispose();
                    dataManager = null;
                }
            }
        }

        void HandleError(object sender, ExceptionEventArgs e)
        {
            showError(e.Exception);
        }

        void HandleSuccess(object sender, EventArgs e)
        {
            okCount++;
            handler.Post(() =>
            {
                TextView tvOKCount = FindViewById<TextView>(Resource.Id.tvOKCount);
                tvOKCount.Text = string.Format("OK Count: {0}", okCount);
            });
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
                ToggleButton btnSendStart = FindViewById<ToggleButton>(Resource.Id.btnSendStart);
                btnSendStart.Checked = enableSend;
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

        public void ApplyCammeraSettings()
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
                ViewGroup.LayoutParams lp = cameraSurfaceView.LayoutParameters;
                int ch = sz.Height;
                int cw = sz.Width;
                if ((double)ch / cw > (double)originalWidth / originalHeight)
                {
                    lp.Width = originalWidth;
                    lp.Height = originalWidth * ch / cw;
                }
                else
                {
                    lp.Width = originalHeight * cw / ch;
                    lp.Height = originalHeight;
                }
                cameraSurfaceView.LayoutParameters = lp;
            });
        }



        #region ISurfaceHolderCallback メンバー

        public void SurfaceChanged(ISurfaceHolder holder, Android.Graphics.Format format, int width, int height)
        {

            try
            {
                // 縦横が入れ替わっている場合の対処
                if (width < height)
                {
                    var t = width;
                    width = height;
                    height = t;
                }

                originalWidth = width;
                originalHeight = height;

                // カメラ設定の編集
                ApplyCammeraSettings();


                // 画面プレビュー開始
                cameraManager.SetPreviewDisplay(cameraSurfaceView.Holder);
                cameraManager.StartPreview();

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
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {

        }

        #endregion

    }
}

