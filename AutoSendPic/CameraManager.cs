using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;

using AutoSendPic.Model;

namespace AutoSendPic
{
    /// <summary>
    /// カメラの制御等を行うクラス
    /// </summary>
    public class CameraManager : Java.Lang.Object, Camera.IPictureCallback, Camera.IPreviewCallback, IDisposable
    {



        #region 変数

        /// <summary>
        /// 同期用オブジェクト
        /// </summary>
        private object syncObj = new object();

        /// <summary>
        /// フラッシュの発光状態
        /// </summary>
        private volatile bool enableFlash = false;

        /// <summary>
        /// 現在のフレームを撮影するかどうかの制御フラグ
        /// </summary>
        private volatile bool flgReqTakePic = false;

        #endregion

        #region プロパティ

        /// <summary>
        /// カメラ
        /// </summary>
        private Camera MainCamera { get; set; }

        /// <summary>
        /// プレビュー出力先
        /// </summary>
        private SurfaceView PreviewSurface { get; set; }

        /// <summary>
        /// 設定内容を取得または設定します
        /// </summary>
        public Settings Settings { get; set; }

        /// <summary>
        /// カメラがオープンされているかどうかを取得します。
        /// </summary>
        public bool IsOpened 
        { 
            get { return MainCamera != null; } 
        }

        /// <summary>
        /// カメラに設定されている画像サイズを取得します
        /// </summary>
        public Camera.Size PictureSize
        {
            get { return MainCamera.GetParameters().PreviewSize; }
        }

        /// <summary>
        /// フラッシュの点灯状態を設定します。
        /// </summary>
        /// <remarks>
        /// 設定を反映するためには、ApplyFlashStatusメソッドか、ApplySettingsメソッドを呼び出して下さい。
        /// </remarks>
        public bool EnableFlash
        {
            get { return enableFlash; }
            set { enableFlash = value;  }
        }



        #endregion

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CameraManager()
        {

        }


        /// <summary>
        /// カメラをオープンします
        /// </summary>
        public void Open()
        {
            lock (syncObj)
            {
                if (MainCamera != null)
                {
                    return;
                }

                MainCamera = Camera.Open();
                
            }
        }
        
        /// <summary>
        /// カメラをクローズします
        /// </summary>
        public void Close()
        {
            lock (syncObj)
            {
                if (MainCamera == null)
                {
                    return;
                }

                //この順序でクリーンアップしないとエラーが起きることがある
                MainCamera.StopPreview();
                MainCamera.SetPreviewCallback(null);
                MainCamera.Release();
                MainCamera.Dispose();
                MainCamera = null;
                
            }
        }

        /// <summary>
        /// プレビュー表示先を設定します
        /// </summary>
        /// <param name="holder">プレビュー表示先</param>
        public void SetPreviewDisplay(ISurfaceHolder holder)
        {
            lock (syncObj)
            {
                MainCamera.SetPreviewDisplay(holder);
            }
        }

        /// <summary>
        /// プレビューを開始します
        /// </summary>
        public void StartPreview()
        {
            lock (syncObj)
            {
                if (!IsOpened)
                {
                    return;
                }
                MainCamera.SetPreviewCallback(this);
                MainCamera.StartPreview();
            }
        }

        /// <summary>
        /// プレビューを終了します
        /// </summary>
        public void StopPreview()
        {
            lock (syncObj)
            {
                if (!IsOpened)
                {
                    return;
                }
                MainCamera.SetPreviewCallback(null);
                MainCamera.StopPreview();

            }
        }

        /// <summary>
        /// 設定を適用します
        /// </summary>
        public void ApplySettings()
        {

            Camera.Size sz;

            if (!IsOpened)
            {
                return;
            }

            Camera.Parameters parameters = MainCamera.GetParameters();
            sz = GetOptimalSize(parameters.SupportedPreviewSizes, Settings.Width, Settings.Height); //最適なサイズを取得
            parameters.SetPreviewSize(sz.Width, sz.Height);
            //sz2 = GetOptimalSize(parameters.SupportedPictureSizes, settings.Width, settings.Height);
            //parameters.SetPictureSize(sz2.Width, sz2.Height);
            //parameters.JpegQuality = 70;

            SetFlashStatusToParam(parameters);

            lock (syncObj)
            {
                if (IsOpened)
                {
                    MainCamera.SetParameters(parameters);
                }
            }

        }

