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
    public class SendJobManager : IDisposable
    {
        protected object lockPicStorages = new object();

        private Thread thread;

        /// <summary>
        ///     保存待ちキュー (スレッドセーフ）
        /// </summary>
        public ConcurrentQueue<DataSendJob> JobQueue { get; private set; }


        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public SendJobManager()
        {
            JobQueue = new ConcurrentQueue<DataSendJob>(); //スレッドセーフ
        }

        ~SendJobManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// データを保存します
        /// </summary>
        /// <param name="dataToSave"></param>
        /// <returns></returns>
        public void RunAJob(DataSendJob job)
        {
            bool flgOK = false;
            try
            {
                flgOK = job.Run();
                if (flgOK)
                {
                    OnSuccess(job);
                }
                else
                {
                    OnError(new Exception("送信に失敗しました（原因不明）"), job);
                }
                return;
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, job);
            }
            finally
            {
                if (job is IDisposable)
                {
                    ((IDisposable)job).Dispose();
                }
            }
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
                    DataSendJob job;
                    if (!JobQueue.TryDequeue(out job))
                    {
                        continue;
                    }
                                        
                    // 古くなりすぎたジョブは実行しない
                    if(job.Expire < DateTime.Now)
                    {
                        if (job is IDisposable)
                        {
                            ((IDisposable)job).Dispose();
                        }
                        continue;
                    }
                    
                    //実行
                    TaskExt.Run(() =>
                    {
                        RunAJob(job);
                    });

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

        public void OnSuccess(DataSendJob job)
        {
            if (Success != null)
            {
                Success(this, new JobEventArgs(job));
            }
        }

        public event EventHandler<JobEventArgs> Success;

        public void OnError(Exception ex, DataSendJob job)
        {
            if (Error != null)
            {
                Error(this, new ExceptionEventArgs(ex, job));
            }
        }

        public event EventHandler<ExceptionEventArgs> Error;


        #region IDisposable メンバー

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                Stop();
            }
            catch { }
        }
    }
}
