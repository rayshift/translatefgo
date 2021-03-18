using System;
using Android.App;
using Android.Content.Res;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Views;
using Xamarin.Essentials;
using Application = Xamarin.Forms.Application;

namespace RayshiftTranslateFGO
{
    public partial class App : Application
    {

        public App()
        {
            var language = Preferences.Get("Language", "en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(language);
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);
            InitializeComponent();

            if (Preferences.Get("SetupV2", false))
            {
                MainPage = new NavigationPage(new MainPage());
            }
            else
            {
                MainPage = new NavigationPage(new SetupPage());
            }


        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
