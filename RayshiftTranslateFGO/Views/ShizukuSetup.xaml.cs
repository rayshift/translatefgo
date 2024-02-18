using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ShizukuSetupPage : ContentPage
    {
        public ShizukuSetupPage(bool needToLoadMain = false)
        {
            InitializeComponent();
            _needToLoadPage = needToLoadMain;
            BindingContext = App.GetViewModel<ShizukuSetupViewModel>();

            NavigationPage.SetHasNavigationBar(this, false);
            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page_shizuku", (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await ReturnToMainPage());
            });

            ShizukuCheckButton.Command = new Command(async () => await ShizukuCheck());
        }

        private bool _needToLoadPage = false;

        public void Unsubscribe()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page_shizuku");
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "shizuku_bound");
        }

        public async Task ReturnToMainPage()
        {
            Unsubscribe();
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "return_to_main_page");
            IReadOnlyList<Page> navStack = Navigation.NavigationStack;


            if (navStack.Count == 0)
            {
                await Navigation.PushAsync(new MainPage());
                await Navigation.PopToRootAsync(true);
            }
            else
            {
                Navigation.InsertPageBefore(new MainPage(), this);
                await Navigation.PopAsync(true);
            }
            
        }

        public async Task ShizukuCheck()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "shizuku_bound");
            var intentService = DependencyService.Get<IIntentService>();

            if (!intentService.IsShizukuAvailable())
            {
                intentService.MakeToast(AppResources.ShizukuNotInstalled);
                return;
            }

            if (intentService.IsShizukuServiceBound())
            {
                intentService.MakeToast(AppResources.Android11SetupSuccessful);
                Preferences.Set("UseShizuku", true);
                await ReturnToMainPage();
                return;
            }

            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "shizuku_bound", (sender) =>
            {
                intentService.MakeToast(AppResources.Android11SetupSuccessful);
                Preferences.Set("UseShizuku", true);
                Device.BeginInvokeOnMainThread(async () => await ReturnToMainPage());
            });

            intentService.CheckShizukuPerm(true);

        }
    }
}