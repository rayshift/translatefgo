using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            TranslationList installedBundle = null;
            bool android11Access = false;
            int region = 1;
            try
            {
                var sm = new ScriptManager();
                var cm = new ContentManager();

                var prefKey = InputData.GetString("preferencesKey");
                region = InputData.GetInt("region", -1);

                if (string.IsNullOrEmpty(prefKey) || region == -1)
                {
                    return 0;
                }

                var storageLocation = Preferences.Get("StorageLocation", "");

                android11Access = !string.IsNullOrEmpty(storageLocation) || !cm.CheckBasicAccess();

                if (string.IsNullOrEmpty(storageLocation) && android11Access)
                {
                    Log.Warn(TAG, "Not setup properly, android storage empty but android 11 mode required. Or no write access.");
                    return 0;
                }

                var installedScriptString = Preferences.Get(prefKey, "");
                if (string.IsNullOrEmpty(installedScriptString))
                {
                    Log.Warn(TAG, "Not setup properly, installed script key empty.");
                    return 0;
                }

                cm.ClearCache();
                var installedFgoInstances = !android11Access ? cm.GetInstalledGameApps(ContentType.DirectAccess) 
                    : cm.GetInstalledGameApps(ContentType.StorageFramework, storageLocation);

                foreach (var instance in installedFgoInstances.ToList())
                {
                    var filePath = android11Access
                        ? $"data/{instance.Path}/files/data/d713/{InstallerPage._assetList}"
                        : $"{instance.Path}/files/data/d713/{InstallerPage._assetList}";
                    var assetStorage = await cm.GetFileContents(
                        android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                        filePath, storageLocation);


                    if (!assetStorage.Successful)
                    {
                        installedFgoInstances.Remove(instance);
                    }

                    instance.LastModified = assetStorage.LastModified;

                    var base64 = "";
                    using var inputStream = new MemoryStream(assetStorage.FileContents);
                    using (var reader = new StreamReader(inputStream, Encoding.ASCII))
                    {
                        base64 = await reader.ReadToEndAsync();
                    }

                    instance.AssetStorage = base64;
                }

                installedBundle = JsonConvert.DeserializeObject<TranslationList>(installedScriptString);

                var installResult = await sm.InstallScript(
                    android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                    (FGORegion)region,
                    installedFgoInstances.Where(w => w.Region == (FGORegion)region).Select(s => s.Path).ToList(),
                    storageLocation,
                    installedBundle.Group,
                    installedFgoInstances.OrderByDescending(o => o.LastModified)?.First()?.AssetStorage,
                    null
                );

                if (!installResult.IsSuccessful)
                {
                    Log.Warn(TAG, $"Unsuccessful installation, reason: {installResult.ErrorMessage}");
                    await rest.SendSuccess((FGORegion)region, (int)installedBundle.Language, TranslationInstallType.Automatic, installedBundle.Group,
                        false, installResult.ErrorMessage, android11Access);
                    return 0;
                }

                Log.Info(TAG, $"Successfully installed bundle {installedBundle.Group}.");
                await rest.SendSuccess((FGORegion)region, (int)installedBundle.Language, TranslationInstallType.Automatic, installedBundle.Group,
                    true, "", android11Access);
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Exception occurred during auto update, {ex}");
                if (installedBundle != null)
                {
                    await rest.SendSuccess((FGORegion) region, (int) installedBundle.Language,
                        TranslationInstallType.Automatic, installedBundle.Group,
                        false, ex.ToString(), android11Access);
                }
                else
                {
                    await rest.SendSuccess((FGORegion)region, 1,
                        TranslationInstallType.Automatic, 0,
                        false, ex.ToString(), android11Access);
                }

                return 0;
            }


            return 1;
        }
    }
}