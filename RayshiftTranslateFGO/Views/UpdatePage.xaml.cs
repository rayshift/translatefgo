using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Util;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class UpdatePage : ContentPage
    {
        private readonly VersionAPIResponse.TranslationUpdateDetails _updateDetails;
        public UpdatePage(VersionAPIResponse.TranslationUpdateDetails updateDetails)
        {
            _updateDetails = updateDetails;
            InitializeComponent();

            this.DownloadUpdateButtonDouble.Clicked += async (o, args) => await DownloadUpdateButtonOnClicked(o, args);
            this.DownloadUpdateButtonSingle.Clicked += async (o, args) => await DownloadUpdateButtonOnClicked(o, args);

            this.UpdateTopTitle.Text = updateDetails.Required ? String.Format(AppResources.UpdateRequiredTitle, ScriptUtil.GetVersionName(), updateDetails.ReadableVer) : String.Format(AppResources.UpdateOptionalTitle, ScriptUtil.GetVersionName(), updateDetails.ReadableVer);
            this.UpdateTopText.Text = updateDetails.Required
                ? AppResources.UpdateRequiredDescription
                : AppResources.UpdateOptionalDescription;

            StringBuilder sb = new StringBuilder();
            foreach (var change in updateDetails.UpdateChanges)
            {
                sb.AppendLine($"\u2022 {change}");
            }

            this.UpdateChangelogTitle.Text = updateDetails.UpdateTitle;
            this.UpdateChangelog.Text = sb.ToString();

            if (updateDetails.Required)
            {
                UpdateChangelogDoubleWidth.IsVisible = false;
                NavigationPage.SetHasNavigationBar(this, false);
            }
            else
            {
                SkipUpdateButton.Clicked += SkipUpdateButtonOnClicked;
                this.DownloadUpdateButtonSingle.IsVisible = false;
            }
        }

        private void SkipUpdateButtonOnClicked(object sender, EventArgs e)
        {
            Preferences.Set("IgnoreUpdate", _updateDetails.AppVer);
            Navigation.RemovePage(this);
        }

        public UpdatePage()
        {
            InitializeComponent();
        }

        private async Task DownloadUpdateButtonOnClicked(object sender, EventArgs e)
        {
            await Xamarin.Essentials.Browser.OpenAsync(AppResources.UpdateURL);
        }
    }
}