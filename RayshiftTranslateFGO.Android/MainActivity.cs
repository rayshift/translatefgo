using System;
using System.Globalization;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Common;
using Android.Util;
using Firebase.Messaging;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Platform = Xamarin.Essentials.Platform;

namespace RayshiftTranslateFGO.Droid
{
    [Activity(Label = "Translate Fate/GO", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : FormsAppCompatActivity
    {
        public Context Context;
#if DEBUG
        internal static readonly string CHANNEL_ID = "announcements_debug";
        public const string UPDATE_CHANNEL_NAME = "update_debug";
#else
        internal static readonly string CHANNEL_ID = "announcements";
        public const string UPDATE_CHANNEL_NAME = "update";
#endif

        internal static readonly int NOTIFICATION_ID = 100;
        public const string TAG = "MainActivity";
        public bool GooglePlayAvailable { get; set; }
        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;
            Context = this.ApplicationContext;

            base.OnCreate(savedInstanceState);
            Platform.Init(this, savedInstanceState);
            Forms.Init(this, savedInstanceState);
            AssetManager assets = this.Assets;

#if DEBUG
            if (Intent.Extras != null)
            {
                foreach (var key in Intent.Extras.KeySet())
                {
                    if (key != null)
                    {
                        var value = Intent.Extras.GetString(key);
                        Log.Debug(TAG, "Key: {0} Value: {1}", key, value);
                    }
                }
            }
#endif
            IsPlayServicesAvailable();
            CreateNotificationChannel();

            FirebaseMessaging.Instance.SubscribeToTopic(CHANNEL_ID);
            FirebaseMessaging.Instance.SubscribeToTopic(UPDATE_CHANNEL_NAME);

            LoadApplication(new App());
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public bool IsPlayServicesAvailable()
        {
            int resultCode = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this);
            if (resultCode != ConnectionResult.Success)
            {
                if (GoogleApiAvailability.Instance.IsUserResolvableError(resultCode))
                    GooglePlayAvailable = false;
                else
                {
                    Finish();
                }
                return false;
            }
            else
            {
                GooglePlayAvailable = true;
                return true;
            }
        }

        void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                // Notification channels are new in API 26 (and not a part of the
                // support library). There is no need to create a notification
                // channel on older versions of Android.
                return;
            }

            var channel = new NotificationChannel(CHANNEL_ID,
                "Announcements",
                NotificationImportance.Default)
            {

                Description = "Rayshift Translate FGO announcements"
            };

            var notificationManager = (NotificationManager)GetSystemService(Context.NotificationService);
            notificationManager.CreateNotificationChannel(channel);

            var channel2 = new NotificationChannel(UPDATE_CHANNEL_NAME,
                "Updates",
                NotificationImportance.Default)
            {

                Description = "Rayshift Translate FGO script updates"
            };

            var notificationManager2 = (NotificationManager)GetSystemService(Context.NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }
    }
}