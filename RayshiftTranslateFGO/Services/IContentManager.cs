using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Uri = Android.Net.Uri;

namespace RayshiftTranslateFGO.Services
{
    public interface IContentManager
    {
        public List<FolderChildren> GetFolderChildren(Uri uri, string path);
        public bool CheckBasicAccess();
        public HashSet<InstalledFGOInstances> GetInstalledGameApps(ContentType accessType, string storageLocation = null);

        public Task<FileContentsResult> GetFileContents(ContentType accessType, string filename,
            string storageLocationBase);

        public Task<bool> WriteFileContents(ContentType accessType, string filename, string storageLocationBase,
            byte[] contents);

        public Task<bool> RemoveFileIfExists(ContentType accessType, string filename, string storageLocationBase);

        public void ClearCache();
    }

    public class InstalledFGOInstances
    {
        public long LastModified { get; set; } = 0;
        public string Path { get; set; }
        public FGORegion Region { get; set; }
    }
    public class FolderChildren
    {
        public string Path { get; set; }
        public long LastModified { get; set; }
    }

    public class FileContentsResult
    {
        public bool Successful { get; set; }
        public FileErrorCode Error { get; set; }
        public byte[] FileContents { get; set; }
        public long LastModified { get; set; } = 0;
    }

    public class FileMetadata
    {
        public bool Exists { get; set; } = false;
        public string Path { get; set; }
        public long LastModified { get; set; } = 0;
    }

    public enum FileErrorCode
    {
        None = 0,
        NotExists,
        UnknownError=65536

    }
    public enum ContentType
    {
        DirectAccess,
        StorageFramework
    }
    [Flags]
    public enum FGORegion
    {
        Jp = 0x1,
        Na = 0x2,
        Debug = 0x4
    }
}