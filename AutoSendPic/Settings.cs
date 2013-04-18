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

        public bool UseHttp { get; set; }

        public string HttpUrl { get; set; }

        public string HttpUser { get; set; }

        public string HttpPass { get; set; }

        public bool BeepOnError { get; set; }

        public bool UsePreview { get; set; }




        public static Settings Load(Context con)
        {
            Settings s = new Settings();
            ISharedPreferences p = PreferenceManager.GetDefaultSharedPreferences(con);

            s.MinInterval = int.Parse(p.GetString("MinInterval", "10"));
            s.Width = int.Parse(p.GetString("Width", "640"));
            s.Height = int.Parse(p.GetString("Height", "480"));
            s.OutputDir = p.GetString("OutputDir", Android.OS.Environment.ExternalStorageDirectory + "/AutoSendPic/");
            s.UseHttp = p.GetBoolean("UseHttp", true);
            s.HttpUrl = p.GetString("HttpUrl", "");
            s.HttpUser = p.GetString("HttpUser", "");
            s.HttpPass = p.GetString("HttpPass", "");
            s.BeepOnError = p.GetBoolean("BeepOnError", true);
            s.UsePreview = p.GetBoolean("UsePreview", true);
 
            return s;
        }
    }
}
