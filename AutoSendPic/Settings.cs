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

			s.MinInterval = p.GetInt ("MinInterval", 1);
            s.Width = p.GetInt("Width", 1280);
            s.Height = p.GetInt("Height", 720);
            s.OutputDir = p.GetString("OutputDir", Android.OS.Environment.ExternalStorageDirectory + "/AutoSendPic/");
            s.FtpUri = p.GetString("FtpUri", "");
            s.FtpUser = p.GetString("FtpUser", "");
            s.FtpPass = p.GetString("FtpPass", "");
                      

            return s;
        }
    }
}
