using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Android.Util;
using Dasync.Collections;
using Humanizer;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Annotations;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.ViewModels;
using Sentry;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class InstallerPage : ContentPage
    {

        public FGORegion Region { get; set; } = FGORegion.Jp;
        public string RegionString => Region.ToString();

        private HashSet<InstalledFGOInstances> _installedFgoInstances { get; set; }

        private HandshakeAPIResponse _handshake;
        private readonly Dictionary<int, TranslationList> _translations = new Dictionary<int, TranslationList>();
        private readonly ObservableCollection<TranslationGUIObject> _guiObjects = new ObservableCollection<TranslationGUIObject>();

        public const string _assetList = "cfb1d36393fd67385e046b084b7cf7ed";
        private bool _pageOpened = false;

        private ContentType _accessMode = ContentType.DirectAccess;
        private Dictionary<string, string> _storageLocations = new Dictionary<string, string>();

        private bool _isCurrentlyUpdating = false;

        private IContentManager _cm { get; set; }
        private IScriptManager _sm { get; set; }
        private IIntentService _im { get; set; }

        private bool _isLoggedIn { get; set; } = false;
        private bool _isDonor { get; set; } = false;

        public InstallerPage(Int32 region)
        {
            Region = (FGORegion)region;
            _cm = DependencyService.Get<IContentManager>();
            _sm =  DependencyService.Get<IScriptManager>();
            _im = DependencyService.Get<IIntentService>();
            NavigationPage.SetHasNavigationBar(this, false);
            InitializeComponent();
            BindingContext = this;

            //TranslationName.Text = Region == FGORegion.Jp ? String.Format(AppResources.InstallerTitle, "JP") : String.Format(AppResources.InstallerTitle, "NA");

            RetryButton.Clicked += UpdateTranslationListClick;
            RefreshButton.Clicked += UpdateTranslationListClick;
            RevertButton.Clicked += RevertButtonOnClicked;
            Refresh.Refreshing += UpdateTranslationListClick;
        }

        private async void RevertButtonOnClicked(object sender, EventArgs e)
        {
            await Uninstall(Region);
        }

        public InstallerPage()
        {
            throw new Exception("InstallerPage requested without a region.");
        }

        /// <summary>
        /// Handshake the first time the view opens
        /// </summary>
        protected override async void OnAppearing()
        {
            switch (Region)
            {
                case FGORegion.Jp:
                    MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "jp_initial_load", async (sender) =>
                    {
                        await InitialLoad();
                    });
                    break;
                case FGORegion.Na:
                    MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "na_initial_load", async (sender) =>
                    {
                        await InitialLoad();
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MessagingCenter.Subscribe<Application>(Xamarin.Forms.Application.Current, "reset_initial_load", async (sender) =>
            {
                _pageOpened = false;
            });

            if (Region == FGORegion.Jp)
            {
                await InitialLoad();
            }
        }

        public async Task InitialLoad()
        {
            if (!_pageOpened)
            {
                _pageOpened = true;
                await UpdateTranslationList();
            }
        }

        public async void UpdateTranslationListClick(object obj, EventArgs args)
        {
            await UpdateTranslationList();
        }

        public async Task UpdateTranslationList()
        {
            _cm.ClearCache();
            if (_isCurrentlyUpdating) return;
            _isCurrentlyUpdating = true;

            try
            {
                ReleaseScheduleLayout.IsVisible = false;
                RetryButton.IsVisible = false;
                LoadingLayout.IsVisible = true;
                TranslationListView.IsVisible = false;
                MasterButtons.IsVisible = false;
                Refresh.IsRefreshing = true;
                //SwitchButtons(false);
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
                

                //TranslationName.Text = Region == FGORegion.Jp
                    //? String.Format(AppResources.InstallerTitle, "JP") + $": {handshake.Data.Response.AppVer}"
                    //: String.Format(AppResources.InstallerTitle, "NA") + $": {handshake.Data.Response.AppVer}";

                // Check region is installed
                if (_installedFgoInstances.All(w => w.Region != Region))
                {
                    LoadingText.Text = String.Format(AppResources.NoFGOFound,
                        $"Fate/Grand Order {Region.ToString().ToUpper()}");
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

                var instanceDict =
                    _installedFgoInstances.OrderByDescending(o => o.LastModified).FirstOrDefault(w => w.Region == Region);

                if (instanceDict == null)
                {
                    LoadingText.Text = String.Format(AppResources.NoFGOInstallationFound2,
                        $"Fate/Grand Order {Region.ToString().ToUpper()}");
                    SwitchErrorObjects(true);
                    return;
                }

                // GET SCRIPT LIST
                var rest = new RestfulAPI();
                var handshake = await rest.GetHandshakeApiResponse(Region, instanceDict.AssetStorage);
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

                    if (_isDonor)
                    {
                        MessagingCenter.Send(Xamarin.Forms.Application.Current, "add_art_tab_non_donor");
                    }
                    else
                    {
                        MessagingCenter.Send(Xamarin.Forms.Application.Current, "remove_art_tab_non_donor");
                    }
                }
                else
                {
                    _isLoggedIn = false;
                    _isDonor = false;
                }

                if (handshake.Data.Response.AssetStatus != HandshakeAssetStatus.Missing &&
                    handshake.Data.Response.AssetStatus != HandshakeAssetStatus.UpToDate)
                {
                    var warningTitle = AppResources.Warning + $" ({Region.ToString().ToUpper()})";
                    switch (handshake.Data.Response.AssetStatus)
                    {
                        case HandshakeAssetStatus.Missing:
                            break;
                        case HandshakeAssetStatus.UpToDate:
                            break;
                        case HandshakeAssetStatus.UpdateRequired:
                            await DisplayAlert(warningTitle, AppResources.AssetWarningOutOfDate, AppResources.OK);
                            break;
                        case HandshakeAssetStatus.TimeTraveler:
                            await DisplayAlert(warningTitle, AppResources.AssetWarningFutureUnreleased, AppResources.OK);
                            break;
                        case HandshakeAssetStatus.Unrecognized:
                            await DisplayAlert(warningTitle, AppResources.AssetWarningUnrecognised, AppResources.OK);
                            break;
                        case HandshakeAssetStatus.Corrupt:
                            await DisplayAlert(warningTitle, AppResources.AssetWarningCorrupted, AppResources.OK);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                var baseFilePath = _accessMode == ContentType.StorageFramework
                    ? $"files/data/d713/"
                    : $"{instanceDict.Path}/files/data/d713/";


                await ProcessAssets(_accessMode,
                    baseFilePath, instanceDict.Path, Region);

                // Add top bar
                if (_handshake.Response.LiveStatus != null && _handshake.Response.LiveStatus.Enabled)
                {
                    ReleaseScheduleLayout.IsVisible = true;
                    ReleaseScheduleChapter.Text = _handshake.Response.LiveStatus.CurrentRelease;
                    if (_handshake.Response.LiveStatus.NextReleaseDate < DateTime.UtcNow)
                    {
                        DisplayNextUpdateTime.IsVisible = false;
                    }
                    else
                    {
                        var timespan = _handshake.Response.LiveStatus.NextReleaseDate.Subtract(DateTime.UtcNow);
                        var lastUpdated = InstallerUtil.PeriodOfTimeOutput(timespan, 0, "");
                        ReleaseScheduleTimeRemaining.Text = lastUpdated;
                    }

                    ReleaseSchedulePercent.Text = _handshake.Response.LiveStatus.PercentDone;
                    ReleaseScheduleTitle.Text = _handshake.Response.LiveStatus.Title;

                    var announcementJson = Preferences.Get("AnnouncementData", null);

                    if (announcementJson != null)
                    {
                        var json =
                            JsonConvert.DeserializeObject<VersionAPIResponse.TranslationAnnouncements>(
                                announcementJson);

                        if (json.IsSpecialAnnouncement)
                        {
                            ReleaseTap.Tapped += OpenAnnouncementOnClicked;
                        }
                    }
                }


                LoadingLayout.IsVisible = false;
                TranslationListView.IsVisible = true;
                MasterButtons.IsVisible = true;
                Refresh.IsRefreshing = false;
                SwitchErrorObjects(false);
                ActivityIndicatorLoading.IsRunning = false;
                Refresh.IsEnabled = true;
                SwitchButtons(true);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
                _isCurrentlyUpdating = false;
                SwitchErrorObjects(true);
            }

            _isCurrentlyUpdating = false;
        }

        private TranslationList _installedBundle { get; set; }

        /// <summary>
        /// Processes valid assets to know which buttons to display
        /// </summary>
        /// <returns></returns>
        public async Task ProcessAssets(ContentType storageType, string pathToCheckWith, string storageLocationBase, FGORegion region)
        {
            var maxParallel = storageType == ContentType.Shizuku ? 1 : 8;
            var versionData = App.GetViewModel<InstallerPageModel>().Cache.Get<VersionAPIResponse.VersionUpdate>("VersionDetails");

            List<string> validSha = new List<string>();
            _translations?.Clear();
            _guiObjects?.Clear();

            var installedScriptString = Preferences.Get($"InstalledScript_{region}", null);

            if (installedScriptString != null)
            {
                _installedBundle = JsonConvert.DeserializeObject<TranslationList>(installedScriptString);

                foreach (var scriptBundle in _installedBundle.Scripts)
                {
                    validSha.Add(scriptBundle.Value.TranslatedSHA1); // Add existing
                }
            }
            else
            {
                _installedBundle = null;
            }

            foreach (var scriptBundleSet in _handshake.Response.Translations)
            {
                foreach (var scriptBundle in scriptBundleSet.Scripts)
                {
                    validSha.Add(scriptBundle.Value.TranslatedSHA1); // Add all valid sha1s
                }
            }

            if (_handshake.Response.Translations.Count > 0)
            {
                bool visibleExtras = versionData.FeaturesEnabled.HasFlag(EnabledTranslationFeatures.UI) ||
                                     (_isDonor &&
                                      versionData.FeaturesEnabled.HasFlag(EnabledTranslationFeatures.UIDonorOnly));

                foreach (var scriptBundleSet in _handshake.Response.Translations)
                {
                    string lastUpdated = "";
                    if (scriptBundleSet.Scripts.Count > 0)
                    {
                        ConcurrentBag<Tuple<long, long>> results = new ConcurrentBag<Tuple<long, long>>();

                        await scriptBundleSet.Scripts.ParallelForEachAsync(async scriptBundle =>
                        {
                            // Check hashes
                            var filePath = Path.Combine(pathToCheckWith, scriptBundle.Key);

                            var fileContentsResult =
                                await _cm.GetFileContents(storageType, filePath, storageLocationBase);

                            if (scriptBundle.Key.Contains('/') ||
                                scriptBundle.Key
                                    .Contains('\\')) // for security purposes, don't allow directory traversal
                            {
                                throw new FileNotFoundException();
                            }


                            TranslationFileStatus status;

                            var fileNotExists = fileContentsResult.Error == FileErrorCode.NotExists;
                            if (!fileNotExists)
                            {
                                var sha1 = ScriptUtil.Sha1(fileContentsResult
                                    .FileContents); // SHA of file currently in use

                                if (sha1 == scriptBundle.Value.GameSHA1) // Not modified
                                {
                                    status = TranslationFileStatus.NotModified;
                                }
                                else if (sha1 == scriptBundle.Value.TranslatedSHA1) // English is installed
                                {
                                    status = TranslationFileStatus.Translated;
                                }
                                else if (validSha.Contains(sha1) && (_installedBundle == null ||
                                                                     (_installedBundle != null &&
                                                                      scriptBundleSet.Group != _installedBundle.Group)))
                                {
                                    status = TranslationFileStatus.DifferentTranslation;
                                }
                                else if (_installedBundle != null && scriptBundleSet.Group == _installedBundle.Group)
                                {
                                    status = TranslationFileStatus.UpdateAvailable;
                                }
                                else
                                {
                                    status = TranslationFileStatus.Invalid;
                                }
                            }
                            else
                            {
                                status = TranslationFileStatus.Missing;
                            }

                            scriptBundle.Value.Status = status;

                            results.Add(new Tuple<long, long>(scriptBundle.Value.LastModified,
                                scriptBundle.Value.Size));
                        }, maxDegreeOfParallelism: maxParallel);

                        long lastModified = results.Max(m => m.Item1);
                        long totalSize = results.Sum(s => s.Item2);
                        scriptBundleSet.TotalSize = totalSize;
                        var timespan = DateTime.UtcNow.Subtract(DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds((long)lastModified).DateTime,
                            DateTimeKind.Utc));
                        lastUpdated = InstallerUtil.PeriodOfTimeOutput(timespan);
                    }

                    
                    _translations.Add(scriptBundleSet.Group, scriptBundleSet);
                    var statusString = InstallerUtil.GenerateStatusString(scriptBundleSet.Scripts);

                    bool enableButton = statusString.Item1 != AppResources.StatusInstalled // either not installed
                                        || (scriptBundleSet.HasExtraStage) // or installed
                                        || (scriptBundleSet.IsDonorOnly && !_isDonor); // or not donor and bundle is donor only (donor prompt)
                    var i1 = scriptBundleSet.Group;

                    if (!scriptBundleSet.Hidden && !(scriptBundleSet.HasExtraStage && !visibleExtras))
                    {

                        var command = !scriptBundleSet.IsDonorOnly // if not donor only
                            ? new Command(async () => await Install(region, i1), () => enableButton && ButtonsEnabled) // fine
                            : ((_isLoggedIn && _isDonor) // if donor only and we are donor
                                ? new Command(async () => await Install(region, i1), () => enableButton && ButtonsEnabled) // fine
                                : !_isLoggedIn // if not logged in
                                    ? new Command(async() => await ConnectAccount()) // log in
                                    : new Command(async() => await DonorPrompt())); // otherwise donate

                        var defaultInstallText = statusString.Item1 != AppResources.StatusInstalled
                            ? AppResources.InstallText
                            : (enableButton ? AppResources.Reinstall : AppResources.InstalledText);

                        var buttonText = scriptBundleSet.IsDonorOnly && !_isDonor 
                                ? (!_isLoggedIn ? AppResources.LoginPrompt : AppResources.DonorPrompt)
                            : defaultInstallText;

                        _guiObjects.Add(new TranslationGUIObject()
                        {
                            BundleID = scriptBundleSet.Group,
                            BundleHidden = scriptBundleSet.Hidden,
                            InstallEnabled = enableButton,
                            InstallClick = command,
                            Name = scriptBundleSet.Name,
                            Status = statusString.Item1,
                            TextColor = statusString.Item2,
                            LastUpdated = lastUpdated,
                            NotPromotional = !scriptBundleSet.IsDonorOnly || _isDonor,
                            ButtonInstallText = buttonText
                        });
                    }
                }

                LoadTranslationList();
            }
            else
            {
                LoadingText.Text = String.Format(AppResources.NoScriptsAvailable);
                SwitchErrorObjects(true);
                return;
            }
        }

        private async Task DonorPrompt()
        {
            var alert = DependencyService.Get<IAlert>();
            var answer = await alert.Display(AppResources.ConnectAccount, AppResources.ConnectAccountNotDonatedInfo, AppResources.DonateButton, AppResources.ConnectAccount, AppResources.Cancel);
            if (answer == AppResources.Cancel)
            {
                return;
            }

            if (answer == AppResources.DonateButton)
            {
                await Browser.OpenAsync("https://rayshift.io/donate");
            }
            else if (answer == AppResources.ConnectAccount)
            {
                await Browser.OpenAsync("https://rayshift.io/identity/account/manage/plus");
            }
        }

        private async Task ConnectAccount()
        {
            bool answer = await DisplayAlert(AppResources.ConnectAccount, AppResources.ConnectAccountInfo, AppResources.Yes, AppResources.No);
            if (!answer)
            {
                return;
            }
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "connect_rayshift_account");
        }

        public async Task Install(FGORegion region, int toInstall)
        {
            _cm.ClearCache();
            bool answer = await DisplayAlert(AppResources.Warning, String.Format(AppResources.InstallWarning, _translations[toInstall].TotalSize.Bytes().Humanize("#.## MB")), AppResources.Yes, AppResources.No);
            if (!answer)
            {
                return;
            }
            SwitchButtons(false);
            var rest = new RestfulAPI();
            var language = _handshake.Response.Translations.First(f => f.Group == toInstall).Language;
            Task successSendTask = null;
            try
            {
                var installResult = await _sm.InstallScript(
                    _accessMode,
                    region,
                    _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path).ToList(),
                    toInstall,
                    null,
                    _guiObjects
                );

                if (!installResult.IsSuccessful)
                {
                    SentrySdk.CaptureMessage(installResult.ErrorMessage, SentryLevel.Error);
                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        false, installResult.ErrorMessage, _accessMode == ContentType.StorageFramework);
                    await DisplayAlert(AppResources.Error,
                        installResult.ErrorMessage, AppResources.OK);

                }
                else
                {
                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        true, "", _accessMode == ContentType.StorageFramework);
                    await Task.Delay(1000);
                }

            }
            catch (System.UnauthorizedAccessException ex)
            {
                if (_accessMode != ContentType.StorageFramework)
                {

                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        false, "Unauthorized error handler: " + ex.ToString(), _accessMode == ContentType.StorageFramework);
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
                SentrySdk.CaptureException(ex);
                successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                    false, ex.ToString(), _accessMode == ContentType.StorageFramework);
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
            }

            await successSendTask;
            await UpdateTranslationList();
            //SwitchButtons(true);

        }

        public async Task Uninstall(FGORegion region)
        {
            _cm.ClearCache();
            bool answer = await DisplayAlert(AppResources.Warning, AppResources.UninstallWarning, AppResources.Yes, AppResources.No);
            if (!answer)
            {
                return;
            }
            SwitchButtons(false);
            var filesToRemove = new List<KeyValuePair<string, string>>();

            try
            {
                var extraRemoveJson = Preferences.Get($"UninstallPurgesExtras_{region}", "[]");
                var extraRemove = JsonConvert.DeserializeObject<List<string>>(extraRemoveJson);
                foreach (var game in _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path))
                {
                    foreach (var basePath in from script
                            in _translations
                        from script2
                            in script.Value.Scripts
                        select script2.Key
                        into file
                        select _accessMode == ContentType.StorageFramework
                            ? $"files/data/d713/{file}"
                            : $"{game}/files/data/d713/{file}")
                    {
                        filesToRemove.Add(new KeyValuePair<string, string>(basePath, game));
                    }

                    // remove extra files if needed
                    if (extraRemove != null && extraRemove.Count > 0)
                    {
                        foreach (var file in extraRemove)
                        {
                            var path = _accessMode == ContentType.StorageFramework ? $"files/data/{file}" : $"{game}/files/data/{file}";
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
                OnPropertyChanged();
                Preferences.Remove($"InstalledScript_{region}");
                Preferences.Remove($"UninstallPurgesExtras_{region}");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                await DisplayAlert(AppResources.InternalError,
                    String.Format(AppResources.InternalErrorDetails, ex.ToString()), AppResources.OK);
            }
            await UpdateTranslationList();
        }

        private bool ButtonsEnabled = true;

        public void LoadTranslationList()
        {
            TranslationListView.ItemsSource = _guiObjects;
            SwitchButtons(true);
            MasterButtons.IsVisible = true;
        }

        public void SwitchButtons(bool status)
        {
            ButtonsEnabled = status;


            foreach (var button in _guiObjects)
            {
                button.InstallEnabled = status;
                button.InstallClick.ChangeCanExecute();
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

        private async void OpenAnnouncementOnClicked(object sender, EventArgs e)
        {
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "installer_page_reopen_announcement");
        }
        public class TranslationGUIObject: INotifyPropertyChanged
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
            public Command InstallClick { get; set; }
            public bool InstallEnabled { get; set; }
            public string LastUpdated { get; set; }
            public int BundleID { get; set; }

            public string ButtonInstallText
            {
                get => _buttonInstallText;
                set
                {
                    _buttonInstallText = value;
                    RaisePropertyChanged(nameof(ButtonInstallText));
                }
            }

            public bool BundleHidden { get; set; } = false;
            public bool NotPromotional { get; set; } = true;
            public bool Promotional => !NotPromotional;
            public bool NoUpdateTimestamp => !(Promotional || string.IsNullOrWhiteSpace(LastUpdated));

            private string _buttonInstallText = AppResources.InstallText;
            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}