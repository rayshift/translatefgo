using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Uri = Android.Net.Uri;

namespace RayshiftTranslateFGO.Services
{
    public interface IContentManager
    {
        /// <summary>
        /// Get children of a folder
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<FolderChildren> GetFolderChildren(Uri uri, string path);

        /// <summary>
        /// Check to see if we can access the old way (directly)
        /// </summary>
        /// <returns></returns>
        public bool CheckBasicAccess();

        /// <summary>
        /// Find installed FGO instances that can be accessed
        /// </summary>
        /// <param name="accessType"></param>
        /// <param name="storageLocations"></param>
        /// <returns></returns>
        public HashSet<InstalledFGOInstances> GetInstalledGameApps(ContentType accessType, Dictionary<string, string> storageLocations = null);

        /// <summary>
        /// Get file contents
        /// </summary>
        /// <param name="accessType"></param>
        /// <param name="filename"></param>
        /// <param name="storageLocationBase"></param>
        /// <returns></returns>
        public Task<FileContentsResult> GetFileContents(ContentType accessType, string filename,
            string storageLocationBase);

        /// <summary>
        /// Write file contents
        /// </summary>
        /// <param name="accessType"></param>
        /// <param name="filename"></param>
        /// <param name="storageLocationBase"></param>
        /// <param name="contents"></param>
        /// <param name="forceNew">Always create new file, will throw error if file exists</param>
        /// <returns></returns>
        public Task<bool> WriteFileContents(ContentType accessType, string filename, string storageLocationBase,
            byte[] contents, bool forceNew = false);

        /// <summary>
        /// Remove file if it exists
        /// </summary>
        /// <param name="accessType"></param>
        /// <param name="filename"></param>
        /// <param name="storageLocationBase"></param>
        /// <returns></returns>
        public Task<bool> RemoveFileIfExists(ContentType accessType, string filename, string storageLocationBase);

        /// <summary>
        /// Clear cache
        /// </summary>
        public void ClearCache();
    }

    public class InstalledFGOInstances
    {
        public long LastModified { get; set; } = 0;
        public string Path { get; set; }
        public FGORegion Region { get; set; }
        public string AssetStorage { get; set; }
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
        StorageFramework,
        Shizuku
    }
    [Flags]
    public enum FGORegion
    {
        Jp = 0x1,
        Na = 0x2,
        Debug = 0x4
    }
}