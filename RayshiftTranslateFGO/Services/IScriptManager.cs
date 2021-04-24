using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using RayshiftTranslateFGO.Views;

namespace RayshiftTranslateFGO.Services
{
    public interface IScriptManager
    {
        public Task<ScriptInstallStatus> InstallScript(ContentType contentType, FGORegion region, List<string> installPaths, string baseInstallPath, int installId, string assetStorageCheck = null,
            ObservableCollection<InstallerPage.TranslationGUIObject> translationGuiObjects = null);
        public Task<bool> UninstallScripts(ContentType contentType, FGORegion region, List<string> installPaths, string baseInstallPath);

    }
    public class ScriptInstallStatus
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
    }
}