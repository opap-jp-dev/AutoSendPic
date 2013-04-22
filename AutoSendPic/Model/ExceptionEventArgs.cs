using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public class ExceptionEventArgs:EventArgs
    {
        public ExceptionEventArgs(Exception ex, object data)
        {
            this.Exception = ex;
            this.Data = data;
        }

        public ExceptionEventArgs(Exception ex)
            : this(ex, null)
        {
        }

        public Exception Exception
        {
            get;
            set;
        }

        public object Data
        {
            get;
            set;
        }
    }
}
