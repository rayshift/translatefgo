using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Android.Content.PM;
using Java.Util;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Annotations;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.ViewModels;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PreInitializePage : ContentPage
    {
        protected IList<ApplicationInfo> InstalledApps { get; set; }
        public readonly ObservableCollection<FGOInstalledApp> GuiObjects = new ObservableCollection<FGOInstalledApp>();
        public bool WarnAboutFolder = false;
        public PreInitializePage()
        {
            //var documentsAppVer = DependencyService.Get<IIntentService>().GetDocumentsUiVersion(); not reliable, keeping unused

            var locationJson = Preferences.Get("StorageLocations", "{}");
            var locations = JsonConvert.DeserializeObject<Dictionary<string, string>>(locationJson);

            InstalledApps = DependencyService.Get<IIntentService>().GetInstalledApps();

            foreach (var app in InstalledApps)
            {
                if (AppNames.ValidAppNames.Contains(app.ProcessName))
                {
                    // var storage = app.StorageUuid; broken!

                    var newApp = new FGOInstalledApp()
                    {
                        AppName = app.ProcessName,
                        Name = AppNames.AppDescriptions[app.ProcessName],
                        ButtonPreconfigureText = $"{AppNames.AppDescriptions[app.ProcessName]}",
                        ButtonClick = new Command(() => AddFolderButtonOnClicked(app.ProcessName)),
                        ButtonEnabled = !locations.ContainsKey(app.ProcessName)
                    };

                    GuiObjects.Add(newApp);
                }
            }
            InitializeComponent();
            BindingContext = App.GetViewModel<PreInitializeViewModel>();
            BindableLayout.SetItemsSource(PreInitializeInstallView, GuiObjects);

            ReturnHomeButton.Command = new Command(async () => await ReturnToMainPage());
            ShizukuSetupButton.Command = new Command(async () => await ShizukuSetupInstead());
            NavigationPage.SetHasNavigationBar(this, false);
            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page",  (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await ReturnToMainPage());
            });

            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "install_locations_updated",
                (sender) =>
                {
                    UpdateRemainingItems();
                });

            WarnAboutFolder = DependencyService.Get<IIntentService>().TestManualLocationRequired();

        }

        private void UpdateRemainingItems()
        {
            var locationJson = Preferences.Get("StorageLocations", "{}");
            var locations = JsonConvert.DeserializeObject<Dictionary<string, string>>(locationJson);

            bool allSeen = true;
            foreach (var app in GuiObjects)
            {
                if (locations.ContainsKey(app.AppName))
                {
                    app.ButtonEnabled = false;
                }
                else
                {
                    allSeen = false;
                    app.ButtonEnabled = true;
                }
            }

            if (allSeen)
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "return_to_main_page");
            }
        }

        private async Task AddFolderButtonOnClicked(string processName)
        {
            if (WarnAboutFolder)
            {
                if (await DisplayAlert(AppResources.Warning,
                        String.Format(AppResources.BluestacksWarning, $"Android/data/{processName}"), AppResources.OK,
                        AppResources.Cancel))
                {
                    App.GetViewModel<PreInitializeViewModel>().Cache.Set("TemporaryProcessName", processName);
                    DependencyService.Get<IIntentService>().OpenDocumentTreeIntent("", $"data%2F{processName}%2F");
                }
            }
            else
            {
                App.GetViewModel<PreInitializeViewModel>().Cache.Set("TemporaryProcessName", processName);
                DependencyService.Get<IIntentService>().OpenDocumentTreeIntent("", $"data%2F{processName}%2F");
            }
        }

        public void Unsubscribe()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page");
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "install_locations_updated");
        }

        public async Task ShizukuSetupInstead()
        {
            //ShizukuSetupButton.IsEnabled = false;
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O) // api 26
            {
                IReadOnlyList<Page> navStack = Navigation.NavigationStack;
                Unsubscribe();

                if (navStack.Count == 0)
                {
                    await Navigation.PushAsync( new ShizukuSetupPage(true));
                    await Navigation.PopToRootAsync(true);
                }
                else
                {
                    Navigation.InsertPageBefore( new ShizukuSetupPage(false), this);
                    await Navigation.PopAsync(true);
                }
            }
            else
            {
                var intentService = DependencyService.Get<IIntentService>();
                intentService.MakeToast(AppResources.ShizukuTooLowAndroidVersion);
            }
        }

        public async Task ReturnToMainPage()
        {
            ReturnHomeButton.IsEnabled = false;
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "reset_initial_load");
            Unsubscribe();
            //await Task.Delay(1500);
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
    }

    public class FGOInstalledApp: INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string AppName { get; set; }
        public string ButtonPreconfigureText { get; set; }
        public Command ButtonClick { get; set; }

        public bool ButtonEnabled
        {
            get => _buttonEnabled;
            set
            {
                _buttonEnabled = value;
                RaisePropertyChanged(nameof(ButtonEnabled));
            }
        }

        private bool _buttonEnabled;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}