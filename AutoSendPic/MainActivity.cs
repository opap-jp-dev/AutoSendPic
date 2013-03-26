using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using Android.OS.Storage;


namespace AutoSendPic
{
    [Activity(Label = "AutoSendPic", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity, ISurfaceHolderCallback, Camera.IPreviewCallback
    {
        SurfaceView cameraSurfaceView;
        DateTime lastSendStartTime = DateTime.MinValue;
        bool enableSend = false;
        private Camera camera;



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

            //ボタン設定

            Button btnSendStart = FindViewById<Button>(Resource.Id.btnSendStart);
            btnSendStart.Click += delegate
            {
                enableSend = !enableSend;
                btnSendStart.Text = enableSend ? "送信停止" : "送信開始";
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



        #region ISurfaceHolderCallback メンバー

        public void SurfaceChanged(ISurfaceHolder holder, Android.Graphics.Format format, int width, int height)
        {

            try
            {
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
            const double ASPECT_TOLERANCE = 0.05;
            double targetRatio = (double)w / h;
            if (sizes == null) return null;

            Camera.Size optimalSize = null;
            double minDiff = Double.MaxValue;

            int targetHeight = h;

            // Try to find an size match aspect ratio and size
            foreach (Camera.Size size in sizes)
            {
                double ratio = (double)size.Width / size.Height;
                if (Math.Abs(ratio - targetRatio) > ASPECT_TOLERANCE) continue;
                if (Math.Abs(size.Height - targetHeight) < minDiff)
                {
                    optimalSize = size;
                    minDiff = Math.Abs(size.Height - targetHeight);
                }
            }

            // Cannot find the one match the aspect ratio, ignore the requirement
            if (optimalSize == null)
            {
                minDiff = Double.MaxValue;
                foreach (Camera.Size size in sizes)
                {
                    if (Math.Abs(size.Height - targetHeight) < minDiff)
                    {
                        optimalSize = size;
                        minDiff = Math.Abs(size.Height - targetHeight);
                    }
                }
            }
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
                        ms.ToArray();
                    }

                    

                    //サーバーに送信する（TODO: 以下は未実装）
                    Encoding enc = System.Text.Encoding.GetEncoding("utf-8");
                    WebClient client = new WebClient();
                    byte[] bytes = client.UploadFile("http://dev.opap.jp/imgup/up.php", tmpfile);

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