        /// <summary>
        /// 撮影をリクエストします
        /// </summary>
        public void RequestTakePicture()
        {
            lock (syncObj)
            {
                flgReqTakePic = true;
            }
        }

        public void AutoFocus()
        {
            lock (syncObj)
            {
                if (IsOpened)
                {
                    MainCamera.AutoFocus(null);
                }
            }
        }

        /// <summary>
        /// フラッシュの状態を適用します
        /// </summary>
        public void ApplyFlashStatus()
        {
            Camera.Parameters parameters = MainCamera.GetParameters();
            SetFlashStatusToParam(parameters);
            lock (syncObj)
            {
                if (IsOpened)
                {
                    MainCamera.SetParameters(parameters);
                }
            }
        }

        /// <summary>
        /// 指定したパラメータオブジェクトにフラッシュの点灯状態を反映させます
        /// </summary>
        /// <param name="parameters">パラメータオブジェクト</param>
        private void SetFlashStatusToParam(Camera.Parameters parameters)
        {
            var modes = parameters.SupportedFlashModes;

            if (enableFlash)
            {
                if (modes.Contains(Camera.Parameters.FlashModeTorch))
                {
                    parameters.FlashMode = Camera.Parameters.FlashModeTorch;
                }
                else if (modes.Contains(Camera.Parameters.FlashModeOn))
                {
                    parameters.FlashMode = Camera.Parameters.FlashModeOn;
                }
            }
            else
            {
                if (modes.Contains(Camera.Parameters.FlashModeOff))
                {
                    parameters.FlashMode = Camera.Parameters.FlashModeOff;
                }
            }

        }

        /// <summary>
        /// リストから指定したサイズに最も近いサイズの要素を取得します
        /// </summary>
        /// <param name="sizes">リスト</param>
        /// <param name="w">幅</param>
        /// <param name="h">高さ</param>
        /// <returns>リストの要素のうちもっとも指定されたサイズに近いもの</returns>
        private Camera.Size GetOptimalSize(IList<Camera.Size> sizes, int w, int h)
        {
            double targetRatio = (double)w / h;
            if (sizes == null)
                return null;

            Camera.Size optimalSize = null;

            int targetHeight = h;


            var sorted_sizes =
                sizes.OrderBy((x) => Math.Abs((double)x.Width / x.Height - targetRatio))
                     .ThenBy((x) => Math.Abs(x.Height - targetHeight));

            optimalSize = sorted_sizes.FirstOrDefault(); //一番差が小さいやつ
            return optimalSize;
        }

        #region イベント

        /// <summary>
        /// 撮影されたときに発生します
        /// </summary>
        public event EventHandler<DataEventArgs> PictureTaken;

        /// <summary>
        /// 撮影イベントを発生させる
        /// </summary>
        /// <param name="pd"></param>
        private void OnPictureTaken(PicData pd)
        {
            if (PictureTaken != null)
            {
                PictureTaken(this, new DataEventArgs(pd));
            }
        }


        /// <summary>
        /// エラーイベントを発生させる
        /// </summary>
        /// <param name="ex"></param>
        public void OnError(Exception ex)
        {
            if (Error != null)
            {
                Error(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// エラー発生時に発生する
        /// </summary>
        public event EventHandler<ExceptionEventArgs> Error;

        #endregion

        
        
        #region IPictureCallback メンバー

        public void OnPictureTaken(byte[] data, Camera camera)
        {
            //未実装
        }

        #endregion

        #region IPreviewCallback メンバー

        public  void OnPreviewFrame(byte[] data, Camera camera)
        {

            lock (syncObj)
            {
                if (flgReqTakePic)
                {
                    flgReqTakePic = false;
                }
                else
                {
                    return;
                }

            }

            try
            {

                //JPEG圧縮を行うため、別スレッドで処理を行う
                Task.Run(() =>
                {
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

                            ms.Close(); // Closeしてからでないと、ToArrayは正常に取得できない

                            byte[] jpegData = ms.ToArray();
                            OnPictureTaken(new PicData(jpegData, DateTime.Now, new LocationData()));

                        }


                    }
                });
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        #endregion



        

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                Close();
            }
            catch { }
        }

    }
}
