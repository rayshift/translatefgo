using System;
using Android.App;
using Android.Content.Res;
using Microsoft.Extensions.DependencyInjection;

using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.ViewModels;
using RayshiftTranslateFGO.Views;
using Xamarin.Essentials;
using Xamarin.Forms;
using Application = Xamarin.Forms.Application;

namespace RayshiftTranslateFGO
{
    public partial class App : Application
    {

        protected static IServiceProvider ServiceProvider { get; set; }

        public App()
        {
            var language = Preferences.Get("Language", "en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(language);
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);
            InitializeComponent();
            SetupServices();

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var dep = DependencyService.Get<IIntentService>();
                var requestedAnd11Acc = dep.IsExternalStorageManager();

                MainPage = Preferences.Get("SetupV2", false) && requestedAnd11Acc
                    ? new NavigationPage(new MainPage())
                    : new NavigationPage(new SetupPage());
            }
            else
            {
                MainPage = Preferences.Get("SetupV2", false)
                    ? new NavigationPage(new MainPage())
                    : new NavigationPage(new SetupPage());
            }
        }

        public static BaseViewModel GetViewModel<TViewModel>()
            where TViewModel : BaseViewModel
            => ServiceProvider.GetService<TViewModel>();

        /// <summary>
        /// https://blog.infernored.com/using-dotnet-extensions-to-do-dependency-injection-in-xamarin-forms/
        /// </summary>
        void SetupServices()
        {
            var services = new ServiceCollection();
            services.AddTransient<BaseViewModel>();
            services.AddTransient<MainPageViewModel>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<PreInitializeViewModel>();
            services.AddTransient<ShizukuSetupViewModel>();
            services.AddTransient<InstallerPageModel>();
            services.AddSingleton<ICacheProvider, CacheProvider>();

            ServiceProvider = services.BuildServiceProvider();
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
