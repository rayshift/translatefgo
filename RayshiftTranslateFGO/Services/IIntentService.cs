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
        public void ExitApplication();

        [ItemCanBeNull]
        public Task<WebAuthenticatorResult> LinkAccount();

        public bool TestManualLocationRequired();

        public void MakeToast(string message);
    }
}