using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Provider;
using Android.Util;
using AndroidX.DocumentFile.Provider;
using IO.Rayshift.Translatefgo;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using Sentry;
using Xamarin.Essentials;
using Xamarin.Forms;
using Uri = Android.Net.Uri;

[assembly: Xamarin.Forms.Dependency(typeof(RayshiftTranslateFGO.Droid.ContentManager))]
namespace RayshiftTranslateFGO.Droid
{
    public class ContentManager: IContentManager
    {
        public ContentResolver AppContentResolver { get; set; }
        public ContentManager()
        {
            var ctx = Android.App.Application.Context;
            AppContentResolver = ctx.ContentResolver;
        }

        public static string UpgradeUrl(string url, bool force = false)
        {
            bool upgradeDirectAccess = Preferences.Get("IsAccessUpgraded", 0) == 1;
            return (upgradeDirectAccess || force) ? SentryKey.UpgradeUrlKey(url) : url;
        }

        public Dictionary<string, List<FolderChildren>> _folderCache = new Dictionary<string, List<FolderChildren>>();

        public bool CheckBasicAccess()
        {
            var fsPreference = Preferences.Get("DefaultFSMode", "Default");
            if (fsPreference != "Default" && fsPreference != "Native")
            {
                return false;
            }
            try
            { 
                var ctx = Android.App.Application.Context;
                var directories = ctx.GetExternalFilesDirs("");

                if (directories != null)
                {
                    foreach (var directory in directories)
                    {
                        var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;
                        if (filesystem != null)
                        {

                            var path = filesystem.ToString();
                            var directoryContents = Directory.GetDirectories(path);
                            if (directoryContents.Length > 0)
                            {
                                return true;
                            }
                            var directoryContentsUpgrade = Directory.GetDirectories(UpgradeUrl(path, true));
                            if (directoryContentsUpgrade.Length > 0)
                            {
                                Preferences.Set("IsAccessUpgraded", 1);
                                return true;
                            }
                        }
                    }
                }
            }
            
            catch (Exception)
            {
                try
                {
                    var ctx = Android.App.Application.Context;
                    var directories = ctx.GetExternalFilesDirs("");

                    if (directories != null)
                    {
                        foreach (var directory in directories)
                        {
                            var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;
                            if (filesystem != null)
                            {

                                var path = filesystem.ToString();
                                var upgradedUrl = UpgradeUrl(path, true);
                                var directoryContentsUpgrade = Directory.GetDirectories(upgradedUrl);
                                if (directoryContentsUpgrade.Length > 0)
                                {
                                    Preferences.Set("IsAccessUpgraded", 1);
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Preferences.Set("IsAccessUpgraded", 0);
                    return false;
                }
                Preferences.Set("IsAccessUpgraded", 0);
                return false;
            }
            Preferences.Set("IsAccessUpgraded", 0);
            return false;
        }


        public async Task<bool> WriteFileContents(ContentType accessType, string filename, string storageLocationBase,
            byte[] contents, bool forceNew = false)
        {
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var fixedPath = UpgradeUrl(filename);
                    if (File.Exists(fixedPath))
                    {
                        File.Delete(fixedPath);
                    }

                    var fileHandle = File.Create(fixedPath, 4096, FileOptions.None);

                    await fileHandle.WriteAsync(contents);
                    await fileHandle.FlushAsync();
                    fileHandle.Close();
                    return true;

                case ContentType.StorageFramework:
                    await WriteFileAsync(accessType, filename, storageLocationBase, contents, forceNew);
                    return true;

                case ContentType.Shizuku:
                    NGFSError error = new NGFSError();
                    MainActivity.NextGenFS.WriteFileContents(filename, contents, error);
                    if (error.IsSuccess)
                    {
                        return true;
                    }
                    throw new Exception($"Couldn't write file via Shizuku: {error.Error}");
                    

                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        public async Task<bool> RemoveFileIfExists(ContentType accessType, string filename, string storageLocationBase)
        {
            var fileIfExists = GetPathIfFileExists(accessType, filename, storageLocationBase);
            if (!fileIfExists.Exists) return false;

            switch (accessType)
            {
                case ContentType.DirectAccess:
                    File.Delete(fileIfExists.Path);
                    await Task.Delay(50); // why is this delay here?
                    return true;

                case ContentType.StorageFramework:
                    var doc = DocumentsContract.BuildDocumentUriUsingTree(Uri.Parse(storageLocationBase), fileIfExists.Path);
                    DocumentFile file = DocumentFile.FromTreeUri(Android.App.Application.Context, doc);
                    await Task.Delay(50);
                    return file.Delete();
                case ContentType.Shizuku:
                    NGFSError error = new NGFSError();
                    MainActivity.NextGenFS.RemoveFileIfExists(filename, error);
                    if (error.IsSuccess)
                    {
                        return true;
                    }
                    throw new Exception($"Couldn't remove file via Shizuku: {error.Error}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        public void ClearCache()
        {
            _folderCache.Clear();
        }

        public async Task<FileContentsResult> GetFileContents(ContentType accessType, string filename, string storageLocationBase)
        {
            var ctx = Android.App.Application.Context;

            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var fixedPath = filename;
                    var meta = GetPathIfFileExists(accessType, fixedPath, storageLocationBase);
                    if (!meta.Exists)
                    {
                        return new FileContentsResult()
                        {
                            Error = FileErrorCode.NotExists,
                            Successful = false
                        };
                    }
                    else
                    {
                        return new FileContentsResult()
                        {
                            Error = FileErrorCode.None,
                            Successful = true,
                            FileContents = await ReadExistingFileAsync(accessType, meta.Path, storageLocationBase),
                            LastModified = meta.LastModified
                        };
                    }
                case ContentType.StorageFramework:
                    var meta2 = GetPathIfFileExists(accessType, filename, storageLocationBase);
                    if (!meta2.Exists)
                    {
                        return new FileContentsResult()
                        {
                            Error = FileErrorCode.NotExists,
                            Successful = false
                        };
                    }

                    var dataBytes = await ReadExistingFileAsync(accessType, meta2.Path, storageLocationBase);

                    return new FileContentsResult()
                    {
                        Error = FileErrorCode.None,
                        Successful = true,
                        FileContents = dataBytes,
                        LastModified = meta2.LastModified
                    };

                case ContentType.Shizuku:
                    NGFSError error = new NGFSError();
                    var metadata = GetPathIfFileExists(ContentType.Shizuku, filename, storageLocationBase);

                    if (metadata.Exists)
                    {
                        var bytes = await ReadExistingFileAsync(ContentType.Shizuku, filename, storageLocationBase);
                        return new FileContentsResult()
                        {
                            Error = FileErrorCode.None,
                            Successful = true,
                            FileContents = bytes,
                            LastModified = metadata.LastModified
                        };
                    }
                    return new FileContentsResult()
                    {
                        Error = FileErrorCode.NotExists,
                        Successful = false
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }



        private FileMetadata GetPathIfFileExists(ContentType accessType, string filename, string storageLocationBase)
        {
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var path = UpgradeUrl(filename);
                    if (File.Exists(path))
                    {
                        var lastModified = File.GetLastWriteTimeUtc(path);
                        return new FileMetadata()
                        {
                            Exists = true,
                            LastModified = ((DateTimeOffset) lastModified).ToUnixTimeMilliseconds(),
                            Path = path
                        };
                    }
                    else return new FileMetadata()
                    {
                        Exists = false
                    };

                case ContentType.StorageFramework:
                    var folderChildren = this.GetFolderChildren(Android.Net.Uri.Parse(storageLocationBase),
                        Path.GetDirectoryName(filename));

                    var fileNameOnly = Path.GetFileName(filename);

                    foreach (var file in folderChildren)
                    {
                        if (file.Path.Split("/").Last() == fileNameOnly)
                        {
                            return new FileMetadata()
                            {
                                Exists = true,
                                Path = file.Path,
                                LastModified = file.LastModified
                            };
                        }
                    }
                    return new FileMetadata()
                    {
                        Exists = false
                    };

                case ContentType.Shizuku:
                    NGFSError error = new NGFSError();
                    var fileExists = MainActivity.NextGenFS.GetFileExists(filename, error);

                    if (error.IsSuccess && !fileExists)
                    {
                        return new FileMetadata()
                        {
                            Exists = false
                        };
                    }
                    if (!error.IsSuccess)
                    {
                        throw new Exception($"Couldn't check existence of file via Shizuku: {error.Error}");
                    }

                    var modTime = MainActivity.NextGenFS.GetFileModTime(filename, error);
                    if (!error.IsSuccess)
                    {
                        throw new Exception($"Couldn't check modtime of file via Shizuku: {error.Error}");
                    }

                    return new FileMetadata()
                    {
                        Exists = true,
                        Path = filename,
                        LastModified = modTime
                    };


                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        private async Task<byte[]> ReadExistingFileAsync(ContentType accessType, string filePath, string storageLocationBase)
        {
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    return await File.ReadAllBytesAsync(filePath);
                case ContentType.StorageFramework:
                    var baseUri = Uri.Parse(storageLocationBase);
                    var contentPath = DocumentsContract.BuildDocumentUriUsingTree(baseUri, filePath);
                    var descriptor = AppContentResolver.OpenAssetFileDescriptor(contentPath!, "r");

                    if (descriptor == null)
                    {
                        throw new Exception($"File descriptor null, tried to open {contentPath}.");
                    }

                    var readStream = descriptor.CreateInputStream();
                    if (readStream == null || !readStream.CanRead)
                    {
                        throw new Exception("Cannot read the readStream.");
                    }
                    byte[] outputBuffer = new byte[readStream.Length];
                    await readStream.ReadAsync(outputBuffer);
                    return outputBuffer;
                case ContentType.Shizuku:
                    var error = new NGFSError();
                    var fileContents = MainActivity.NextGenFS.ReadExistingFile(filePath, error);
                    if (error.IsSuccess)
                    {
                        return fileContents;
                    }
                    else
                    {
                        throw new Exception($"Couldn't read file contents via Shizuku: {error.Error}");
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        private async Task<bool> WriteFileAsync(ContentType accessType, string filePath, string storageLocationBase, byte[] bytes, bool forceNew = false)
        {
            switch (accessType)
            {
                case ContentType.StorageFramework:
                    // check file exists
                    Uri contentPath;

                    if (!forceNew)
                    {
                        var fileExistPath = this.GetPathIfFileExists(accessType, filePath, storageLocationBase);

                        if (!fileExistPath.Exists)
                        {
                            var pathUri = Uri.Parse(storageLocationBase);
                            var treeId = DocumentsContract.GetTreeDocumentId(pathUri) +
                                         $"/{Path.GetDirectoryName(filePath)}";
                            var newPath = DocumentsContract.BuildDocumentUriUsingTree(pathUri, treeId);
                            DocumentFile newFile = DocumentFile.FromTreeUri(Android.App.Application.Context, newPath);
                            var documentFile =
                                newFile.CreateFile("application/octet-stream", Path.GetFileName(filePath));

                            contentPath = documentFile.Uri;
                        }
                        else
                        {
                            var baseUriParse = Uri.Parse(storageLocationBase);
                            contentPath = DocumentsContract.BuildDocumentUriUsingTree(baseUriParse, fileExistPath.Path);
                        }
                    }
                    else
                    {
                        var pathUri = Uri.Parse(storageLocationBase);
                        var treeId = DocumentsContract.GetTreeDocumentId(pathUri) +
                                     $"/{Path.GetDirectoryName(filePath)}";
                        var newPath = DocumentsContract.BuildDocumentUriUsingTree(pathUri, treeId);
                        DocumentFile newFile = DocumentFile.FromTreeUri(Android.App.Application.Context, newPath);
                        var documentFile =
                            newFile.CreateFile("application/octet-stream", Path.GetFileName(filePath));

                        contentPath = documentFile.Uri;
                    }

                    var descriptor = AppContentResolver.OpenAssetFileDescriptor(contentPath!, "w");

                    if (descriptor == null)
                    {
                        throw new Exception($"File descriptor null, tried to open {contentPath}.");
                    }

                    var writeStream = descriptor.CreateOutputStream();
                    if (writeStream == null || !writeStream.CanWrite)
                    {
                        throw new Exception("Cannot write the writeStream.");
                    }
                    await writeStream.WriteAsync(bytes);
                    return true;
                case ContentType.Shizuku:
                    var error = new NGFSError();
                    var fileContents = MainActivity.NextGenFS.WriteFileContents(filePath, bytes, error);
                    if (error.IsSuccess)
                    {
                        return fileContents;
                    }
                    else
                    {
                        throw new Exception($"Couldn't write file contents via Shizuku: {error.Error}");
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        public HashSet<InstalledFGOInstances> GetInstalledGameApps(ContentType accessType, Dictionary<string, string> storageLocations = null)
        {
            var ctx = Android.App.Application.Context;
            var apps = new HashSet<InstalledFGOInstances>();
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var directories = ctx.GetExternalFilesDirs("");

                    if (directories != null)
                    {
                        foreach (var directory in directories)
                        {
                            if (directory == null) continue;
                            var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;
                            if (filesystem != null)
                            {
                                try
                                {
                                    var path = UpgradeUrl(filesystem.ToString());
                                    var directoryContents = Directory.GetDirectories(path);
                                    foreach (var foundDirectory in directoryContents)
                                    {
                                        foreach(var validAppName in AppNames.ValidAppNames) {
                                            if (foundDirectory.Split("/").Last() == validAppName)
                                            {
                                                var region = foundDirectory.EndsWith(".en")
                                                    ? FGORegion.Na
                                                    : FGORegion.Jp;
                                                
                                                apps.Add(new InstalledFGOInstances()
                                                {
                                                    Path = foundDirectory,
                                                    Region = region
                                                });
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    continue;
                                }
                            }
                        }
                    }
                    break;

                case ContentType.StorageFramework:
                    if (storageLocations == null || storageLocations.Count == 0) return apps;

                    foreach (var location in storageLocations)
                    {
                        var package = location.Key;
                        foreach (var validAppName in AppNames.ValidAppNames)
                        {
                            if (package == validAppName)
                            {
                                var childrenFolders = this.GetFolderChildren(Android.Net.Uri.Parse(location.Value), "");
                                if (childrenFolders.Count == 0) continue; // app uninstalled

                                var region = package.EndsWith(".en")
                                    ? FGORegion.Na
                                    : FGORegion.Jp;

                                apps.Add(new InstalledFGOInstances()
                                {
                                    Path = location.Value,
                                    Region = region
                                });
                            }
                        }
                    }

                    break;
                case ContentType.Shizuku:
                    var directories2 = ctx.GetExternalFilesDirs("");

                    HashSet<string> seenDirectories = new HashSet<string>();

                    if (directories2 != null)
                    {
                        if (directories2.Length == 0)
                        {
                            Log.Error("TranslateFGO", "No external storage directories found.");
                            throw new Exception("Couldn't find any external storage directories on your device, returned none.");
                        }
                        foreach (var directory in directories2)
                        {
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (directory == null)
                            {
                                continue;
                            }

                            Log.Info("TranslateFGO", $"Checking: {directory.Path}");
                            if (seenDirectories.Contains(directory.Path))
                            {
                                continue;
                            }

                            seenDirectories.Add(directory.Path);

                            Log.Info("TranslateFGO", $"Checking path {directory.Path}");

                            var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;
                            if (filesystem != null)
                            {
                                try
                                {
                                    var path = filesystem.ToString();
                                    var directoryContents = GetShizukuDirectories(path);
                                    foreach (var foundDirectory in directoryContents)
                                    {
                                        foreach (var validAppName in AppNames.ValidAppNames)
                                        {
                                            if (foundDirectory.Split("/").Last() == validAppName)
                                            {
                                                var region = foundDirectory.EndsWith(".en")
                                                    ? FGORegion.Na
                                                    : FGORegion.Jp;

                                                apps.Add(new InstalledFGOInstances()
                                                {
                                                    Path = foundDirectory,
                                                    Region = region
                                                });
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("TranslateFGO", $"Error listing directory {filesystem.ToString()}, {ex}");
                                    throw; // this one should be thrown as it'll just return empty if the dir doesn't exist
                                }
                            }
                            else
                            {
                                Log.Info("TranslateFGO", $"Null filesystem for {directory.Path}");
                            }
                        }

                        seenDirectories.Clear();
                        foreach (var directory in directories2)
                        {
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (directory == null)
                            {
                                continue;
                            }
                            if (seenDirectories.Contains(directory.Path))
                            {
                                continue;
                            }

                            seenDirectories.Add(directory.Path);

                            var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;

                            foreach (var validAppName in AppNames.ValidAppNames)
                            {
                                if (filesystem != null)
                                {
                                    var manualCheck = Path.Combine(filesystem.ToString(), validAppName + "/");

                                    NGFSError error = new NGFSError();
                                    var contents = MainActivity.NextGenFS.ListDirectoryContents(manualCheck, error);

                                    var region = validAppName.EndsWith(".en")
                                        ? FGORegion.Na
                                        : FGORegion.Jp;

                                    if (error.IsSuccess && contents != null && contents.Length > 0 && apps.Count(w => w.Path.TrimEnd('/') == manualCheck.TrimEnd('/')) == 0)
                                    {
                                        apps.Add(new InstalledFGOInstances()
                                        {
                                            Path = manualCheck,
                                            Region = region
                                        });
                                    }
                                }
                                else
                                {
                                    Log.Info("TranslateFGO", $"Null filesystem for {directory.Path}");
                                }
                            }


                        }
                        

                    }
                    else
                    {
                        Log.Error("TranslateFGO", "No storage directories found.");
                        throw new Exception("Couldn't find any external storage directories on your device.");
                    }
                    break;
            }
            
            return apps;
        }

        List<string> GetShizukuDirectories(string path)
        {
            List<string> dirs = new List<string>();
            var error = new NGFSError();
            var directories = MainActivity.NextGenFS.ListDirectoryContents(path, error);

            if (!error.IsSuccess)
            {
                throw new Exception($"Couldn't list directories with Shizuku: {error.Error}");
            }
            foreach (var dir in directories)
            {
                var type = dir.Split("|");
                if (type[0] == "D")
                {
                    dirs.Add(type[1]);
                }
            }

            Log.Info("TranslateFGO", $"Directory listing: {path}, sizeof: {dirs.Count}");

            return dirs;
        }

        /// <summary>
        /// List contents of folder via storage access framework (used for Android/data/)
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="path">Optional path</param>
        /// <returns></returns>
        public List<FolderChildren> GetFolderChildren(Uri uri, string path)
        {

            string[] projection = {
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnLastModified
            };

            var newPath = DocumentsContract.GetTreeDocumentId(uri) + $"/{path}";
            var children = DocumentsContract.BuildChildDocumentsUriUsingTree(uri, newPath);
            List<FolderChildren> folderChildren = new List<FolderChildren>();


            //string fileSelectionQuery = null;
            //Bundle? queryArgs = new Bundle();
            /*if (fileSelection != null)
            {
                var sb = new StringBuilder();
                foreach (var _ in fileSelection)
                {
                    sb.Append("?,");
                }

                var inQuery = sb.ToString().TrimEnd(',');
                fileSelectionQuery = DocumentsContract.Document.ColumnDocumentId + $" IN ({inQuery})";
                queryArgs.PutString(ContentResolver.QueryArgSqlSelection, fileSelectionQuery);
                queryArgs.PutStringArray(ContentResolver.QueryArgSqlSelectionArgs, fileSelection);
            }*/ // https://github.com/xamarin/xamarin-android/issues/5788

            if (_folderCache.ContainsKey(children?.ToString() ?? throw new InvalidOperationException($"BuildChildDocumentsUriUsingTree returned null for {uri}, {newPath}"))) return _folderCache[children.ToString()!];

            try
            {
                var c = AppContentResolver.Query(children, projection, null, null, null);

                if (c == null) return new List<FolderChildren>(); // Return empty if the folder can't be accessed

                while (c.MoveToNext())
                {
                    var fPath = c.GetString(0);


                    /*if (fileSelection != null)
                    {
                        if (!fileSelection.Contains(fPath?.Split("/").Last())) continue;
                    }*/

                    var lastModified = c.GetString(1);
                    folderChildren.Add(new FolderChildren()
                    {
                        Path = fPath,
                        LastModified = long.Parse(lastModified!)
                    });
                }

            }
            catch (Exception ex)
            {
                Log.Warn("TranslateFGO", $"Exception on GetFolderChildren: {ex}");
                SentrySdk.CaptureException(ex);
                return new List<FolderChildren>();
            }

            _folderCache.TryAdd(children.ToString()!, folderChildren);

            return folderChildren;

        }
    }
}