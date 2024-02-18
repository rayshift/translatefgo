using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Content.PM;
using Android.OS.Storage;
using RayshiftTranslateFGO.Annotations;
using Xamarin.Essentials;

namespace RayshiftTranslateFGO.Services
{
    public interface IIntentService
    {
        public void OpenDocumentTreeIntent(string what, string append = null);
        public IList<StorageVolume> GetStorageVolumes();
        public IList<ApplicationInfo> GetInstalledApps();
        public long GetDocumentsUiVersion();
        public void ExitApplication();

        public bool IsShizukuAvailable();
        public bool IsShizukuServiceBound();
        public bool CheckShizukuPerm(bool andBind = false);

        [ItemCanBeNull]
        public Task<WebAuthenticatorResult> LinkAccount();

        public bool TestManualLocationRequired();

        public void MakeToast(string message);
    }
}