using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Views;

namespace RayshiftTranslateFGO.Services
{
    public interface IScriptManager
    {
        public Task<ScriptInstallStatus> InstallScript(ContentType contentType, FGORegion region, List<string> installPaths, int installId, string assetStorageCheck = null,
            ObservableCollection<InstallerPage.TranslationGUIObject> translationGuiObjects = null);
        public Task<bool> UninstallScripts(ContentType contentType, FGORegion region, List<string> installPaths, string baseInstallPath);

        public Task<ScriptInstallStatus> InstallArt(ContentType contentType, FGORegion region,
            List<string> installPaths, List<ArtUrl> artUrls,
            ObservableCollection<ArtPage.ArtGUIObject> artGuiObjects = null, int button = 0);

        public Task GetArtAssetStorage(ContentType contentType, FGORegion region,
            List<string> installPaths, List<ArtUrl> artUrls, bool isUninstall);

    }
    public class ScriptInstallStatus
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
    }
    public class FileToWrite
    {
        public string BaseInstallPath { get; set; }
        public string FilePath { get; set; }
        public byte[] Contents { get; set; }

        public FileToWrite(string filePath, string baseInstallPath, byte[] contents)
        {
            BaseInstallPath = baseInstallPath;
            FilePath = filePath;
            Contents = contents;
        }
    }
}