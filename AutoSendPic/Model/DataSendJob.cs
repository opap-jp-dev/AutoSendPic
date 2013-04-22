using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace AutoSendPic.Model
{
    public abstract class  DataSendJob
    {
        public string FileNameFormat { get; set; }

        public PicData DataToSend { get; set; }

        public DateTime Expire { get; set; }
        

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DataSendJob(PicData dataToSend, DateTime expire)
        {
            this.DataToSend = dataToSend;
            this.Expire = expire;            
        }
        
        public abstract Task<bool> Run();


        public string MakeFileName(DateTime timestamp)
        {
            string fileName = string.Format(FileNameFormat, timestamp);
            return fileName;
        }
        
        public void OnError(Exception ex)
        {
            if (Error != null)
            {
                Error(this, new ExceptionEventArgs(ex));
            }
        }

        public event EventHandler<ExceptionEventArgs> Error;
    }
}