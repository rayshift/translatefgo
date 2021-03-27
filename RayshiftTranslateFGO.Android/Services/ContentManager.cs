using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Support.V4.Provider;
using Android.Util;
using RayshiftTranslateFGO.Services;
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

        public static string[] ValidAppNames = new[]
        {
            "com.aniplex.fategrandorder",
            "com.aniplex.fategrandorder.en",
            "io.rayshift.betterfgo",
            "io.rayshift.betterfgo.en",
        };



        public bool CheckBasicAccess()
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
                        try
                        {
                            var path = filesystem.ToString();
                            var directoryContents = Directory.GetDirectories(path);
                            if (directoryContents.Length > 0)
                            {
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            
                        }
                    }
                }
            }

            return false;
        }


        public async Task<bool> WriteFileContents(ContentType accessType, string filename, string storageLocationBase,
            byte[] contents)
        {
            var ctx = Android.App.Application.Context;

            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var fixedPath = filename;
                    await File.WriteAllBytesAsync(fixedPath, contents);
                    return true;

                case ContentType.StorageFramework:
                    await WriteFileAsync(accessType, filename, storageLocationBase, contents);
                    return true;

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
                    await Task.Delay(50);
                    return true;

                case ContentType.StorageFramework:
                    var doc = DocumentsContract.BuildDocumentUriUsingTree(Uri.Parse(storageLocationBase), fileIfExists.Path);
                    DocumentFile file = DocumentFile.FromTreeUri(Android.App.Application.Context, doc);
                    await Task.Delay(50);
                    return file.Delete();
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
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
                            FileContents = await File.ReadAllBytesAsync(fixedPath),
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }



        private FileMetadata GetPathIfFileExists(ContentType accessType, string filename, string storageLocationBase)
        {
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    var path = filename;
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
                    else return new FileMetadata();

                case ContentType.StorageFramework:
                    var fileNameOnly = Path.GetFileName(filename);

                    var folderChildren = this.GetFolderChildren(Android.Net.Uri.Parse(storageLocationBase),
                        Path.GetDirectoryName(filename), new string[] {fileNameOnly});


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
                    return new FileMetadata();

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
                    if (!readStream.CanRead)
                    {
                        throw new Exception("Cannot read the readStream.");
                    }
                    byte[] outputBuffer = new byte[readStream.Length];
                    await readStream.ReadAsync(outputBuffer);
                    return outputBuffer;
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        private async Task<bool> WriteFileAsync(ContentType accessType, string filePath, string storageLocationBase, byte[] bytes)
        {
            switch (accessType)
            {
                case ContentType.DirectAccess:
                    await File.WriteAllBytesAsync(filePath, bytes);
                    return true;
                case ContentType.StorageFramework:
                    // check file exists

                    var fileExistPath = this.GetPathIfFileExists(accessType, filePath, storageLocationBase);

                    Uri contentPath;
                    if (!fileExistPath.Exists)
                    {
                        var pathUri = Uri.Parse(storageLocationBase);
                        //var treeId = DocumentsContract.GetTreeDocumentId(pathUri) + $"/{filePath}";
                        var treeId = DocumentsContract.GetTreeDocumentId(pathUri) + $"/{Path.GetDirectoryName(filePath)}";
                        var newPath = DocumentsContract.BuildDocumentUriUsingTree(pathUri, treeId);
                        DocumentFile newFile = DocumentFile.FromTreeUri(Android.App.Application.Context, newPath);
                        var documentFile = newFile.CreateFile("application/octet-stream", Path.GetFileName(filePath));

                        contentPath = documentFile.Uri;
                    }
                    else
                    {
                        var baseUriParse = Uri.Parse(storageLocationBase);
                        contentPath = DocumentsContract.BuildDocumentUriUsingTree(baseUriParse, fileExistPath.Path);
                    }

                    var descriptor = AppContentResolver.OpenAssetFileDescriptor(contentPath!, "w");

                    if (descriptor == null)
                    {
                        throw new Exception($"File descriptor null, tried to open {contentPath}.");
                    }

                    var writeStream = descriptor.CreateOutputStream();
                    if (!writeStream.CanWrite)
                    {
                        throw new Exception("Cannot write the writeStream.");
                    }
                    await writeStream.WriteAsync(bytes);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
            }
        }

        public HashSet<InstalledFGOInstances> GetInstalledGameApps(ContentType accessType, string storageLocation = null)
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
                            var filesystem = new DirectoryInfo(directory.AbsolutePath)?.Parent?.Parent;
                            if (filesystem != null)
                            {
                                try
                                {
                                    var path = filesystem.ToString();
                                    var directoryContents = Directory.GetDirectories(path);
                                    foreach (var foundDirectory in directoryContents)
                                    {
                                        foreach(var validAppName in ValidAppNames) {
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

                                }
                            }
                        }
                    }
                    break;

                case ContentType.StorageFramework:
                    if (string.IsNullOrEmpty(storageLocation)) return apps;

                    var folders = this.GetFolderChildren(Android.Net.Uri.Parse(storageLocation), "data/");

                    if (folders.Count == 0) return apps;

                    foreach (var folder in folders)
                    {
                        var package = folder.Path.Split("/").Last();
                        foreach (var validAppName in ValidAppNames)
                        {
                            if (package == validAppName)
                            {
                                var region = package.EndsWith(".en")
                                    ? FGORegion.Na
                                    : FGORegion.Jp;


                                apps.Add(new InstalledFGOInstances()
                                {
                                    Path = package,
                                    Region = region
                                });
                            }
                        }

                    }
                    break;
            }

            return apps;
        }

        /// <summary>
        /// List contents of folder via storage access framework (used for Android/data/)
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="path">Optional path</param>
        /// <param name="fileSelection"></param>
        /// <returns></returns>
        public List<FolderChildren> GetFolderChildren(Uri uri, string path, string[] fileSelection = null)
        {

            string[] projection = {
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnLastModified
            };

            var newPath = DocumentsContract.GetTreeDocumentId(uri) + $"/{path}";
            var children = DocumentsContract.BuildChildDocumentsUriUsingTree(uri, newPath);
            List<FolderChildren> folderChildren = new List<FolderChildren>();

            string fileSelectionQuery = null;
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

            try
            {
                //var c = string.IsNullOrEmpty(fileSelectionQuery) ? 
                    //AppContentResolver.Query(children, projection, null, null, null) : 
                    //AppContentResolver.Query(children, projection, queryArgs, new CancellationSignal());
                    var c = AppContentResolver.Query(children, projection, null, null, null);


                if (c == null) return new List<FolderChildren>(); // Return empty if the folder can't be accessed
                
                while (c.MoveToNext())
                {
                    var fPath = c.GetString(0);

                    if (fileSelection != null)
                    {
                        if (!fileSelection.Contains(fPath?.Split("/").Last())) continue;
                    }

                    var lastModified = c.GetString(1);
                    folderChildren.Add(new FolderChildren()
                    {
                        Path = fPath,
                        LastModified = long.Parse(lastModified)
                    });
                }

            }
            catch (Exception ex)
            {
                Log.Warn("TranslateFGO", $"Exception on GetFolderChildren: {ex}");
                return new List<FolderChildren>();
            }

            return folderChildren;

        }
    }
}