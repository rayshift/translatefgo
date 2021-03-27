using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Database;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Common;
using Android.Provider;
using Android.Support.V4.Provider;
using Android.Util;
using Firebase.Messaging;
using Java.Interop;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.Views;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Environment = Android.OS.Environment;
using Platform = Xamarin.Essentials.Platform;
using Uri = Android.Net.Uri;

namespace RayshiftTranslateFGO.Droid
{
    [Activity(Label = "Translate Fate/GO", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : FormsAppCompatActivity
    {
        public Context Context;
#if DEBUG
        internal static readonly string CHANNEL_ID = "announcements_v2_debug";
        public const string UPDATE_CHANNEL_NAME = "update_v2_debug";
#else
        internal static readonly string CHANNEL_ID = "announcements_v2";
        public const string UPDATE_CHANNEL_NAME = "update_v2";
#endif

        public enum RequestCodes
        {
            FolderIntentRequestCode
        }
        
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

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            base.OnActivityResult(requestCode, resultCode, intent);
            Log.Info("TranslateFGO", $"ActivityResult Code: {resultCode}, Result: {resultCode}, Data: {intent?.DataString}");

            if (resultCode == Result.Ok && requestCode == (int)RequestCodes.FolderIntentRequestCode)
            {
                var service = new ContentManager();

                var uri = intent.Data;
                var selectedFolder = DocumentsContract.GetTreeDocumentId(uri);

                if (selectedFolder == null || !selectedFolder.EndsWith("Android"))
                {
                    
                    var folderSplit = selectedFolder?.Split(":").Last();
                    var infoText = UIFunctions.GetResourceString("AndroidFolderNotSelected");
                    var errorMessage = String.Format(infoText, !string.IsNullOrEmpty(folderSplit) ? folderSplit : "none");
                    Toast.MakeText(this.Context, errorMessage, ToastLength.Long)?.Show();
                    return;
                }

                service.ClearCache();
                var dataChildren = service.GetFolderChildren(uri, "data/"); // Get list of children to find FGO folders

                if (dataChildren.Count == 0)
                {
                    var infoText = UIFunctions.GetResourceString("AndroidDataFolderEmpty");
                    Toast.MakeText(this.Context, infoText, ToastLength.Long)?.Show();
                    return;
                }

                var appNamesList = ContentManager.ValidAppNames.ToList();
                bool found = false;
                foreach (var folder in dataChildren)
                {
                    if (appNamesList.Contains(folder.Path.Split("/").Last()))
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    var infoText = UIFunctions.GetResourceString("NoFGOInstallationFoundToast");
                    Toast.MakeText(this.Context, infoText, ToastLength.Long)?.Show();
                    return;
                }

                // Save URL
                try
                {
                    this.ContentResolver.TakePersistableUriPermission(uri, intent.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission));
                }
                catch (Exception ex)
                {
                    Log.Error("BetterFGO", $"Error thrown while persisting uri: {ex}");
                    var errorText = UIFunctions.GetResourceString("UnknownError");
                    var errorMessage = String.Format(errorText, ex.ToString());
                    Toast.MakeText(this.Context, errorMessage, ToastLength.Long)?.Show();
                }

                // If we got this far, all is well
                var successText = UIFunctions.GetResourceString("Android11SetupSuccessful");
                Toast.MakeText(this.Context, successText, ToastLength.Long)?.Show();

                Log.Info("BetterFGO", $"Saving URI: {uri?.ToString()}");

                Preferences.Set("StorageType", (int)ContentType.StorageFramework);
                Preferences.Set("StorageLocation", uri?.ToString());

                MessagingCenter.Send(Xamarin.Forms.Application.Current, "return_to_main_page");
            }
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

            notificationManager.CreateNotificationChannel(channel2);
        }
    }

}