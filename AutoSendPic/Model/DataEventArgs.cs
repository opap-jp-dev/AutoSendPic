using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public class DataEventArgs : EventArgs
    {
        public DataEventArgs(PicData pd)
        {
            this.Data = pd;
        }

        public PicData Data
        {
            get;
            set;
        }
    }
}
