using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
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
            ShowCorrectAuthenticationButton();
            ShowCorrectAutoUpdateButton();
        }

        public void ShowCorrectAuthenticationButton()
        {
            if (Preferences.ContainsKey("AuthKey"))
            {
                Authentication.Command = new Command(async () => await Deauthenticate());
                Authentication.Text = "Remove pre-release key";
            }
            else
            {
                Authentication.Command = new Command(async () => await Authenticate());
                Authentication.Text = "Enter pre-release key";
            }
        }

        public void ShowCorrectAutoUpdateButton()
        {
            if (Preferences.ContainsKey("DisableAutoUpdate"))
            {
                AutoUpdate.Command = new Command(EnableAutoUpdate);
                AutoUpdate.Text = "Enable auto update";
            }
            else
            {
                AutoUpdate.Command = new Command(DisableAutoUpdate);
                AutoUpdate.Text = "Disable auto update";
            }
        }

        public async Task Authenticate()
        {
            string result = await DisplayPromptAsync("Enter authentication key", "If you have an authentication key to access content under development, enter it here.");

            if (result != null)
            {

                bool isValid = Guid.TryParse(result, out _);

                if (!isValid)
                {
                    await DisplayAlert("Error", "The format of the key is invalid.", "OK");
                    return;
                }

                Preferences.Set("AuthKey", result);
                ShowCorrectAuthenticationButton();
            }
        }
        public async Task Deauthenticate()
        {
            if (await DisplayAlert("Confirm", "Are you sure you would like to clear the current authentication key?", "Yes", "No"))
            {
                Preferences.Remove("AuthKey");
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