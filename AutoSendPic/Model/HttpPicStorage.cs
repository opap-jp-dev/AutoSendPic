using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Net;

namespace AutoSendPic.Model
{
    public class HttpPicStorage : PicStorage
    {
        /// <summary>
        /// 送信先URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 認証情報
        /// </summary>
        public ICredentials Credentials
        {
            get;
            set;
        }

        /// <summary>
        /// タイムアウトの秒数（デフォルト: 20）
        /// </summary>
        public uint Timeout { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public HttpPicStorage()
        {
            //デフォルト値の設定
            Credentials = CredentialCache.DefaultNetworkCredentials;
            Timeout = 20; 
        }

        public override async Task<bool> Save(PicData dataToSave)
        {
            //保存処理
            bool sendOK = await SendHttp(dataToSave).Timeout(TimeSpan.FromSeconds(Timeout));
            return sendOK;

        }

        /// <summary>
        /// データをHTTPで送信する
        /// </summary>
        /// <param name="dataToSave"></param>
        /// <returns></returns>
        private async Task<bool> SendHttp(PicData dataToSave)
        {
            return await Task<bool>.Run(() =>
            {
                //位置情報等
                Dictionary<string, string> formValues = new Dictionary<string, string>();

                formValues["Accuracy"] = dataToSave.Location.Accuracy.ToString();
                formValues["Altitude"] = dataToSave.Location.Altitude.ToString();
                formValues["Latitude"] = dataToSave.Location.Latitude.ToString();
                formValues["Longitude"] = dataToSave.Location.Longitude.ToString();
                formValues["Provider"] = dataToSave.Location.Provider;
                formValues["Speed"] = dataToSave.Location.Speed.ToString();
                formValues["Time"] = dataToSave.Location.Time.ToString();


                //画像データを添付
                using (MemoryStream ms = new MemoryStream(dataToSave.Data))
                {

                    bool sendOK = HttpUploadFile(this.Url,
                                                 this.Credentials,
                                                 MakeFileName(dataToSave.TimeStamp),
                                                 ms,
                                                 "file",
                                                 "image/jpeg",
                                                 formValues);

                    return sendOK;
                }
            });
        }

        /// <summary>
        /// HTTPでファイルをアップロードする。
        /// </summary>
        /// <remarks>
        /// This code is from http://stackoverflow.com/questions/566462/upload-files-with-httpwebrequest-multipart-form-data/2996904#2996904
        /// </remarks>
        /// <param name="url">送信先URL</param>
        /// <param name="credentials">認証情報</param>
        /// <param name="filename">ファイル名</param>
        /// <param name="data">ファイルデータ（0バイト目にシークした状態で渡して下さい）</param>
        /// <param name="paramName">ファイルを添付するパラメータ名</param>
        /// <param name="contentType">ファイルのContent-Type</param>
        /// <param name="formValues">ファイル以外のフォームの値</param>
        /// <returns>
        ///     true: レスポンスが「200」だった場合
        ///     false: レスポンスが「200」以外だった場合
        /// </returns>
        public static bool HttpUploadFile(string url, ICredentials credentials, string filename, Stream data,
                                          string paramName, string contentType, Dictionary<string, string> formValues)
        {

            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = credentials;
            
            using (Stream rs = wr.GetRequestStream())
            {
                // ファイル以外の値を送信
                string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
                foreach (string key in formValues.Keys)
                {
                    rs.Write(boundarybytes, 0, boundarybytes.Length);
                    string formitem = string.Format(formdataTemplate, key, formValues[key]);
                    byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                    rs.Write(formitembytes, 0, formitembytes.Length);
                }
                rs.Write(boundarybytes, 0, boundarybytes.Length);

                // ファイルを送信
                string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                string header = string.Format(headerTemplate, paramName, filename, contentType);
                byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
                rs.Write(headerbytes, 0, headerbytes.Length);

                byte[] buffer = new byte[4096];
                int bytesRead = 0;
                while ((bytesRead = data.Read(buffer, 0, buffer.Length)) != 0)
                {
                    rs.Write(buffer, 0, bytesRead);
                }

                byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                rs.Write(trailer, 0, trailer.Length);
                rs.Close();
            }

            //レスポンスの取得
            using (HttpWebResponse wresp = (HttpWebResponse)wr.GetResponse())
            {
                if (wresp.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }
            }

            return true;
        }

    }
}
