using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Android;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using Android.Widget;
using Java.Util;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : TabbedPage
    {

        public MainPage()
        {
            // Check updates
            Device.BeginInvokeOnMainThread(async () => await UpdateCheck());
            NavigationPage.SetHasNavigationBar(this, false);
            InitializeComponent();

            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_goto_pre_initialize", async (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await ReturnToAndroid11Setup());
            });
            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_goto_language", async (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await ReturnToLanguage());
            });
            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_reopen_announcement", async (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await GotoAnnouncementPage());
            });

            this.CurrentPageChanged += OnCurrentPageChanged;
        }

        private void OnCurrentPageChanged(object sender, EventArgs e)
        {
            var title = ((TabbedPage)sender).CurrentPage?.Title;

            if (title.StartsWith("NA"))
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "na_initial_load");
            }
            else if (title.StartsWith("JP"))
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "jp_initial_load");
            }
        }

        public void Unsubscribe()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_goto_pre_initialize");
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_goto_language");
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "installer_page_reopen_announcement");
        }
        public async Task ReturnToAndroid11Setup()
        {
            Unsubscribe();
            var newInitPage = new PreInitializePage();
            Navigation.InsertPageBefore(newInitPage, this);
            await Navigation.PopToRootAsync(true);
            
        }
        public async Task ReturnToLanguage()
        {
            Unsubscribe();
            Navigation.InsertPageBefore(new SetupPage(), this); // tuck under the update page
            await Navigation.PopToRootAsync(true);
        }
        private Dictionary<string, FGORegion> _appsInstalled { get; set; }

        private async Task UpdateCheck()
        {
            var rest = new RestfulAPI();
            var response = await rest.GetVersionAPIResponse();

            bool addAnnouncementPage = false;

            while (!response.IsSuccessful)
            {
                string errorMessage;
                errorMessage = !string.IsNullOrEmpty(response.Data?.Message) 
                    ? $"{AppResources.VersionErrorMessage}\n{response.Data.Status}: {response.Data.Message}" 
                    : $"{AppResources.VersionErrorMessage}\n{response.ResponseStatus}: {response.ErrorMessage}\n\n{response.Content}";

                var doRetry = await DisplayAlert(AppResources.VersionErrorTitle,
                    errorMessage, AppResources.VersionRetryButton, AppResources.VersionExitButton);

                if (!doRetry)
                {
                    DependencyService.Get<IIntentService>()?.ExitApplication();
                }

                response = await rest.GetVersionAPIResponse();
            }

            bool gotoUpdate = response.Data.Response.Action == "update" && Preferences.Get("IgnoreUpdate", 0) < response.Data.Response.Update.AppVer;

            // announcements
            if (response.Data.Response.Announcement != null)
            {
                Preferences.Set("AnnouncementData", JsonConvert.SerializeObject(response.Data.Response.Announcement));
                if (Preferences.Get("AnnouncementRead", 0) < response.Data.Response.Announcement.id)
                {
                    addAnnouncementPage = true;
                }
            }

            var cm = DependencyService.Get<IContentManager>();

            if (cm.CheckBasicAccess())
            {
                if (gotoUpdate)
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await GotoUpdatePage(response.Data.Response.Update); // update takes priority over announcement
                    });

                }
                else if (addAnnouncementPage)
                {
                    await GotoAnnouncementPage();
                }

            }
            else if (string.IsNullOrWhiteSpace(Preferences.Get("StorageLocation", "")))
            {
                if (gotoUpdate)
                {

                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        Unsubscribe();
                        var page = await GotoUpdatePage(response.Data.Response.Update);
                        Navigation.InsertPageBefore(new PreInitializePage(), page); // tuck under the update page
                        Navigation.RemovePage(this);
                    });

                }
                else
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        Unsubscribe();
                        Navigation.InsertPageBefore(new PreInitializePage(), this);
                        await Navigation.PopAsync(true);
                    });

                }
            }
            else
            {
                if (gotoUpdate)
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await GotoUpdatePage(response.Data.Response.Update);
                    });
                }
                else if (addAnnouncementPage)
                {
                    await GotoAnnouncementPage();
                }
            }

        }

        public async Task<Page> GotoUpdatePage(VersionAPIResponse.TranslationUpdateDetails details)
        {
            var page = new UpdatePage(details);
            await Navigation.PushAsync(page);
            if (details.Required)
            {
                Unsubscribe();
                Navigation.RemovePage(this);
            }

            return page;
        }

        public async Task<Page> GotoAnnouncementPage()
        {
            var page = new AnnouncementPage();
            await Navigation.PushAsync(page);

            return page;
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