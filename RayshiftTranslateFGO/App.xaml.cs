using System;
using Android.App;
using Android.Content.Res;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Views;
using Application = Xamarin.Forms.Application;

namespace RayshiftTranslateFGO
{
    public partial class App : Application
    {

        public App()
        {
            InitializeComponent();
            MainPage = new MainPage();
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
