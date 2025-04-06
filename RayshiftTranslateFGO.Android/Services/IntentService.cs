using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.OS.Storage;
using Android.Util;
using Android.Widget;
using IO.Rayshift.Translatefgo;
using Java.Lang;
using Java.Net;
using RayshiftTranslateFGO.Annotations;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using Rikka.Shizuku;
using Xamarin.Essentials;
using Xamarin.Forms;
using static Android.Content.PM.PackageManager;
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

        public void OpenExternalStoragePage()
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var mainActivity = Forms.Context as Activity;
                try
                {

                    Android.Net.Uri uri = Android.Net.Uri.Parse("package:" + Forms.Context.ApplicationInfo.PackageName);
                    Intent intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
                    mainActivity.StartActivityForResult(intent, 670);
                }
                catch (System.Exception ex)
                {
                    Intent intent = new Intent();
                    intent.SetAction(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    mainActivity.StartActivityForResult(intent, 670);
                }
            }
        }

        public bool IsExternalStorageManager()
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                return Environment.IsExternalStorageManager;
            }
            else
            {
                return true;
            }
        }


        public IList<ApplicationInfo> GetInstalledApps()
        {
            return Forms.Context.PackageManager.GetInstalledApplications(Android.Content.PM.PackageInfoFlags.MatchAll);
        }

        /// <summary>
        /// DocumentsUI is patched if ver >= 340916000, need Shizuku
        /// </summary>
        /// <returns></returns>
        public long GetDocumentsUiVersion()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Q) // android 10
            {
                return 0;
            }

            var apps = GetInstalledApps().Where(w => w.ProcessName != null && w.ProcessName.Contains("documentsui")).ToList();
            long version = -1;
            long latestBuildTime = 0;
            foreach (var app in apps)
            {
                if (app.ProcessName == null) continue;
                try
                {
                    var package = Forms.Context.PackageManager.GetPackageInfo(app.ProcessName,
                        Android.Content.PM.PackageInfoFlags.MatchAll);

                    if (package == null) continue;

                    // if we find these just cancel out
                    if (app.ProcessName == "com.google.android.documentsui" ||
                        app.ProcessName == "com.android.documentsui")
                    {
                        version = package.LongVersionCode;
                        return version;
                    }

                    // find the newest
                    if (package.LastUpdateTime > latestBuildTime)
                    {
                        latestBuildTime = package.LastUpdateTime;
                        version = package.LongVersionCode;
                    }

                }
                catch (Android.Content.PM.PackageManager.NameNotFoundException)
                {
                    continue;
                }
            }

            return version;
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

        public bool CheckShizukuPerm(bool andBind = false)
        {
            if (Shizuku.IsPreV11)
            {
                // Pre-v11 is unsupported
                MakeToast("Your Shizuku version is too old. Please upgrade.");
                return false;
            }

            if (Shizuku.CheckSelfPermission() == 0)
            {
                // Granted
                if (andBind)
                {
                    BindShizuku();
                }

                return true;
            }
            else if (Shizuku.ShouldShowRequestPermissionRationale())
            {
                Shizuku.RequestPermission(MainActivity.SHIZUKU_PERM);
                return false;
            }
            else
            {
                // Request the permission
                Shizuku.RequestPermission(MainActivity.SHIZUKU_PERM);
                return false;
            }
        }

        public bool IsShizukuAvailable()
        {
            var shizukuActive = Shizuku.PingBinder();

            if (shizukuActive && !MainActivity.ShizukuListenersSetup)
            {
                Shizuku.AddRequestPermissionResultListener(MainActivity.ShizukuListener);
                ShizukuProvider.EnableMultiProcessSupport(true);
                MainActivity.ShizukuListenersSetup = true;
            }


            return shizukuActive;
        }

        public bool IsShizukuServiceBound()
        {
            return MainActivity.NextGenFS.Binder != null;
        }

        internal void BindShizuku()
        {
            if (!IsShizukuServiceBound())
            {
                Context context2 = Android.App.Application.Context;
                var nextClass = Java.Lang.Class.FromType(typeof(NGFSService)).Name;
                var package = context2.PackageName!;

                Log.Info("TranslateFGO", $"Classname: {nextClass}");
                Log.Info("TranslateFGO", $"Package name: {package}");

                var pckManager = context2.PackageManager;

                if (pckManager == null) throw new System.Exception("Null package manager. This should never happen.");
                var verCode = pckManager.GetPackageInfo(package, 0)?.LongVersionCode;

                if (verCode == null) throw new System.Exception("Null verCode. This should never happen.");

                var shizukuArgs = new Shizuku.UserServiceArgs(
                    Android.Content.ComponentName.CreateRelative(package,
                        nextClass)).ProcessNameSuffix("user_service").Debuggable(true).Version((int)verCode);

                Log.Info("TranslateFGO", $"Trying to bind NextGenFS.");

                MainActivity.NextGenFS = new NextGenFSServiceConnection();

                Shizuku.BindUserService(shizukuArgs, MainActivity.NextGenFS);
            }
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