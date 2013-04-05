using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Preferences;
using Android.Content;

namespace AutoSendPic
{
    public class Settings
    {
        public int MinInterval { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string OutputDir { get; set; }

        public string FtpUri { get; set; }

        public string FtpUser { get; set; }

        public string FtpPass { get; set; }






        public static Settings Load(Context con)
        {
            Settings s = new Settings();
            ISharedPreferences p = PreferenceManager.GetDefaultSharedPreferences(con);

			s.MinInterval = int.Parse ( p.GetString ("MinInterval", "1"));
			s.Width = int.Parse ( p.GetString("Width", "1280"));
			s.Height = int.Parse ( p.GetString("Height", "720"));
            s.OutputDir = p.GetString("OutputDir", Android.OS.Environment.ExternalStorageDirectory + "/AutoSendPic/");
            s.FtpUri = p.GetString("FtpUri", "");
            s.FtpUser = p.GetString("FtpUser", "");
            s.FtpPass = p.GetString("FtpPass", "");
                      

            return s;
        }
    }
}
