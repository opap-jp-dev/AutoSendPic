using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoSendPic.Model
{
    public struct LocationData
    {
        public string Provider { get;  set; }
        public double Altitude { get;  set; }
        public double Accuracy { get;  set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get;  set; }
        public DateTime Time { get;  set; }
    }
}
