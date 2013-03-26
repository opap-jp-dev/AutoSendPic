using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public class ExceptionEventArgs:EventArgs
    {
        public ExceptionEventArgs(Exception ex)
        {
            this.Exception = ex;
        }

        public Exception Exception
        {
            get;
            set;
        }
    }
}
