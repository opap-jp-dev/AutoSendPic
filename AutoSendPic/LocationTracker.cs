using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using Android.Locations;

using AutoSendPic.Model;

namespace AutoSendPic
{
    /// <summary>
    /// GPS等の位置情報を受信する
    /// </summary>
    public class LocationTracker : Java.Lang.Object, ILocationListener
    {

        /// <summary>
        /// 位置情報マネージャ
        /// </summary>
        LocationManager locationMan;

        /// <summary>
        /// 最後に測位した位置情報プロバイダ
        /// </summary>
        public string LastProvider
        {
            get;
            private set;
        }
        /// <summary>
        /// 最後にGPSを受信した時刻
        /// </summary>
        public DateTime LastGPSReceived { get; private set; }

        /// <summary>
        /// メインの位置情報プロバイダ
        /// </summary>
        public string MainProvider { get; private set; }

        /// <summary>
        /// 最後に測位した位置情報
        /// </summary>
        public LocationData LastLocation { get; private set; }

        /// <summary>
        /// 受信中かどうかを取得します
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context"></param>
        public LocationTracker(Context context, string provider = LocationManager.GpsProvider)
        {
            locationMan = (LocationManager)context.GetSystemService(Service.LocationService);


            if(locationMan.GetProviders(true).Contains(provider))
            {
                MainProvider = provider;
            }
            else if (locationMan.IsProviderEnabled(LocationManager.GpsProvider))
            {
                MainProvider = LocationManager.GpsProvider;
            }
            else if (locationMan.IsProviderEnabled(LocationManager.NetworkProvider))
            {
                MainProvider = LocationManager.NetworkProvider;
            }
            else
            {
                Criteria crit = new Criteria();
                crit.Accuracy = Accuracy.Fine;
                MainProvider = locationMan.GetBestProvider(crit, true);
            }
            LastGPSReceived = DateTime.MinValue;

        }

        /// <summary>
        /// ファイナライザ
        /// </summary>
        ~LocationTracker()
        {
            Dispose(false);
        }

        /// <summary>
        /// 受信を開始する
        /// </summary>
        public void Start()
        {
            locationMan.GetLastKnownLocation(MainProvider);
            locationMan.RequestLocationUpdates(MainProvider, 1000, 1, this);

            IsActive = true;
        }

        /// <summary>
        /// 受信を終了する
        /// </summary>
        public void Stop()
        {
            if (IsActive)
            {
                locationMan.RemoveUpdates(this);
            }
            IsActive = false;
        }

        /// <summary>
        /// 破棄
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (locationMan != null)
            {
                try
                {
                    Stop();
                }
                catch { }
                locationMan.Dispose();
                locationMan = null;
            }
            base.Dispose(disposing);
        }

        #region ILocationListener メンバー

        /// <summary>
        /// 位置情報が変化したとき
        /// </summary>
        public void OnLocationChanged(Location location)
        {
            //UNIX時刻
            DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            LastLocation = new LocationData()
            {
                Provider = location.Provider,
                Accuracy = location.Accuracy,
                Altitude = location.Altitude,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Speed = location.Speed,
                Time = UnixEpoch.AddMilliseconds(location.Time).ToLocalTime()
            };
        }

        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
            if (IsActive)
            {
                locationMan.RequestLocationUpdates(provider, 1000, 1, this);
            }
        }

        public void OnStatusChanged(string provider, Availability status, Bundle extras)
        {
            
        }

        #endregion
    }
}
