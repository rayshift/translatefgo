using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Work;
using Firebase.Messaging;
using RayshiftTranslateFGO.Services;
using Xamarin.Essentials;

namespace RayshiftTranslateFGO.Droid
{
    [Service]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    [IntentFilter(new[] { "com.google.firebase.INSTANCE_ID_EVENT" })]
    public class RayshiftFirebaseMessagingService : FirebaseMessagingService
    {
        const string TAG = "RayshiftFirebaseMsgService";

        public override void OnMessageReceived(RemoteMessage message)
        {
            //Log.Debug(TAG, "From: " + message.From);
            //var body = message.GetNotification().Body; 
            //Log.Debug(TAG, "Notification Message Body: " + body);
            Log.Info(TAG, $"Received a firebase request from {message.From}.");
            if (message.From == $"/topics/{MainActivity.CHANNEL_ID}")
            {
                Log.Debug(TAG, "Received an announcement.");
                SendNotification(message.GetNotification().Body, message.GetNotification().Title, message.Data);
            }
            else if (message.From == $"/topics/{MainActivity.UPDATE_CHANNEL_NAME}" && 
                     !Preferences.ContainsKey("DisableAutoUpdate"))
            {
                Log.Debug(TAG, "Received an update request.");

                if (!message.Data.ContainsKey("region") || !int.TryParse(message.Data["region"], out var region))
                {
                    Log.Warn(TAG, "Firebase message missing region.");
                    return;
                }

                if (region < 1 || region > 2)
                {
                    Log.Warn(TAG, "Invalid region for update request.");
                    return;
                }

                string preferencesKey = "";
                switch (region)
                {
                    case 1: 
                        preferencesKey = $"InstalledScript_{FGORegion.Jp}";
                        break;
                    case 2:
                        preferencesKey = $"InstalledScript_{FGORegion.Na}";
                        break;
                }

                if (Preferences.Get(preferencesKey, null) == null)
                {
                    Log.Warn(TAG, "User hasn't installed any script for this region.");
                    return;
                }

                var data = new Data.Builder();
                data.PutInt("region", region);
                data.PutString("preferencesKey", preferencesKey);
                var finalData = data.Build();
                var builder = OneTimeWorkRequest.Builder.From<RayshiftTranslationUpdateWorker>();
                builder.SetInputData(finalData);

                OneTimeWorkRequest request = builder.Build();
                WorkManager.Instance.Enqueue(request);

            }
        }
        void SendNotification(string messageBody, string messageTitle, IDictionary<string, string> data)
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop);
            foreach (var key in data.Keys)
            {
                intent.PutExtra(key, data[key]);
            }

            var pendingIntent = PendingIntent.GetActivity(this,
                MainActivity.NOTIFICATION_ID,
                intent,
                PendingIntentFlags.OneShot);

            var notificationBuilder = new NotificationCompat.Builder(this, MainActivity.CHANNEL_ID)
                .SetSmallIcon(Resource.Drawable.ic_action_book)
                .SetLargeIcon(BitmapFactory.DecodeResource(Resources, Resource.Drawable.ic_stat_ic_notification))
                .SetContentTitle(messageTitle)
                .SetContentText(messageBody)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent);

            var notificationManager = NotificationManagerCompat.From(this);
            notificationManager.Notify(MainActivity.NOTIFICATION_ID, notificationBuilder.Build());
        }

        public override void OnNewToken(string token)
        {
#if DEBUG
            Log.Debug(TAG, "FCM token: " + token);
#endif
            SendRegistrationToServer(token);
        }

        async void SendRegistrationToServer(string token)
        {
            var rest = new RestfulAPI();
            await rest.SendRegistrationToken(token);
        }
    }
}