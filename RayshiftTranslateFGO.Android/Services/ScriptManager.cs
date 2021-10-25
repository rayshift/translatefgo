using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AndroidX.AppCompat.Content.Res;
using Dasync.Collections;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.Views;
using Xamarin.Essentials;
using Xamarin.Forms;

[assembly: Xamarin.Forms.Dependency(typeof(RayshiftTranslateFGO.Droid.ScriptManager))]
namespace RayshiftTranslateFGO.Droid
{
    public class ScriptManager: IScriptManager
    {
        private readonly IContentManager _cm;
        public ScriptManager()
        {
            _cm = new ContentManager();
        }

        public async Task<ScriptInstallStatus> InstallScript(ContentType contentType, FGORegion region, List<string> installPaths, string baseInstallPath, int installId, string assetStorageCheck = null,
            ObservableCollection<InstallerPage.TranslationGUIObject> translationGuiObjects = null)
        {
            _cm.ClearCache();
            var gui = translationGuiObjects != null;
            var guiObject = gui ? translationGuiObjects.First(w => w.BundleID == installId) : null;
            if (guiObject != null)
            {
                guiObject.Status =
                    UIFunctions.GetResourceString("InstallingFetchingHandshake");
                guiObject.ButtonInstallText = UIFunctions.GetResourceString("InstallingText");
                guiObject.TextColor = Color.Chocolate;
            }
            // fetch new translation list to ensure it is up to date
            var restful = new RestfulAPI();
            var translationList = await restful.GetHandshakeApiResponse(region, assetStorageCheck);

            if (!translationList.IsSuccessful || translationList.Data.Status != 200)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallHandshakeFailure"), translationList.StatusCode, translationList.Data?.Message)
                };
            }

            if (assetStorageCheck != null)
            {
                switch (translationList.Data.Response.AssetStatus)
                {
                    case HandshakeAssetStatus.UpdateRequired:
                        return new ScriptInstallStatus()
                        {
                            IsSuccessful = false,
                            ErrorMessage = String.Format("AssetStorage.txt out of date, skipping update.")
                        };
                    default:
                        break;
                }
            }

