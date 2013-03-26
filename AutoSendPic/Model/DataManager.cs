using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace AutoSendPic.Model
{
    public class DataManager:IDisposable
    {
        protected object lockPicDataQueue = new object();

        private CancellationTokenSource tokenSource;

        /// <summary>
        ///     保存待ちキュー
        /// </summary>
        public Queue<PicData> PicDataQueue { get; private set; }

        /// <summary>
        ///     保存先のストレージ
        /// </summary>
        public List<PicStorage> PicStorages { get; set; }

        protected object lockPicStorages = new object();

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public DataManager()
        {
            PicDataQueue = new Queue<PicData>();
            PicStorages = new List<PicStorage>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataToSave"></param>
        /// <returns></returns>
        public bool Save(PicData dataToSave)
        {

            foreach (var stor in PicStorages)
            {
                try
                {
                    stor.Save(dataToSave);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
            }

            return true;
        }

        public async void Start()
        {

            tokenSource = null;
            tokenSource = new CancellationTokenSource();

            CancellationToken token = tokenSource.Token;

            await Task.Run(() =>
            {
                for (; ; )
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(100);
                    
                    //１件処理する
                    PicData pd;
                    lock (lockPicDataQueue)
                    {
                        pd = PicDataQueue.Dequeue();
                    }

                    if (pd == null)
                    {
                        continue;
                    }

                    Save(pd);

                }
            });
        }

        public void Stop()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }


        public void OnError(Exception ex)
        {
            if (Error != null)
            {
                Error(this, ex);   
            }
        }

        public event EventHandler<Exception> Error;


        #region IDisposable メンバー

        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch { }
        }

        #endregion
    }
}
