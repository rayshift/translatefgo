using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SetupPage : ContentPage
    {
        public SetupPage()
        {

            var language = Preferences.Get("Language", "en-US");
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);  
            SetupButton.Clicked += SetupButtonOnClicked;
            switch (language)
            {
                case "en-US":
                    LanguageEnglish.IsChecked = true;
                    break;
                case "es":
                    LanguageSpanish.IsChecked = true;
                    break;
                case "fr":
                    LanguageFrench.IsChecked = true;
                    break;
                case "id":
                    LanguageIndonesian.IsChecked = true;
                    break;
                case "pt-BR":
                    LanguagePtBr.IsChecked = true;
                    break;
                case "zh-CN":
                    LanguageSimplifiedChinese.IsChecked = true;
                    break;
            }


        }

        private void SetupButtonOnClicked(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await PerformSetup();
            });
        }

        private void Language_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value && sender is RadioButton button)
            {
                UIFunctions.SetLocale((string) button.Value);
                SetupButton.Text = AppResources.SetupButton;
                SelectLanguageLabel.Text = AppResources.SelectLanguage;
            }
        }

        public async Task PerformSetupPost()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "manage_ext_storage_listen");

            var dep = DependencyService.Get<IIntentService>();
            var requestedAnd11Acc = dep.IsExternalStorageManager();
            if (requestedAnd11Acc)
            {
                Preferences.Set("SetupV2", true);
                Navigation.InsertPageBefore(new MainPage(), this);
                await Navigation.PopAsync(true);
            }
            else
            {
                var retry = await DisplayAlert(AppResources.DirectoryPermissionDeniedTitle,
                    AppResources.DirectoryPermissionDeniedBody, AppResources.Yes, AppResources.No);
                if (retry)
                {
                    await PerformSetup();
                }
                else
                {
                    await DisplayAlert(AppResources.SetupFailedTitle,
                        AppResources.SetupFailedBody + "No storage write access granted. The app may still work with Shizuku. Restart the app to try again.", AppResources.OK);
                    Preferences.Set("SetupV2", true);
                    Navigation.InsertPageBefore(new MainPage(), this);
                    await Navigation.PopAsync(true);
                }
            }
        }

        public async Task PerformSetup()
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var dep = DependencyService.Get<IIntentService>();
                var requestedAnd11Acc = dep.IsExternalStorageManager();

                if (requestedAnd11Acc)
                {
                    await PerformSetupPost();
                    return;
                }
                else
                {
                    dep.OpenExternalStoragePage();
                }

                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "manage_ext_storage_listen",
                    async (sender) => { Device.BeginInvokeOnMainThread(async () => await PerformSetupPost()); });
            }
            else
            {
                var statusRead = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                var statusWrite = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (statusRead != PermissionStatus.Granted || statusWrite != PermissionStatus.Granted)
                {
                    await ShowMessage(AppResources.DirectoryPermissionAccessTitle,
                        AppResources.DirectoryPermissionAccessBody, AppResources.OK,
                        async () =>
                        {

                            while (true)
                            {
                                var requestedWrite = await Permissions.RequestAsync<Permissions.StorageWrite>();
                                if (requestedWrite != PermissionStatus.Granted)
                                {
                                    var retry = await DisplayAlert(AppResources.DirectoryPermissionDeniedTitle,
                                        AppResources.DirectoryPermissionDeniedBody, AppResources.Yes, AppResources.No);
                                    if (!retry)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                await Permissions.RequestAsync<Permissions.StorageRead>();

                                break;
                            }

                            var statusWriteSecondCheck = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                            if (statusWriteSecondCheck != PermissionStatus.Granted)
                            {
                                await DisplayAlert(AppResources.SetupFailedTitle,
                                    AppResources.SetupFailedBody + "No storage write access.", AppResources.OK);
                            }
                            else
                            {
                                Preferences.Set("SetupV2", true);
                                Navigation.InsertPageBefore(new MainPage(), this);
                                await Navigation.PopAsync(true);
                            }
                        });
                }
                else
                {
                    Preferences.Set("SetupV2", true);
                    Navigation.InsertPageBefore(new MainPage(), this);
                    await Navigation.PopAsync(true);
                }
            }
        }

        public async Task ShowMessage(string title,
            string message,
            string buttonText,
            Action afterHideCallback)
        {
            await DisplayAlert(
                title,
                message,
                buttonText);

            afterHideCallback?.Invoke();
        }
    }
}