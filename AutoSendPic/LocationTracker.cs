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
    public class LocationTracker : Java.Lang.Object, ILocationListener
    {

        LocationManager locationMan;


        public string LastProvider
        {
            get;
            private set;
        }
        public DateTime LastGPSReceived { get; private set; }
        public string MainProvider { get; private set; }
        public LocationData LastLocation { get; private set; }

        public LocationTracker(Context context)
        {
            locationMan = (LocationManager)context.GetSystemService(Service.LocationService);

            Criteria crit = new Criteria();
            crit.Accuracy = Accuracy.Fine;

            if (locationMan.IsProviderEnabled(LocationManager.GpsProvider))
            {
                MainProvider = LocationManager.GpsProvider;
            }
            else if (locationMan.IsProviderEnabled(LocationManager.NetworkProvider))
            {
                MainProvider = LocationManager.NetworkProvider;
            }
            else
            {
                MainProvider = locationMan.GetBestProvider(crit, true);
            }
            LastGPSReceived = DateTime.MinValue;

        }

        public void Start()
        {
            locationMan.GetLastKnownLocation(MainProvider);
            foreach (string prov in locationMan.GetProviders(true))
            {
                locationMan.GetLastKnownLocation(prov);
                locationMan.RequestLocationUpdates(prov, 1000, 1, this);
            }
        }

        public void Stop()
        {
            locationMan.RemoveUpdates(this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Stop();
            locationMan.Dispose();
        }

        #region ILocationListener メンバー

        public void OnLocationChanged(Location location)
        {

            if (location.Provider != LocationManager.GpsProvider &&
                DateTime.Now - LastGPSReceived < TimeSpan.FromMinutes(1))
            {
                return; // 1分以内にGPSを受信した場合は、GPS以外の情報は無視
            }

            if (location.Provider == LocationManager.GpsProvider)
            {
                LastGPSReceived = DateTime.Now;
            }

            LastLocation = new LocationData()
            {
                Provider = location.Provider,
                Accuracy = location.Accuracy,
                Altitude = location.Altitude,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Speed = location.Speed,
                Time = new DateTime(location.Time)
            };
        }

        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string provider, Availability status, Bundle extras)
        {
        }

        #endregion
    }
}
