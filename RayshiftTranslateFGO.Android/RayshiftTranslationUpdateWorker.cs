using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Util;
using AndroidX.Work;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.Views;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace RayshiftTranslateFGO.Droid
{
    public class RayshiftTranslationUpdateWorker: Worker
    {
        const string TAG = "RayshiftTranslationUpdateWorker";

        private HandshakeAPIResponse _handshake;
        private TranslationList _translation { get; set; }

        public RayshiftTranslationUpdateWorker(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public RayshiftTranslationUpdateWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
        {
        }

        public override Result DoWork()
        {
            var updateTask = BeginUpdate();
            Task.WaitAll(updateTask);
            return updateTask.Result == 0 ? Result.InvokeFailure() : Result.InvokeSuccess();
        }

        public async Task<int> BeginUpdate()
        {
            var rest = new RestfulAPI();
            var handshake = await rest.GetHandshakeApiResponse();

            _handshake = handshake;

            // Check for valid status
            if (handshake == null || handshake.Status != 200)
            {
                Log.Warn(TAG, "Bad handshake for script update.");
                return 0;
            }

            if (!string.IsNullOrEmpty(handshake.Response.Endpoint))
            {
                rest.SetEndpoint(handshake.Response.Endpoint);
            }

            // Check for app updates
            var currentVersion = ScriptUtil.GetBuild();
            if (int.Parse(handshake.Response.UpdateVer) > currentVersion)
            {
                Log.Warn(TAG, "Bad Translate FGO app version for script update.");
                return 0;
            }

            var existingFateApp = Android.App.Application.Context.PackageManager
                .GetInstalledApplications(PackageInfoFlags.MatchAll)
                .FirstOrDefault(x => x.PackageName == "com.aniplex.fategrandorder");

            var existingFateAppBetter = Android.App.Application.Context.PackageManager
                .GetInstalledApplications(PackageInfoFlags.MatchAll)
                .FirstOrDefault(x => x.PackageName == "io.rayshift.betterfgo");

            bool installToBetter = false;
            bool installToJp = false;

            if (existingFateApp == null && existingFateAppBetter == null)
            {
                Log.Warn(TAG, "FGO not installed for script update.");
                return 0;
            }

            if (existingFateAppBetter != null)
            {
                var packageVersion = Android.App.Application.Context.PackageManager
                    .GetPackageInfo("io.rayshift.betterfgo", 0)
                    ?.VersionName;
                bool valid = ScriptUtil.IsValidAppVersion(handshake.Response.AppVer, packageVersion);
                if (!valid)
                {
                    Log.Warn(TAG, "Better FGO App version not valid for script update.");
                    return 0;
                }

                installToBetter = true;
            }
            if (existingFateApp != null)
            {
                var packageVersion = Android.App.Application.Context.PackageManager
                    .GetPackageInfo("com.aniplex.fategrandorder", 0)
                    ?.VersionName;
                bool valid = ScriptUtil.IsValidAppVersion(handshake.Response.AppVer, packageVersion);
                if (!valid)
                {
                    Log.Warn(TAG, "FGO App version not valid for script update.");
                    if (!installToBetter)
                    {
                        return 0;
                    }
                }
                else
                {
                    installToJp = true;
                }

            }
            

            var externalPath = System.IO.Directory.GetParent(Android.App.Application.Context.GetExternalFilesDir(null).Parent);
            if (await Permissions.CheckStatusAsync<Permissions.StorageWrite>() == PermissionStatus.Granted && externalPath.Exists)
            {
                var assetPathJp = Path.Combine(externalPath.ToString(), "com.aniplex.fategrandorder/files/data/d713/");
                var assetPathBetter = Path.Combine(externalPath.ToString(), "io.rayshift.betterfgo/files/data/d713/");

                List<string> installPaths = new List<string>();
                if (installToJp)
                {
                    installPaths.Add(assetPathJp);
                }
                if (installToBetter)
                {
                    installPaths.Add(assetPathBetter);
                }

                foreach (var assetPath in installPaths)
                {
                    if (System.IO.Directory.Exists(assetPath) && existingFateApp != null)
                    {
                        var assetStorage = Path.Combine(assetPath, ManagerPage._assetList);
                        if (File.Exists(assetStorage))
                        {
                            await using var testFs = new System.IO.FileStream(assetStorage, FileMode.Open);
                            if (!(testFs.CanRead && testFs.CanWrite))
                            {
                                Log.Warn(TAG, $"Can't write to {assetPath}, exiting early.");
                                return 0;
                            }
                        }
                    }
                }

                try
                {

                    return await RunUpdate(installPaths);

                }
                catch (Exception ex)
                {
                    Log.Warn(TAG,
                        $"An exception occurred when trying to run an automatic update: {ex.ToString()}");
                    return 0;
                }

            }
            Log.Warn(TAG, "Fallout error on BeginUpdate() for script update.");
            return 0;
        }

        public async Task<int> RunUpdate(List<string> installPaths)
        {
            List<string> _validSha = new List<string>();

            var installedScriptString = Preferences.Get("InstalledScript", null);

            if (_handshake.Response.Translations.Count > 0)
            {
                var installedBundle = JsonConvert.DeserializeObject<TranslationList>(installedScriptString);

                
                foreach (var scriptBundleSet in _handshake.Response.Translations)
                {
                    if (installedBundle.Group == scriptBundleSet.Group)
                    {
                        _translation = scriptBundleSet;
                    }
                }
            }
            else
            {
                Log.Warn(TAG, "No scripts found for script update.");
                return 0;
            }

            // Run update for real now
            Dictionary<string, byte[]> filesToWrite = new Dictionary<string, byte[]>();
            var rs = new RestfulAPI();
            
            // Get update to install
            if (_translation == null)
            {
                return 0;
            }

            // Get files to write
            var items = _translation.Scripts.ToList();
            for (var i = 0; i < _translation.Scripts.Count; i++)
            {
                //ProgramStatus.Text = $"downloading script {i + 1} of {_translations[toInstall].Scripts.Count}...";
                var downloadUrl = items[i].Value.DownloadURL;
                var script = await rs.GetScript(downloadUrl);

                if (script == null)
                {
                    throw new Exception($"Empty file.\nDownload URL: {downloadUrl}");
                }

                var scriptSha = ScriptUtil.Sha1(script);

                if (scriptSha != items[i].Value.TranslatedSHA1)
                {
                    throw new Exception($"Checksum failure.\nReal file length: {script.Length}\nDownload URL: {downloadUrl}");
                }

                foreach (var path in installPaths)
                {
                    filesToWrite.Add(Path.Combine(path, items[i].Key), script);
                }
            }

            foreach (var file in filesToWrite)
            {
                Log.Debug(TAG, $"Writing file {file.Key}");
                await File.WriteAllBytesAsync(file.Key, file.Value);
            }

            Preferences.Set("InstalledScript", JsonConvert.SerializeObject(_translation));
            return 1;
        }
    }
}