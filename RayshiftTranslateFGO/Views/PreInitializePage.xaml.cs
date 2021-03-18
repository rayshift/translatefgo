using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RayshiftTranslateFGO.Services;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PreInitializePage : ContentPage
    {
        public PreInitializePage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            AddFolderButton.Clicked += AddFolderButtonOnClicked;
            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page",  (sender) =>
            {
                Device.BeginInvokeOnMainThread(async () => await ReturnToMainPage());
            });
        }

        private void AddFolderButtonOnClicked(object sender, EventArgs e)
        {
            DependencyService.Get<IIntentService>().OpenDocumentTreeIntent("");
        }

        public void Unsubscribe()
        {
            MessagingCenter.Unsubscribe<Application>(Xamarin.Forms.Application.Current, "return_to_main_page");
        }

        public async Task ReturnToMainPage()
        {
            Unsubscribe();
            await Task.Delay(1500);
            IReadOnlyList<Page> navStack = Navigation.NavigationStack;

            if (navStack.Count == 0)
            {
                await Navigation.PushAsync(new MainPage());
                await Navigation.PopToRootAsync(true);
            }
            else
            {
                Navigation.InsertPageBefore(new MainPage(), this);
                await Navigation.PopAsync(true);
            }
        }
    }
}