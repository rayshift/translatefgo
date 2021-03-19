using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
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
        public AboutPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            ShowCorrectAuthenticationButton();
            ShowCorrectAutoUpdateButton();
            this.Version.Text = ScriptUtil.GetVersionName();
            RetryAndroid11.Clicked += RetryAndroid11OnClicked;
            ChangeLanguage.Clicked += ChangeLanguageOnClicked;
            ResetApp.Clicked += ResetAppOnClicked;
        }

        private void ChangeLanguageOnClicked(object sender, EventArgs e)
        {
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_language");
        }

        private async void RetryAndroid11OnClicked(object sender, EventArgs e)
        {
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_pre_initialize");
        }

        private async void ResetAppOnClicked(object sender, EventArgs e)
        {
            if (await DisplayAlert(AppResources.Confirm, AppResources.ResetAppText, AppResources.Yes, AppResources.No))
            {
                Preferences.Clear();
                DependencyService.Get<IIntentService>()?.ExitApplication();
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
                }
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