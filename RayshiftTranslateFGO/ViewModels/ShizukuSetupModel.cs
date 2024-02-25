using RayshiftTranslateFGO.Services;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace RayshiftTranslateFGO.ViewModels
{
    public class ShizukuSetupViewModel : BaseViewModel
    {
        public ShizukuSetupViewModel(ICacheProvider cache) : base(cache)
        {
            OpenTutorial = new Command(async () => await Browser.OpenAsync("https://rayshift.io/shizuku-tutorial"));
            OpenDownload = new Command(async () => await Browser.OpenAsync("https://shizuku.rikka.app/download/"));
        }

        public ICommand OpenTutorial { get; }
        public ICommand OpenDownload { get; }
    }
}