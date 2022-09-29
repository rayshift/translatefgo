using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    public partial class MainPage : TabbedPage
    {
        private bool _artPageIsAdded = true;
        public MainPage()
        {
            // Check updates
            Device.BeginInvokeOnMainThread(async () => await UpdateCheck());
            NavigationPage.SetHasNavigationBar(this, false);
            InitializeComponent();
            
            HideArtPage();
            BindingContext = App.GetViewModel<MainPageViewModel>();

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

            var sLock = App.GetViewModel<MainPageViewModel>().Cache.Get<bool>("GlobalSubscribeLock");
            if (!sLock)
            {
                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "remove_art_tab_non_donor", async (sender) =>
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        var versionData = App.GetViewModel<MainPageViewModel>().Cache.Get<VersionAPIResponse.VersionUpdate>("VersionDetails");
                        if (versionData.FeaturesEnabled.HasFlag(EnabledTranslationFeatures.ArtDonorOnly))
                        {
                            HideArtPage();
                        }
                    });
                });

                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "add_art_tab_non_donor", async (sender) =>
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        var versionData = App.GetViewModel<MainPageViewModel>().Cache.Get<VersionAPIResponse.VersionUpdate>("VersionDetails");
                        if (versionData.FeaturesEnabled.HasFlag(EnabledTranslationFeatures.ArtDonorOnly))
                        {
                            ShowArtPage();
                        }
                    });
                });

                App.GetViewModel<MainPageViewModel>().Cache.Set("GlobalSubscribeLock", true);
            }

            this.CurrentPageChanged += OnCurrentPageChanged;
        }

        private void OnCurrentPageChanged(object sender, EventArgs e)
        {
            var title = ((TabbedPage)sender).CurrentPage?.Title;

            if (title == UIFunctions.GetResourceString("NAInstaller"))
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "na_initial_load");
            }
            else if (title == UIFunctions.GetResourceString("JPInstaller"))
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "jp_initial_load");
            }
            else if (title == UIFunctions.GetResourceString("CustomArtTab"))
            {
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "art_initial_load");
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

            // store in cache for settings
            App.GetViewModel<MainPageViewModel>().Cache.Set("VersionDetails", response.Data.Response);

            var artIsVisible = response.Data.Response.FeaturesEnabled.HasFlag(EnabledTranslationFeatures.Art);

            if (artIsVisible)
            {
                ShowArtPage();
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
            else if (string.IsNullOrWhiteSpace(Preferences.Get("StorageLocations", "")))
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

        public void HideArtPage()
        {
            var tabbedPage = this;
            if (_artPageIsAdded)
            {
                _artPageIsAdded = false;
                tabbedPage.Children.RemoveAt(2);
            }
        }

        public void ShowArtPage()
        {
            var tabbedPage = this;
            if (!_artPageIsAdded)
            {
                _artPageIsAdded = true;
                ArtPageRef.Title = AppResources.CustomArtTab; // don't know why this is needed but ok
                tabbedPage.Children.Insert(2, ArtPageRef);
                //CustomArtTabPage.Title = AppResources.CustomArtTab;
                OnPropertyChanged();
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