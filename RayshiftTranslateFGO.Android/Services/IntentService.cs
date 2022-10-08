using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS.Storage;
using Android.Widget;
using Java.Net;
using RayshiftTranslateFGO.Annotations;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using Xamarin.Essentials;
using Xamarin.Forms;
using Application = Xamarin.Forms.Application;
using Environment = Android.OS.Environment;

[assembly: Xamarin.Forms.Dependency(typeof(RayshiftTranslateFGO.Droid.IntentService))]
namespace RayshiftTranslateFGO.Droid
{
    public class IntentService: IIntentService
    {
        public IntentService()
        {

        }

        [Obsolete]
        public IList<StorageVolume> GetStorageVolumes()
        {
            var activity = Forms.Context as Activity;

            var storageManager = Forms.Context.GetSystemService(Context.StorageService) as StorageManager;
            var volumes = storageManager.StorageVolumes;

            return volumes;
        }

        public IList<ApplicationInfo> GetInstalledApps()
        {
            return Forms.Context.PackageManager.GetInstalledApplications(PackageInfoFlags.MatchAll);
        }

        public bool TestManualLocationRequired()
        {
            var storageManager = Forms.Context.GetSystemService(Context.StorageService) as StorageManager;
            Intent documentIntent;
            try
            {
                documentIntent = storageManager.PrimaryStorageVolume.CreateOpenDocumentTreeIntent();
            }
            catch (Java.Lang.IncompatibleClassChangeError) // bluestacks
            {
                documentIntent = new Intent(Intent.ActionOpenDocumentTree);
            }

            var uri = documentIntent.GetParcelableExtra("android.provider.extra.INITIAL_URI") as Android.Net.Uri;
            var scheme = uri?.ToString();

            if (scheme == null)
            {
                documentIntent.Dispose();
                return true;
            }

            documentIntent.Dispose();
            return false;
        }

        public void OpenDocumentTreeIntent(string what, string append = null)
        {
            /*
              "Then out spake brave Horatius,
               The Captain of the Gate:
               'To every man upon this earth
               Death cometh soon or late.
               And how can man die better
               Than facing fearful odds,
               For the ashes of his fathers,
               And the temples of his gods'"
             */

            var activity = Forms.Context as Activity;

            var storageManager = Forms.Context.GetSystemService(Context.StorageService) as StorageManager;
            Intent documentIntent;
            try
            {
                documentIntent = storageManager.PrimaryStorageVolume.CreateOpenDocumentTreeIntent();
            }
            catch (Java.Lang.IncompatibleClassChangeError) // bluestacks
            {
                documentIntent = new Intent(Intent.ActionOpenDocumentTree);
            }


            var uri = documentIntent.GetParcelableExtra("android.provider.extra.INITIAL_URI") as Android.Net.Uri;
            var scheme = uri?.ToString();

            if (scheme != null)
            {
                scheme = scheme.Replace("/root/", "/document/");

                scheme += "%3AAndroid%2F" + append;


                documentIntent.PutExtra("android.provider.extra.INITIAL_URI", Android.Net.Uri.Parse(scheme));
            }
            else
            {
                var documentUri = "content://com.android.externalstorage.documents/document/primary%3AAndroid%2F" + append;
                documentIntent.PutExtra("android.provider.extra.INITIAL_URI", Android.Net.Uri.Parse(documentUri));
            }

            documentIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission |
                                    ActivityFlags.GrantPrefixUriPermission |
                                    ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(documentIntent, (int) MainActivity.RequestCodes.FolderIntentRequestCode);
        }

        [ItemCanBeNull]
        public async Task<WebAuthenticatorResult> LinkAccount()
        {
            try
            {
                var authResult = await WebAuthenticator.AuthenticateAsync(
                    new Uri($"{EndpointURL.EndPoint}/identity/account/manage/link"),
                    new Uri("translatefgo://link"));

                return authResult;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        public void MakeToast(string message)
        {
            Toast.MakeText(Android.App.Application.Context, message, ToastLength.Long)?.Show();
        }

        public void ExitApplication()
        {
            var activity = (Activity)Forms.Context;
            activity.FinishAffinity();
        }
    }
}