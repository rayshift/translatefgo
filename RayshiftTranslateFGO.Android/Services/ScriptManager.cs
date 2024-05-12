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

        // only being used for uninstall atm "temporary" but actually perm
        public async Task GetArtAssetStorage(ContentType contentType, FGORegion region,
            List<string> installPaths, List<ArtUrl> artUrls, bool isUninstall)
        {
            _cm.ClearCache();
            var ftw = new List<FileToWrite>();
            var restful = new RestfulAPI();
            foreach (var game in installPaths)
            {
                var assetStoragePath = contentType == ContentType.StorageFramework
                    ? $"files/data/d713/{InstallerPage._assetList}"
                    : $"{game}/files/data/d713/{InstallerPage._assetList}";

                var fileContents = await _cm.GetFileContents(contentType, assetStoragePath, game);

                if (!fileContents.Successful || fileContents.FileContents.Length == 0)
                {
                    return;
                }


                // remove bom
                var base64 = "";
                await using var inputStream = new MemoryStream(fileContents.FileContents);
                using (var reader = new StreamReader(inputStream, Encoding.ASCII))
                {
                    base64 = await reader.ReadToEndAsync();
                }

                var svtIds = artUrls.SelectMany(s => s.ServantIDs).ToList();
                var newAssetList = await restful.SendAssetList(base64, svtIds, region, true);

                if (!newAssetList.IsSuccessful)
                {
                    return;
                }

                // add bom
                await using var outputStream = new MemoryStream();
                await using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
                {
                    await writer.WriteAsync(newAssetList.Data.Response["data"]);
                }

                // for later
                ftw.Add(new FileToWrite(assetStoragePath, game, outputStream.ToArray()));
            }

            foreach (var assetStorageFile in ftw)
            {
                // write assetstorage
                await _cm.RemoveFileIfExists(contentType,
                    assetStorageFile.FilePath, assetStorageFile.BaseInstallPath);
                await _cm.WriteFileContents(contentType, assetStorageFile.FilePath, assetStorageFile.BaseInstallPath,
                    assetStorageFile.Contents, true);
            }
        }
        public async Task<ScriptInstallStatus> InstallArt(ContentType contentType, FGORegion region,
            List<string> installPaths, List<ArtUrl> artUrls, ObservableCollection<ArtPage.ArtGUIObject> artGuiObjects = null, int button = 0)
        {
            _cm.ClearCache();
            var gui = artGuiObjects != null;
            var guiObject = gui ? artGuiObjects.First(w => w.Region == region) : null;
            if (guiObject != null)
            {
                guiObject.Status =
                    UIFunctions.GetResourceString("InstallingFetchingHandshake");
                if (button == 1)
                {
                    guiObject.Button1InstallText = UIFunctions.GetResourceString("InstallingText");
                }
                else if (button == 2)
                {
                    guiObject.Button2InstallText = UIFunctions.GetResourceString("InstallingText");
                }

                guiObject.TextColor = Color.Chocolate;
            }

            var restful = new RestfulAPI();

            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallDownloadNewAssetStorage"));
            }

            ConcurrentDictionary<string, byte[]> fileCache = new ConcurrentDictionary<string, byte[]>();

            var arts = artUrls.SelectMany(s => s.Urls).ToList();
            var totalArts = arts.Count;

            // get new assetstorage.txt
            foreach (var game in installPaths)
            {
                var assetStoragePath = contentType == ContentType.StorageFramework ? $"files/data/d713/{InstallerPage._assetList}"
                    : $"{game}/files/data/d713/{InstallerPage._assetList}";

                var fileContents = await _cm.GetFileContents(contentType, assetStoragePath, game);

                if (!fileContents.Successful || fileContents.FileContents.Length == 0)
                {
                    return new ScriptInstallStatus()
                    {
                        IsSuccessful = false,
                        ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallEmptyAssetStorage"), "art", assetStoragePath, fileContents.Error)
                    };
                }


                // remove bom
                var base64 = "";
                await using var inputStream = new MemoryStream(fileContents.FileContents);
                using (var reader = new StreamReader(inputStream, Encoding.ASCII))
                {
                    base64 = await reader.ReadToEndAsync();
                }

                var svtIds = artUrls.SelectMany(s => s.ServantIDs).ToList();
                var newAssetList = await restful.SendAssetList(base64, svtIds, region);

                if (!newAssetList.IsSuccessful)
                {
                    return new ScriptInstallStatus()
                    {
                        IsSuccessful = false,
                        ErrorMessage = string.Format(UIFunctions.GetResourceString("InstallAssetStorageAPIFailure"), "art", newAssetList.StatusCode, newAssetList.Data?.Message)
                    };
                }

                // add bom
                await using var outputStream = new MemoryStream();
                await using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
                {
                    await writer.WriteAsync(newAssetList.Data.Response["data"]);
                }

                // for later
                var assetStorageFile = new FileToWrite(assetStoragePath, game, outputStream.ToArray());

                // download each art

                if (guiObject != null)
                {
                    guiObject.Status =
                        String.Format(UIFunctions.GetResourceString("InstallDownloadingArts"), 1, totalArts);
                }

                int i = 0;
                object lockObj = new Object();

                // assuming writing can be async
                var maxParallel = contentType == ContentType.Shizuku ? 1 : 4;
                try
                {
                    await arts.ParallelForEachAsync(async art =>
                    {
                        var filePath = contentType == ContentType.StorageFramework ? $"files/data/d713/{art.Filename}"
                            : $"{game}/files/data/d713/{art.Filename}";
                        var downloadUrl = art.Url;
                        byte[] artData;
                        if (!fileCache.ContainsKey(downloadUrl))
                        {
                            var downloadResponse = await restful.GetScript(downloadUrl, true);
                            var downloadedArt = downloadResponse.RawBytes;

                            if (!downloadResponse.IsSuccessful)
                            {
                                throw new EndEarlyException(String.Format(
                                    UIFunctions.GetResourceString("InstallScriptDownloadFailure"),
                                    "art", downloadUrl, downloadResponse.ErrorMessage));
                            }

                            if (downloadedArt == null)
                            {
                                throw new EndEarlyException(String.Format(
                                    UIFunctions.GetResourceString("InstallEmptyFileFailure"),
                                    "art", downloadUrl));
                            }

                            fileCache.TryAdd(downloadUrl, downloadedArt);
                            artData = downloadedArt;
                        }
                        else
                        {
                            artData = fileCache[downloadUrl];
                        }

                        var file = new FileToWrite(filePath, game, artData);

                        // write
                        if (file.FilePath.EndsWith(".bin"))
                        {
                            await _cm.RemoveFileIfExists(contentType,
                                file.FilePath, file.BaseInstallPath);
                        }

                        await _cm.WriteFileContents(contentType, file.FilePath, file.BaseInstallPath, file.Contents, true);

                        i++;
                        lock (lockObj)
                        {
                            if (guiObject != null)
                            {
                                guiObject.Status =
                                    String.Format(UIFunctions.GetResourceString("InstallDownloadingArts"),
                                        Math.Min(i, totalArts), totalArts);
                            }
                        }
                    }, maxDegreeOfParallelism: maxParallel);
                }
                catch (EndEarlyException ex)
                {
                    return new ScriptInstallStatus()
                    {
                        IsSuccessful = false,
                        ErrorMessage = ex.ToString()
                    };
                }

                // write assetstorage
                await _cm.RemoveFileIfExists(contentType,
                    assetStorageFile.FilePath, assetStorageFile.BaseInstallPath);
                await _cm.WriteFileContents(contentType, assetStorageFile.FilePath, assetStorageFile.BaseInstallPath, assetStorageFile.Contents, true);
            }

            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallFinished"));
                guiObject.TextColor = Color.LimeGreen;
            }

            // Write checksum cache
            var prefKey = region == FGORegion.Jp ? "JPArtChecksums" : "NAArtChecksums";
            var chkPref = Preferences.Get(prefKey, "[]");

            var checksums = JsonConvert.DeserializeObject<List<string>>(chkPref);
            if (checksums == null) checksums = new List<string>();

            checksums.AddRange(arts.Select(s => s.Hash));

            var chkRewrite = JsonConvert.SerializeObject(checksums);
            Preferences.Set(prefKey, chkRewrite);

            return new ScriptInstallStatus()
            {
                IsSuccessful = true,
                ErrorMessage = ""
            };

        }

        public async Task<ScriptInstallStatus> InstallScript(ContentType contentType, FGORegion region, List<string> installPaths, int installId, string assetStorageCheck = null,
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

            Task<AsyncUploader.ExtraAssetReturn> extraFileAwaitTask = null;
            // get new extra files - do first as this takes longest to process

            int totalStages = 3;
            // upload in the background
            AsyncUploader uploader = new AsyncUploader();

            if (groupToInstall.HasExtraStage)
            {
                var buffer = new MemoryStream();
                var writer = new BinaryWriter(buffer);
                // File format: int32 install path count, int32 total file count, null-terminated string path, int32 length of data, byte[] data
                writer.Write(installPaths.Count);
                foreach (var game in installPaths)
                {
                    // pack
                    writer.Write(groupToInstall.ExtraStages.Count);
                    foreach (var path in groupToInstall.ExtraStages)
                    {

                        var directPath = contentType == ContentType.StorageFramework
                            ? $"files/data/{path}"
                            : $"{game}/files/data/{path}";

                        var fileContents = await _cm.GetFileContents(contentType, directPath, game);

                        if (!fileContents.Successful || fileContents.FileContents.Length == 0)
                        {
                            if (fileContents.Error == FileErrorCode.NotExists)
                            {
                                return new ScriptInstallStatus()
                                {
                                    IsSuccessful = false,
                                    ErrorMessage = UIFunctions.GetResourceString("InstallMissingExtraFile")
                                };
                            }
                            return new ScriptInstallStatus()
                            {
                                IsSuccessful = false,
                                ErrorMessage = String.Format(UIFunctions.GetResourceString("InstallEmptyExtraFile"), installId, directPath, fileContents.Error)
                            };
                        }
                        writer.Write(path);
                        writer.Write(fileContents.FileContents.Length);
                        writer.Write(fileContents.FileContents);
                    }
                }
                writer.Flush();



                if (Guid.TryParse(Preferences.Get(EndpointURL.GetLinkedAccountKey(), null), out var userToken) || !groupToInstall.IsDonorOnly)
                {
                    extraFileAwaitTask = uploader.GetExtraAssets(buffer, userToken, installId, region);
                }
                else
                {
                    throw new EndEarlyException("No user token found in storage.");
                }
                
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
                },maxDegreeOfParallelism: 4);
            }
            catch (EndEarlyException ex)
            {
                return new ScriptInstallStatus()
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.ToString()
                };
            }


            //Dictionary<string, Tuple<string, byte[]>> filesToWrite = new Dictionary<string, Tuple<string, byte[]>>();
            List<FileToWrite> filesToWrite = new List<FileToWrite>();
            // process returned extra files
            if (groupToInstall.HasExtraStage && extraFileAwaitTask != null)
            {
                if (guiObject != null)
                {
                    guiObject.Status =
                        String.Format(UIFunctions.GetResourceString("InstallExtraFiles"), uploader.Stage, totalStages, uploader.Percent);
                }

                while (!extraFileAwaitTask.IsCompleted)
                {
                    if (guiObject != null)
                    {
                        guiObject.Status =
                            String.Format(UIFunctions.GetResourceString("InstallExtraFiles"), uploader.Stage, totalStages, uploader.Percent);
                    }

                    await Task.Delay(100);
                }
                var extraResult = await extraFileAwaitTask;

                if (!extraResult.IsSuccessful)
                {
                    return extraResult;
                }

                var bytes = extraResult.Data;
                await using var outputStream = new MemoryStream(bytes);
                using var reader = new BinaryReader(outputStream);

                var totalPaths = reader.ReadInt32();

                for (int i = 0; i < totalPaths; i++)
                {
                    var totalFiles = reader.ReadInt32();
                    for (int k = 0; k < totalFiles; k++)
                    {
                        var pathToWrite = reader.ReadString();
                        var dataLength = reader.ReadInt32();
                        var data = reader.ReadBytes(dataLength);

                        var game = installPaths[i];

                        var directPath = contentType == ContentType.StorageFramework
                            ? $"files/data/{pathToWrite}"
                            : $"{game}/files/data/{pathToWrite}";

                        filesToWrite.Add(new FileToWrite(directPath, game, data));
                    }
                }
                
            }

            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallDownloadNewAssetStorage"));
            }

            // get new assetstorage.txt
            foreach (var game in installPaths)
            {
                var assetStoragePath = contentType == ContentType.StorageFramework ? $"files/data/d713/{InstallerPage._assetList}"
                    : $"{game}/files/data/d713/{InstallerPage._assetList}";

                var fileContents = await _cm.GetFileContents(contentType, assetStoragePath, game);

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

                filesToWrite.Add(new FileToWrite(assetStoragePath, game, outputStream.ToArray()));
            }

            // prepare files

            foreach (var game in installPaths)
            {
                foreach (var asset in scriptDictionary)
                {
                    var assetInstallPath = contentType == ContentType.StorageFramework
                        ? $"files/data/d713/{asset.Key}"
                        : $"{game}/files/data/d713/{asset.Key}";

                    filesToWrite.Add(new FileToWrite(assetInstallPath, game, asset.Value));
                }
            }

            // write files
            int j = 1;
            int tot = filesToWrite.Count;
            foreach (var file in filesToWrite)
            {

                if (file.FilePath.EndsWith(".bin"))
                {
                    await _cm.RemoveFileIfExists(contentType,
                        file.FilePath, file.BaseInstallPath);
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

                await _cm.WriteFileContents(contentType, file.FilePath, file.BaseInstallPath, file.Contents);

                j += 1;
            }


            if (guiObject != null)
            {
                guiObject.Status =
                    String.Format(UIFunctions.GetResourceString("InstallFinished"));
                guiObject.TextColor = Color.LimeGreen;
            }

            if (!groupToInstall.HasExtraStage)
            {
                Preferences.Set($"InstalledScript_{region}", JsonConvert.SerializeObject(groupToInstall));
            }

            if (groupToInstall.HasExtraStage)
            {
                var pref = Preferences.Get($"UninstallPurgesExtras_{region}", "[]");
                var existingExtras = JsonConvert.DeserializeObject<List<string>>(pref);
                if (existingExtras == null) existingExtras = new List<string>();
                foreach (var extra in groupToInstall.ExtraStages)
                {
                    if (!existingExtras.Contains(extra))
                    {
                        existingExtras.Add(extra);
                    }
                }

                var extraSave = JsonConvert.SerializeObject(existingExtras);
                Preferences.Set($"UninstallPurgesExtras_{region}", extraSave);
            }
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
}