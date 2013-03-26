using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace AutoSendPic.Model
{
    public class DataManager:IDisposable
    {
        protected object lockPicStorages = new object();

        private Thread thread;

        /// <summary>
        ///     保存待ちキュー (スレッドセーフ）
        /// </summary>
        public ConcurrentQueue<PicData> PicDataQueue { get; private set; }

        /// <summary>
        ///     保存先のストレージ 
        /// </summary>
        public List<PicStorage> PicStorages { get; private set; }


        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public DataManager()
        {
            PicDataQueue = new ConcurrentQueue<PicData>(); //スレッドセーフ
            PicStorages = new List<PicStorage>(); //非スレッドセーフ
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
                    if (ex is ThreadAbortException)
                    {
                        throw;
                    }
                    else
                    {
                        OnError(ex);
                    }
                }
            }

            return true;
        }
		/// <summary>
		/// 	非同期アップロードを開始する
		/// </summary>
        public void Start()
        {

            if (thread != null && thread.ThreadState == ThreadState.Running)
            {
                return; 
            }

            thread = new Thread(() =>
            {
                for (; ; )
                {
                    Thread.Sleep(100);
                    
                    //１件処理する
                    PicData pd;
                    if (PicDataQueue.TryDequeue(out pd))
                    {
                        Save(pd);
                    }
                    
                }

            });
            thread.Start();

        }

        public void Stop()
        {
            if (thread != null)
            {
                try
                {
                    thread.Abort();
                }
                catch { }
            }
        }


        public void OnError(Exception ex)
        {
            if (Error != null)
            {
                Error(this, new ExceptionEventArgs( ex));   
            }
        }

        public event EventHandler<ExceptionEventArgs> Error;


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
