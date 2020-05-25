using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Android;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using Android.Widget;
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
            // Check network connectivity
            /*var current = Connectivity.NetworkAccess;
            if (current == NetworkAccess.None)
            {
                Task.Run(async () =>
                {
                    await ShowMessage("Error", "You need internet access to use this application.", "Exit", () =>
                    {
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    });
                });
                return;
            }*/
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await AppStartup();
            });

            InitializeComponent();
        }

        public async Task AppStartup()
        {
            // Check correct permissions exist
            var statusRead = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            var statusWrite = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (statusRead != PermissionStatus.Granted || statusWrite != PermissionStatus.Granted)
            {
                await ShowMessage("Permission request", "This app requires write access to your storage to operate.", "OK",
                    async () =>
                    {
                        var requestedWrite = await Permissions.RequestAsync<Permissions.StorageWrite>();
                        if (requestedWrite != PermissionStatus.Granted)
                        {
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                            return;
                        }
                        await Permissions.RequestAsync<Permissions.StorageRead>();
                    });
            }
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