            // download required scripts
            var groupToInstall = translationList.Data.Response.Translations.FirstOrDefault(w => w.Group == installId);
            if (groupToInstall?.Scripts == null)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallMissingScriptFailure"), installId)
                };
            }

            if (!gui && groupToInstall.Hidden)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallHiddenScriptFailure"), installId)
                };
            }

            var totalScripts = groupToInstall.Scripts.Count;

            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallDownloadingScripts"), 1, totalScripts);
            }

            ConcurrentDictionary<string, byte[]> scriptDictionary = new ConcurrentDictionary<string, byte[]>();
            object lockObj = new Object();

            try
            {
                await groupToInstall.Scripts.ParallelForEachAsync(async script =>
                {
                    var name = script.Key;
                    var downloadUrl = script.Value.DownloadURL;
                    var downloadResponse = await restful.GetScript(downloadUrl);
                    var downloadedScript = downloadResponse.RawBytes;

                    if (!downloadResponse.IsSuccessful)
                    {
                        throw new EndEarlyException(String.Format(UIFunctions.GetResourceString("InstallScriptDownloadFailure"),
                            installId, downloadUrl, downloadResponse.ErrorMessage));
                    }

                    if (downloadedScript == null)
                    {
                        throw new EndEarlyException(String.Format(UIFunctions.GetResourceString("InstallEmptyFileFailure"),
                            installId, downloadUrl));
                    }

                    var scriptSha = ScriptUtil.Sha1(downloadedScript);

                    if (scriptSha != script.Value.TranslatedSHA1)
                    {
                        throw new EndEarlyException(String.Format(UIFunctions.GetResourceString("InstallChecksumFailure"),
                            installId, downloadedScript.Length, downloadUrl));
                    }

                    scriptDictionary.TryAdd(name, downloadedScript);
                    lock (lockObj)
                    {
                        if (guiObject != null)
                        {
                            guiObject.Status =
                                String.Format(UIFunctions.GetResourceString("InstallDownloadingScripts"),
                                    Math.Min(scriptDictionary.Count, totalScripts), totalScripts);
                        }
                    }
                },maxDegreeOfParallelism:4);
            }
            catch (EndEarlyException ex)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.ToString()
                };
            }

            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallDownloadNewAssetStorage"));
            }

            Dictionary<string, byte[]> newAssetStorages = new Dictionary<string, byte[]>();
            // get new assetstorage.txt
            foreach (var game in installPaths)
            {
                var assetStoragePath = contentType != ContentType.DirectAccess ? $"data/{game}/files/data/d713/{InstallerPage._assetList}"
                    : $"{game}/files/data/d713/{InstallerPage._assetList}";

                var fileContents = await _cm.GetFileContents(contentType, assetStoragePath, baseInstallPath);

                if (!fileContents.Successful || fileContents.FileContents.Length == 0)
                {
                    return new ScriptInstallStatus()
                    {
                        IsSuccessful = false,
                        ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallEmptyAssetStorage"), installId, assetStoragePath, fileContents.Error)
                    };
                }

                
                // remove bom
                var base64 = "";
                await using var inputStream = new MemoryStream(fileContents.FileContents);
                using (var reader = new StreamReader(inputStream, Encoding.ASCII))
                {
                    base64 = await reader.ReadToEndAsync();
                }
                var newAssetList = await restful.SendAssetList(base64, installId, region);

                if (!newAssetList.IsSuccessful)
                {
                    return new ScriptInstallStatus()
                    {
                        IsSuccessful = false,
                        ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallAssetStorageAPIFailure"), installId, newAssetList.StatusCode, newAssetList.Data?.Message)
                    };
                }

                // add bom
                await using var outputStream = new MemoryStream();
                await using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
                {
                    await writer.WriteAsync(newAssetList.Data.Response["data"]);
                }

                newAssetStorages.Add(assetStoragePath, outputStream.ToArray());
            }

            // prepare files

            Dictionary<string, byte[]> filesToWrite = newAssetStorages;

            foreach (var game in installPaths)
            {
                foreach (var asset in scriptDictionary)
                {
                    var assetInstallPath = contentType != ContentType.DirectAccess
                        ? $"data/{game}/files/data/d713/{asset.Key}"
                        : $"{game}/files/data/d713/{asset.Key}";

                    filesToWrite.Add(assetInstallPath, asset.Value);
                }
            }

            // write files
            int j = 1;
            int tot = filesToWrite.Count;
            foreach (var file in filesToWrite)
            {

                if (file.Key.EndsWith(".bin"))
                {
                    await _cm.RemoveFileIfExists(contentType,
                        file.Key, baseInstallPath);
                }
            }
            _cm.ClearCache();

            foreach (var file in filesToWrite)
            {
                if (guiObject != null)
                {
                    guiObject.Status =
                        String.Format(UIFunctions.GetResourceString("InstallWriteFile"), j, tot);
                }

                await _cm.WriteFileContents(contentType, file.Key, baseInstallPath, file.Value);

                j += 1;
            }


            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallFinished"));
                guiObject.TextColor = Color.LimeGreen;
            }
            Preferences.Set($"InstalledScript_{region}", JsonConvert.SerializeObject(groupToInstall));

            return new ScriptInstallStatus()
            {
                IsSuccessful = true,
                ErrorMessage = ""
            };
        }

        public Task<bool> UninstallScripts(ContentType contentType, FGORegion region, List<string> installPaths, string baseInstallPath)
        {
            throw new System.NotImplementedException(); // implemented locally
        }
    }

    public class EndEarlyException : Exception
    {
        public EndEarlyException(string message, Exception innerException) : base(message, innerException)
        {
        }
        public EndEarlyException(string message) : base(message)
        {
        }
    }
}