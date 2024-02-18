using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Annotations;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using static Android.Provider.ContactsContract.CommonDataKinds;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ArtPage : ContentPage
    {
        private HashSet<InstalledFGOInstances> _installedFgoInstances { get; set; }

        private ArtAPIResponse _handshake;

        private readonly ObservableCollection<ArtGUIObject> _guiObjects =
            new ObservableCollection<ArtGUIObject>();

        private ContentType _accessMode = ContentType.DirectAccess;

        private bool _pageOpened = false;

        private bool _hasSubscribed = false;

        private Dictionary<string, string> _storageLocations = new Dictionary<string, string>();

        public const string _assetList = "cfb1d36393fd67385e046b084b7cf7ed";

        private bool _isCurrentlyUpdating = false;

        private IContentManager _cm { get; set; }
        private IScriptManager _sm { get; set; }
        private IIntentService _im { get; set; }

        private bool _isLoggedIn { get; set; } = false;
        private bool _isDonor { get; set; } = false;

        public bool NAEnabled { get; set; } = false;
        public bool JPEnabled { get; set; } = false;

        // combo of NEW crc + art id + filename, to check if already installed and we can skip
        public List<string> JPFileChecksum { get; set; } = new List<string>();
        public List<string> NAFileChecksum { get; set; } = new List<string>();

        public ArtPage()
        {
            _cm = DependencyService.Get<IContentManager>();
            _sm = DependencyService.Get<IScriptManager>();
            _im = DependencyService.Get<IIntentService>();
            NavigationPage.SetHasNavigationBar(this, false);
            InitializeComponent();
            BindingContext = this;

            RetryButton.Clicked += UpdateArtListClick;
            RefreshButton.Clicked += UpdateArtListClick;
            RevertButton.Clicked += RevertButtonOnClicked;
            Refresh.Refreshing += UpdateArtListClick;
        }


        /// <summary>
        /// Handshake the first time the view opens
        /// </summary>
        protected override async void OnAppearing()
        {
            if (!_hasSubscribed)
            {
                _hasSubscribed = true;
                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "art_initial_load",
                    async (sender) => { await InitialLoad(); });

                MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "reset_initial_load",
                    async (sender) => { _pageOpened = false; });
            }

            await InitialLoad();

        }

        private async void RevertButtonOnClicked(object sender, EventArgs e)
        {
            await UninstallArt();
        }

        private async Task UninstallArt()
        {
            _cm.ClearCache();
            bool answer = await DisplayAlert(AppResources.Warning, AppResources.UninstallArtWarning, AppResources.Yes, AppResources.No);
            if (!answer)
            {
                return;
            }
            SwitchButtons(false);
            List<KeyValuePair<string, string>> filesToRemove = new List<KeyValuePair<string, string>>();
            RevertButton.Text = AppResources.ArtUninstallingButton;
            try
            {
                foreach (var region in new[] { FGORegion.Jp, FGORegion.Na })
                {
                    var artUrls = region == FGORegion.Jp
                        ? _handshake.Response.JPArtUrls
                        : _handshake.Response.NAArtUrls;

                    await _sm.GetArtAssetStorage(_accessMode,
                        region,
                        _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path).ToList(),
                        artUrls.ToList(),
                        true);

                    var removals = artUrls.SelectMany(s => s.Urls).Select(s => s.Filename).ToList();
                    foreach (var game in _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path))
                    {
                        foreach (var file in removals)
                        {
                            var path = _accessMode == ContentType.StorageFramework
                                ? $"files/data/d713/{file}"
                                : $"{game}/files/data/d713/{file}";
                            filesToRemove.Add(new KeyValuePair<string, string>(path, game));
                        }
                    }

                    
                }
                int i = 0;
                foreach (var file in filesToRemove)
                {
                    i += 1;
                    RevertButton.Text = String.Format(AppResources.UninstallingText, i, filesToRemove.Count);
                    OnPropertyChanged();
                    await _cm.RemoveFileIfExists(_accessMode,
                        file.Key, file.Value);
                }

                RevertButton.Text = AppResources.UninstallFinished;
                Preferences.Remove("JPArtChecksums");
                Preferences.Remove("NAArtChecksums");
                OnPropertyChanged();
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
            }
            await UpdateArtList();
        }

        public async Task InitialLoad()
        {
            if (!_pageOpened)
            {
                _pageOpened = true;
                await UpdateArtList();
            }
        }

        public async void UpdateArtListClick(object obj, EventArgs args)
        {
            await UpdateArtList();
        }

        public async Task UpdateArtList()
        {
            _cm.ClearCache();
            if (_isCurrentlyUpdating) return;
            _isCurrentlyUpdating = true;

            try
            {
                RetryButton.IsVisible = false;
                LoadingLayout.IsVisible = true;
                ArtListView.IsVisible = false;
                MasterButtons.IsVisible = false;
                Refresh.IsRefreshing = true;
                SwitchErrorObjects(false);
                ActivityIndicatorLoading.IsRunning = true;
                Refresh.IsEnabled = false;
                RevertButton.Text = AppResources.UninstallText;
                ActivityIndicatorLoading.VerticalOptions = LayoutOptions.Center;
                ActivityIndicatorLoading.HorizontalOptions = LayoutOptions.Center;

                LoadingText.Text = AppResources.LoadingPleaseWait;
                LoadingText.VerticalOptions = LayoutOptions.CenterAndExpand;
                LoadingText.HorizontalOptions = LayoutOptions.CenterAndExpand;

                //await Task.Delay(1000);

                var locationJson = Preferences.Get("StorageLocations", "{}");
                var locations = JsonConvert.DeserializeObject<Dictionary<string, string>>(locationJson);

                _storageLocations = locations;

                if (Preferences.Get("UseShizuku", false))
                {
                    _accessMode = ContentType.Shizuku;
                }
                else if (_storageLocations.Count > 0 || !_cm.CheckBasicAccess())
                {
                    _accessMode = ContentType.StorageFramework;
                }
                else
                {
                    _accessMode = ContentType.DirectAccess;
                }

                // need to pause for a bit here if shizuku

                if (_accessMode == ContentType.Shizuku)
                {
                    LoadingText.Text = AppResources.ShizukuLoading;
                    if (!_im.IsShizukuAvailable())
                    {
                        LoadingText.Text = AppResources.ShizukuConnectFailure;
                        SwitchErrorObjects(true);
                        return;
                    }
                    if (!_im.IsShizukuServiceBound())
                    {
                        var maxTries = 10; // give it 5 seconds to bind, might not be long enough?
                        while (!_im.IsShizukuServiceBound())
                        {
                            if (maxTries <= 0)
                            {
                                LoadingText.Text = AppResources.ShizukuConnectFailure;
                                SwitchErrorObjects(true);
                                return;
                            }
                            if (DependencyService.Get<IIntentService>().IsShizukuAvailable())
                            {
                                DependencyService.Get<IIntentService>().CheckShizukuPerm(true);
                            }
                            await Task.Delay(500);
                            maxTries--;
                        }

                    }
                    LoadingText.Text = AppResources.LoadingPleaseWait;
                }


                _installedFgoInstances = _cm.GetInstalledGameApps(_accessMode, _storageLocations);
                


                // Check region is installed
                if (!_installedFgoInstances.Any())
                {
                    LoadingText.Text = String.Format(AppResources.NoFGOFound,
                        $"Fate/Grand Order");
                    SwitchErrorObjects(true);
                    return;
                }

                foreach (var instance in _installedFgoInstances.ToList())
                {
                    var filePath = _accessMode == ContentType.StorageFramework
                        ? $"files/data/d713/{_assetList}"
                        : $"{instance.Path}/files/data/d713/{_assetList}";
                    var assetStorage = await _cm.GetFileContents(
                        _accessMode,
                        filePath, instance.Path);


                    if (!assetStorage.Successful)
                    {
                        _installedFgoInstances.Remove(instance);
                    }

                    instance.LastModified = assetStorage.LastModified;

                    if (assetStorage?.FileContents != null)
                    {
                        var base64 = "";
                        using var inputStream = new MemoryStream(assetStorage.FileContents);
                        using (var reader = new StreamReader(inputStream, Encoding.ASCII))
                        {
                            base64 = await reader.ReadToEndAsync();
                        }

                        instance.AssetStorage = base64;
                    }
                    else
                    {
                        instance.AssetStorage = null;
                    }
                }

                var instanceDictNA =
                    _installedFgoInstances.OrderByDescending(o => o.LastModified).FirstOrDefault(w => w.Region == FGORegion.Na);

                var instanceDictJP =
                    _installedFgoInstances.OrderByDescending(o => o.LastModified).FirstOrDefault(w => w.Region == FGORegion.Jp);

                if (instanceDictNA == null && instanceDictJP == null)
                {
                    LoadingText.Text = String.Format(AppResources.NoFGOInstallationFound2,
                        $"Fate/Grand Order");
                    SwitchErrorObjects(true);
                    return;
                }

                NAEnabled = instanceDictNA != null;
                JPEnabled = instanceDictJP != null;


                _guiObjects?.Clear();
                // GET ART LIST
                var rest = new RestfulAPI();
                var handshake = await rest.GetArtAPIResponse(instanceDictJP?.AssetStorage, instanceDictNA?.AssetStorage);
                _handshake = handshake.Data;

                if (handshake.Data == null || handshake.Data.Status != 200)
                {
                    LoadingText.Text = handshake.Data == null
                        ? AppResources.TryAgainLater
                        : $"{AppResources.TranslateAPIError}\n{handshake.Data?.Message}";
                    SwitchErrorObjects(true);
                    return;
                }

                if (_handshake.Response.AccountStatus != null)
                {
                    _isLoggedIn = _handshake.Response.AccountStatus.tokenStatus == UserTokenStatus.Active;
                    _isDonor = _handshake.Response.AccountStatus.isPlus;
                }
                else
                {
                    _isLoggedIn = false;
                    _isDonor = false;
                }
                
                // warn for issues
                foreach(var region in new [] {FGORegion.Jp, FGORegion.Na})
                {

                    var regionEnabled = region == FGORegion.Jp ? JPEnabled : NAEnabled;
                    var instanceDict = region == FGORegion.Jp ? instanceDictJP : instanceDictNA;
                    var status = region == FGORegion.Jp
                        ? handshake.Data.Response.JPAssetStatus
                        : handshake.Data.Response.NAAssetStatus;
                    var artUrls = region == FGORegion.Jp
                        ? _handshake.Response.JPArtUrls
                        : _handshake.Response.NAArtUrls;

                    var pref = region == FGORegion.Jp 
                        ? Preferences.Get("JPArtChecksums", "[]") 
                        : Preferences.Get("NAArtChecksums", "[]");

                    var checksums = JsonConvert.DeserializeObject<List<string>>(pref);
                    if (checksums == null)
                    {
                        checksums = new List<string>(); 
                    }

                    if (!regionEnabled) continue; // TODO: Disable region

                    if (status != HandshakeAssetStatus.Missing &&
                        status != HandshakeAssetStatus.UpToDate)
                    {

                        var warningTitle = AppResources.Warning + $" ({region.ToString().ToUpper()})";
                        switch (status)
                        {
                            case HandshakeAssetStatus.UpdateRequired:
                                await DisplayAlert(warningTitle, AppResources.AssetWarningOutOfDate, AppResources.OK);
                                break;
                            case HandshakeAssetStatus.TimeTraveler:
                                await DisplayAlert(warningTitle, AppResources.AssetWarningFutureUnreleased,
                                    AppResources.OK);
                                break;
                            case HandshakeAssetStatus.Unrecognized:
                                await DisplayAlert(warningTitle, AppResources.AssetWarningUnrecognised,
                                    AppResources.OK);
                                break;
                            case HandshakeAssetStatus.Corrupt:
                                await DisplayAlert(warningTitle, AppResources.AssetWarningCorrupted, AppResources.OK);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                     // Add gui elements

                     foreach (var item in artUrls)
                     {
                         if (item.IsCurrentlyInstalled && !item.IsNew)
                         {
                             if (!item.Urls.TrueForAll(w => checksums.Contains(w.Hash))) { // for this object, we don't have all checksums
                                 item.ToBeInstalled = true;
                             }
                         }
                         else
                         {
                             item.ToBeInstalled = true;
                         }
                     }

                     var total = artUrls.Count();
                     var totalStarred = artUrls.Count(c => c.Starred);
                     var notInstalled = artUrls.Count(c => c.ToBeInstalled);
                     var notInstalledStarred = artUrls.Count(c => c.ToBeInstalled && c.Starred);

                     var totalSize = artUrls.Where(w => w.ToBeInstalled).Sum(w => w.Size);
                     var totalSizeStarred = artUrls.Where(w => w.Starred && w.ToBeInstalled).Sum(w => w.Size);
                     _guiObjects.Add(new ArtGUIObject()
                     {
                         BundleHidden = false,
                         SizeOfInstall = String.Format(AppResources.ArtTotalSize, totalSizeStarred.Bytes().Humanize("#.## MB"), totalSize.Bytes().Humanize("#.## MB")),
                         Name = region == FGORegion.Jp ? AppResources.JPArtName : AppResources.NAArtName,
                         Status = String.Format(AppResources.ArtStatus, notInstalledStarred, totalStarred, notInstalled, total),
                         Install2Enabled = notInstalled > 0,
                         Install1Enabled = notInstalledStarred > 0,
                         CanEnable1 = notInstalledStarred > 0,
                         CanEnable2 = notInstalled > 0,
                         Install1Click = new Command(async () => await InstallArt(region, artUrls.Where(w => w.Starred && w.ToBeInstalled), 1), () => ButtonsEnabled && notInstalledStarred > 0),
                         Install2Click = new Command(async () => await InstallArt(region, artUrls.Where(w=>w.ToBeInstalled), 2), ()=>ButtonsEnabled && notInstalled > 0),
                         Region = region,
                         TextColor = Color.Default
                });
                }
                ArtListView.ItemsSource = _guiObjects;


                LoadingLayout.IsVisible = false;
                ArtListView.IsVisible = true;
                MasterButtons.IsVisible = true;
                Refresh.IsRefreshing = false;
                SwitchErrorObjects(false);
                ActivityIndicatorLoading.IsRunning = false;
                Refresh.IsEnabled = true;
                SwitchButtons(true);
            }
            catch (Exception ex)
            {
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
                _isCurrentlyUpdating = false;
                SwitchErrorObjects(true);
            }

            _isCurrentlyUpdating = false;
        }

        private async Task InstallArt(FGORegion region, IEnumerable<ArtUrl> artUrls, int button)
        {
            _cm.ClearCache();
            bool answer = await DisplayAlert(AppResources.Warning, AppResources.ArtInstallWarning, AppResources.Yes, AppResources.No);
            if (!answer)
            {
                return;
            }
            SwitchButtons(false);
            var rest = new RestfulAPI();
            Task successSendTask = null;
            try
            {
                var installResult = await _sm.InstallArt(
                    _accessMode,
                    region,
                    _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path).ToList(),
                    artUrls.ToList(),
                    _guiObjects,
                    button
                );

                if (!installResult.IsSuccessful)
                {
                    successSendTask = rest.SendSuccess(region, -1, TranslationInstallType.Manual, -1,
                        false, installResult.ErrorMessage, _accessMode == ContentType.StorageFramework, true);
                    await DisplayAlert(AppResources.Error,
                        installResult.ErrorMessage, AppResources.OK);

                }
                else
                {
                    successSendTask = rest.SendSuccess(region, -1, TranslationInstallType.Manual, -1,
                        true, "", _accessMode == ContentType.StorageFramework, true);
                    await Task.Delay(1000);
                }

            }
            catch (System.UnauthorizedAccessException ex)
            {
                if (_accessMode != ContentType.StorageFramework)
                {

                    successSendTask = rest.SendSuccess(region, -1, TranslationInstallType.Manual, -1,
                        false, "Unauthorized error handler: " + ex.ToString(), _accessMode == ContentType.StorageFramework, true);
                    var retry = await DisplayAlert(AppResources.DirectoryPermissionDeniedTitle,
                        AppResources.Android11AskToSetup, AppResources.Yes, AppResources.No);

                    if (retry)
                    {
                        await successSendTask;
                        MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_goto_pre_initialize");
                        return;
                    }

                    await successSendTask;
                    return;
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                successSendTask = rest.SendSuccess(region, -1, TranslationInstallType.Manual, -1,
                    false, ex.ToString(), _accessMode == ContentType.StorageFramework, true);
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
            }

            await successSendTask;
            await UpdateArtList();
        }

        private bool ButtonsEnabled = true;

        public void SwitchButtons(bool status)
        {
            ButtonsEnabled = status;


            foreach (var button in _guiObjects)
            {
                button.Install1Enabled = status && button.CanEnable1;
                if (button.CanEnable1)
                {
                    button.Install1Click.ChangeCanExecute();
                }
                button.Install2Enabled = status && button.CanEnable2;
                if (button.CanEnable2)
                {
                    button.Install2Click.ChangeCanExecute();
                }

                OnPropertyChanged();
            }

            RevertButton.IsEnabled = status;
            RefreshButton.IsEnabled = status;
            Refresh.IsEnabled = status;
            OnPropertyChanged();
        }

        public void SwitchErrorObjects(bool status)
        {
            LoadingText.HorizontalTextAlignment = TextAlignment.Center;
            ActivityIndicatorLoading.IsRunning = !status;
            RetryButton.IsVisible = status;
            Refresh.IsEnabled = status;
            Refresh.IsRefreshing = !status;
            ButtonsEnabled = status;
            _isCurrentlyUpdating = !status;
        }

        public class ArtGUIObject : INotifyPropertyChanged
        {
            public string Name { get; set; }

            public string Status
            {
                get => _status;
                set
                {
                    _status = value;
                    RaisePropertyChanged(nameof(Status));
                }
            }

            private string _status;

            public Command Install1Click { get; set; }
            public Command Install2Click { get; set; }
            public bool Install1Enabled
            {
                get => _install1Enabled;
                set
                {
                    _install1Enabled = value;
                    RaisePropertyChanged(nameof(Install1Enabled));
                }
            }

            public bool Install2Enabled
            {
                get => _install2Enabled;
                set
                {
                    _install2Enabled = value;
                    RaisePropertyChanged(nameof(Install2Enabled));
                }
            }

            public bool CanEnable1 { get; set; } = false;
            public bool CanEnable2 { get; set; } = false;

            public bool _install1Enabled { get; set; } = false;
            public bool _install2Enabled { get; set; } = false;
            public string Button1InstallText
            {
                get => _button1InstallText;
                set
                {
                    _button1InstallText = value;
                    RaisePropertyChanged(nameof(Button1InstallText));
                }
            }

            public FGORegion Region { get; set; }

            public string Button2InstallText
            {
                get => _button2InstallText;
                set
                {
                    _button2InstallText = value;
                    RaisePropertyChanged(nameof(Button2InstallText));
                }
            }

            public string SizeOfInstall
            {
                get => _sizeOfInstall;
                set
                {
                    _sizeOfInstall = value;
                    RaisePropertyChanged(nameof(SizeOfInstall));
                }
            }

            private string _sizeOfInstall = "Unknown";

            public Color TextColor
            {
                get => _textColor;
                set
                {
                    _textColor = value;
                    RaisePropertyChanged(nameof(TextColor));
                }
            }

            private Color _textColor;

            public bool BundleHidden { get; set; } = false;

            private string _button1InstallText = AppResources.InstallStarred;
            private string _button2InstallText = AppResources.InstallAll;
            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}