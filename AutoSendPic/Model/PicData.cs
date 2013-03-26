using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public class PicData
    {
        public DateTime TimeStamp { get; set; }
        public byte[] Data { get; set; }

        public PicData(byte[] aData, DateTime aTimeStamp)
        {
            this.Data = aData;
            this.TimeStamp = aTimeStamp;
        }

    }
}
