using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Util;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AnnouncementPage : ContentPage
    {
        private VersionAPIResponse.TranslationAnnouncements _announcementDetails;
        public AnnouncementPage()
        {
            var announcementJson = Preferences.Get("AnnouncementData", null);

            if (announcementJson == null)
            {
                Navigation.RemovePage(this);
                return;
            }

            _announcementDetails =
                JsonConvert.DeserializeObject<VersionAPIResponse.TranslationAnnouncements>(announcementJson);

            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(_announcementDetails.Url))
            {
                this.OpenWebsite.Clicked += async (o, args) => await WebsiteButtonOnClicked(o, args);
            }
            else
            {
                this.OpenWebsite.IsEnabled = false;
            }
            this.Close.Clicked += async (o, args) => CloseAnnouncementButtonOnClicked(o, args);

            this.AnnouncementTitle.Text = _announcementDetails.Title;
            this.AnnouncementBody.Text = _announcementDetails.Message;

            if (!string.IsNullOrWhiteSpace(_announcementDetails.ImageUrl))
            {
                this.DefaultLogo.IsVisible = false;
                this.AnnouncementImage.Source = _announcementDetails.ImageUrl;
                this.ImageLogo.IsVisible = true;
            }
        }

        private void CloseAnnouncementButtonOnClicked(object sender, EventArgs e)
        {
            Preferences.Set("AnnouncementRead", _announcementDetails.id);
            Navigation.RemovePage(this);
        }


        private async Task WebsiteButtonOnClicked(object sender, EventArgs e)
        {
            await Xamarin.Essentials.Browser.OpenAsync(_announcementDetails.Url);
        }
    }
}