using Android.App;
using Android.Content;
using Android.OS;
using RayshiftTranslateFGO.Services;
using Xamarin.Forms;
using Application = Xamarin.Forms.Application;

[assembly: Xamarin.Forms.Dependency(typeof(RayshiftTranslateFGO.Droid.IntentService))]
namespace RayshiftTranslateFGO.Droid
{
    public class IntentService: IIntentService
    {
        public IntentService()
        {

        }
        public void OpenDocumentTreeIntent(string what)
        {
            var activity = Forms.Context as Activity;

            var documentIntent = new Intent(Intent.ActionOpenDocumentTree);

            var externalStorageUrl = Environment.ExternalStorageDirectory.AbsolutePath;
            documentIntent.PutExtra("android.provider.extra.INITIAL_URI", externalStorageUrl);
            documentIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission |
                                    ActivityFlags.GrantPrefixUriPermission |
                                    ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(documentIntent, (int) MainActivity.RequestCodes.FolderIntentRequestCode);
        }

        public void ExitApplication()
        {
            var activity = (Activity)Forms.Context;
            activity.FinishAffinity();
        }
    }
}