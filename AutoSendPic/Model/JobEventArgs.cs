using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public class JobEventArgs : EventArgs
    {
        public JobEventArgs(DataSendJob job)
        {
            this.Job = job;
        }

        public DataSendJob Job
        {
            get;
            set;
        }
    }
}
