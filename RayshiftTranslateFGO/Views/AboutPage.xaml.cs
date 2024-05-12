using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.ViewModels;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class AboutPage : ContentPage
    {
        protected RestfulAPI API;
        public AboutPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            ShowCorrectAuthenticationButton();
            ShowCorrectAutoUpdateButton();
            ShowCorrectLinkAccountButton();
            this.Version.Text = ScriptUtil.GetVersionName();
            RetryAndroid11.Clicked += RetryAndroid11OnClicked;
            ChangeLanguage.Clicked += ChangeLanguageOnClicked;
            ShizukuSetupButton.Clicked += OpenShizukuOnClicked;
            ResetApp.Clicked += ResetAppOnClicked;
            API = new RestfulAPI();

            var sLock = App.GetViewModel<MainPageViewModel>().Cache.Get<bool>("AboutSubscribeLock");
            if (!sLock)
            {
                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "connect_rayshift_account", async (sender) =>
                {
                    Device.BeginInvokeOnMainThread(async () => await StartLinkAccount());
                });

                App.GetViewModel<MainPageViewModel>().Cache.Set("AboutSubscribeLock", true);
            }

            BindingContext = App.GetViewModel<AboutViewModel>();
        }

        protected override void OnAppearing()
        {
            var announcementJson = Preferences.Get("AnnouncementData", null);

            if (announcementJson == null)
            {
                ReopenAnnouncement.IsEnabled = false;
            }
            else
            {
                ReopenAnnouncement.Command = new Command(OpenAnnouncementOnClicked);
            }

        }

        private void ChangeLanguageOnClicked(object sender, EventArgs e)
        {
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_language");
        }

        private async void RetryAndroid11OnClicked(object sender, EventArgs e)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_pre_initialize");
            }
            else
            {
                var intentService = DependencyService.Get<IIntentService>();
                intentService.MakeToast(AppResources.TooLowAndroidVersion);
            }
        }

        private async void OpenShizukuOnClicked(object sender, EventArgs e)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O) // api 26
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_shizuku");
            }
            else
            {
                var intentService = DependencyService.Get<IIntentService>();
                intentService.MakeToast(AppResources.ShizukuTooLowAndroidVersion);
            }
        }
        private async void OpenAnnouncementOnClicked()
        {
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_reopen_announcement");
        }

        private async void ResetAppOnClicked(object sender, EventArgs e)
        {
            if (await DisplayAlert(AppResources.Confirm, AppResources.ResetAppText, AppResources.Yes, AppResources.No))
            {
                Preferences.Clear();
                DependencyService.Get<IIntentService>().ExitApplication();
            }
        }

        private async Task StartLinkAccount()
        {
            var intentService = DependencyService.Get<IIntentService>();
            var linkData = await intentService.LinkAccount();

            if (linkData != null && !string.IsNullOrEmpty(linkData.AccessToken))
            {
                if (!Guid.TryParse(linkData.AccessToken, out Guid guid))
                {
                    intentService.MakeToast(string.Format(AppResources.LinkAccountFailure, "invalid token returned"));
                    return;
                }
                // test link
                var test = await API.GetLinkedUserDetails(guid);

                if (test.Data.Status != 200)
                {
                    intentService.MakeToast(string.Format(AppResources.LinkAccountFailure, $"{test.Data.Status} - {test.Data.Message}"));
                    return;
                }

                intentService.MakeToast(string.Format(AppResources.LinkAccountSuccess, test.Data.Response.userName));

                // set link
                Preferences.Set(EndpointURL.GetLinkedAccountKey(), linkData.AccessToken);

                App.GetViewModel<AboutViewModel>().Cache.Set("LoggedUser", test.Data);
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "reset_initial_load");
                if (test.Data.Response.isPlus)
                {
                    MessagingCenter.Send(Xamarin.Forms.Application.Current, "add_art_tab_non_donor");
                }
                else
                {
                    MessagingCenter.Send(Xamarin.Forms.Application.Current, "remove_art_tab_non_donor");
                }
            }

            ShowCorrectLinkAccountButton();
        }

        private async Task StartUnlinkAccount()
        {
            if (await DisplayAlert(AppResources.Confirm, AppResources.UnlinkConfirmation, AppResources.Yes, AppResources.No))
            {
                Preferences.Remove(EndpointURL.GetLinkedAccountKey());
                App.GetViewModel<AboutViewModel>().Cache.Remove("LoggedUser");
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "reset_initial_load");
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "remove_art_tab_non_donor");
                ShowCorrectLinkAccountButton();
            }

        }

        public void ShowCorrectAuthenticationButton()
        {
            if (Preferences.ContainsKey("AuthKey"))
            {
                Authentication.Command = new Command(async () => await Deauthenticate());
                Authentication.Text = AppResources.RemovePreReleaseKey;
            }
            else
            {
                Authentication.Command = new Command(async () => await Authenticate());
                Authentication.Text = AppResources.AddPreReleaseKey;
            }
        }

        public void ShowCorrectAutoUpdateButton()
        {
            if (Preferences.ContainsKey("DisableAutoUpdate"))
            {
                AutoUpdate.Command = new Command(EnableAutoUpdate);
                AutoUpdate.Text = AppResources.EnableAutoScriptUpdate;
            }
            else
            {
                AutoUpdate.Command = new Command(DisableAutoUpdate);
                AutoUpdate.Text = AppResources.RemoveAutoScriptUpdate;
            }
        }

        public void ShowCorrectLinkAccountButton()
        {
            if (!Preferences.ContainsKey(EndpointURL.GetLinkedAccountKey()))
            {
                LinkAccount.Command = new Command(async () => await StartLinkAccount());
                LinkAccount.Text = AppResources.LinkAccount;
            }
            else
            {
                LinkAccount.Command = new Command(async () => await StartUnlinkAccount());
                LinkAccount.Text = AppResources.UnlinkAccount;
            }
        }

        public async Task Authenticate()
        {
            string result = await DisplayPromptAsync(AppResources.EnterAuthKey, AppResources.EnterAuthKeyDescription);

            if (result != null)
            {

                bool isValid = Guid.TryParse(result, out _);

                if (!isValid)
                {
                    await DisplayAlert(AppResources.Error, AppResources.EnterAuthKeyInvalid, AppResources.OK);
                    return;
                }

                Preferences.Set("AuthKey", result);
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "reset_initial_load");
                ShowCorrectAuthenticationButton();
            }
        }
        public async Task Deauthenticate()
        {
            if (await DisplayAlert(AppResources.Confirm, AppResources.EnterAuthKeyClear, AppResources.Yes, AppResources.No))
            {
                Preferences.Remove("AuthKey");
                if (!string.IsNullOrEmpty(EndpointURL.OldEndPoint))
                {
                    EndpointURL.EndPoint = EndpointURL.OldEndPoint;
                    EndpointURL.OldEndPoint = "";
                    EndpointURL.NeedsRefresh = true;
                }

                MessagingCenter.Send(Xamarin.Forms.Application.Current, "reset_initial_load");
                ShowCorrectAuthenticationButton();
            }
        }

        public void EnableAutoUpdate()
        {
            Preferences.Remove("DisableAutoUpdate");
            ShowCorrectAutoUpdateButton();
        }

        public void DisableAutoUpdate()
        {
            Preferences.Set("DisableAutoUpdate", 1);
            ShowCorrectAutoUpdateButton();
        }
    }
}