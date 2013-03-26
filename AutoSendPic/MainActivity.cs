using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using Android.OS.Storage;
using AutoSendPic.Model;


namespace AutoSendPic
{
    [Activity(Label = "AutoSendPic", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity, ISurfaceHolderCallback, Camera.IPreviewCallback
    {
        SurfaceView cameraSurfaceView;
        DateTime lastSendStartTime = DateTime.MinValue;
        bool enableSend = false;
        private Camera camera;
        private DataManager dataManager;


        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // 画面の向きを固定する
            RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;

            // 各種フラグ設定
            this.Window.AddFlags(WindowManagerFlags.Fullscreen);
            this.Window.AddFlags(WindowManagerFlags.KeepScreenOn);


            // カメラ表示設定
            cameraSurfaceView = FindViewById<SurfaceView>(Resource.Id.surfaceView1);
            cameraSurfaceView.Holder.AddCallback(this);

            // 送信モジュールを初期化
            dataManager = new DataManager();
            dataManager.PicStorages.Add(new LocalFilePicStorage(){OutputDir =Android.OS.Environment.ExternalStorageDirectory + "/AutoSendPic/", FileNameFormat="pic_{0:yyyy-MM-dd-HH-mm-ss}.jpg"});
            dataManager.Error += dataManager_Error;

            //ボタン設定
            Button btnSendStart = FindViewById<Button>(Resource.Id.btnSendStart);
            btnSendStart.Click += delegate
            {
                enableSend = !enableSend;
                btnSendStart.Text = enableSend ? "送信停止" : "送信開始";
                if (enableSend)
                {
                    dataManager.Start();
                }
                else
                {
                    dataManager.Stop();
                }
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
                camera.AutoFocus(null);
            };

        }

        void dataManager_Error(object sender, ExceptionEventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (e == null || e.Exception == null)
                {
                    sb.Append("NULL Error");
                }
                else
                {
                    sb.AppendFormat(
                        "{0}: {1}",
                        e.Exception.GetType().Name,
                        e.Exception.Message
                        );
                }

                Toast toast = Toast.MakeText(BaseContext, sb.ToString(), Android.Widget.ToastLength.Short);
                toast.Show();
            }
            catch { }
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
                    Intent intent = new Intent(this, typeof(SettingsActivity));
                    StartActivity(intent);
                    break;
                default:
                    break;
            }

            return true;

        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (dataManager != null)
            {
                dataManager.Dispose();
                dataManager = null;
            }
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

                // カメラ設定の編集
                Camera.Parameters parameters = camera.GetParameters();
                Camera.Size sz = GetOptimalPreviewSize(parameters.SupportedPreviewSizes, width, height); //最適なサイズを取得
                parameters.SetPreviewSize(sz.Width, sz.Height);
                camera.SetParameters(parameters);


                //レイアウト設定の編集
                ViewGroup.LayoutParams lp = cameraSurfaceView.LayoutParameters;
                int ch = sz.Height;
                int cw = sz.Width;
                if (ch / cw > height / width)
                {
                    lp.Width = width;
                    lp.Height = width * ch / cw;
                }
                else
                {
                    lp.Width = height * cw / ch;
                    lp.Height = height;
                }
                cameraSurfaceView.LayoutParameters = lp;

                // 画面プレビュー開始
                camera.SetPreviewDisplay(holder);
                camera.SetPreviewCallback(this);
                camera.StartPreview();

            }
            catch (System.IO.IOException)
            { }
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            camera = Camera.Open();
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            camera.SetPreviewCallback(null);
            camera.StopPreview();
            camera.Release();
            camera = null;

        }
        private Camera.Size GetOptimalPreviewSize(IList<Camera.Size> sizes, int w, int h)
        {
            double targetRatio = (double)w / h;
            if (sizes == null) return null;

            Camera.Size optimalSize = null;

            int targetHeight = h;


            var sorted_sizes = 
                sizes.OrderBy((x) => Math.Abs((double)x.Width / x.Height - targetRatio))
                     .ThenBy((x) => Math.Abs(x.Height - targetHeight));

            optimalSize = sorted_sizes.FirstOrDefault(); //一番差が小さいやつ
            return optimalSize;
        }
        #endregion

        #region IPreviewCallback メンバー

        public void OnPreviewFrame(byte[] data, Camera camera)
        {

            string tmpfile = "/data/data/" + this.PackageName + "/tmp.jpg";

            try
            {
                //送信が有効かどうかチェック
                if (!enableSend)
                {
                    return;
                }

                //約1秒毎に実行する
                if (DateTime.Now - lastSendStartTime < new TimeSpan(0, 0, 0, 1)) { return; }
                lastSendStartTime = DateTime.Now;

                //データを読み取り
                Camera.Parameters parameters = camera.GetParameters();
                Camera.Size size = parameters.PreviewSize;
                using (Android.Graphics.YuvImage image = new Android.Graphics.YuvImage(data, parameters.PreviewFormat,
                        size.Width, size.Height, null))
                {


                    //データをJPGに変換してメモリに保持                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        image.CompressToJpeg(
                            new Android.Graphics.Rect(0, 0, image.Width, image.Height), 90,
                            ms);

                        ms.Close();
                        PicData pd = new PicData(ms.ToArray(), DateTime.Now); // Closeしてからでないと、ToArrayは正常に取得できない
                        dataManager.PicDataQueue.Enqueue(pd);
                    }
                    
                    
                }


            }
            catch (Exception e)
            {
                Toast toast = Toast.MakeText(BaseContext, e.Message, Android.Widget.ToastLength.Short);
                toast.Show();
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpfile))
                    {
                        File.Delete(tmpfile);
                    }
                }
                catch { }

            }

        }

        #endregion
    }
}

