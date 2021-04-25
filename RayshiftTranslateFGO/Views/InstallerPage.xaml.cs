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

        private bool _android11Access = false;
        private string _storageLocation = "";

        private bool _isCurrentlyUpdating = false;

        private IContentManager _cm { get; set; }
        private IScriptManager _sm { get; set; }

        public InstallerPage(Int32 region)
        {
            Region = (FGORegion)region;
            _cm = DependencyService.Get<IContentManager>();
            _sm =  DependencyService.Get<IScriptManager>();
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


                _storageLocation = Preferences.Get("StorageLocation", "");

                if (!string.IsNullOrEmpty(_storageLocation) || !_cm.CheckBasicAccess())
                {
                    _android11Access = true;

                }

                if (!_android11Access)
                {
                    _installedFgoInstances = _cm.GetInstalledGameApps(ContentType.DirectAccess);
                }
                else
                {
                    _installedFgoInstances = _cm.GetInstalledGameApps(ContentType.StorageFramework, _storageLocation);
                }

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
                    var filePath = _android11Access
                        ? $"data/{instance.Path}/files/data/d713/{_assetList}"
                        : $"{instance.Path}/files/data/d713/{_assetList}";
                    var assetStorage = await _cm.GetFileContents(
                        _android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                        filePath, _storageLocation);


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

                var baseFilePath = _android11Access
                    ? $"data/{instanceDict.Path}/files/data/d713/"
                    : $"{instanceDict.Path}/files/data/d713/";


                await ProcessAssets(_android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                    baseFilePath, _storageLocation, Region);


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
                foreach (var scriptBundleSet in _handshake.Response.Translations)
                {
                    ConcurrentBag<Tuple<long,long>> results = new ConcurrentBag<Tuple<long, long>>();

                    await scriptBundleSet.Scripts.ParallelForEachAsync(async scriptBundle =>
                    {
                        // Check hashes
                        var filePath = Path.Combine(pathToCheckWith, scriptBundle.Key);

                        var fileContentsResult = await _cm.GetFileContents(storageType, filePath, storageLocationBase);

                        if (scriptBundle.Key.Contains('/') || scriptBundle.Key.Contains('\\')) // for security purposes, don't allow directory traversal
                        {
                            throw new FileNotFoundException();
                        }


                        TranslationFileStatus status;

                        var fileNotExists = fileContentsResult.Error == FileErrorCode.NotExists;
                        if (!fileNotExists)
                        {
                            var sha1 = ScriptUtil.Sha1(fileContentsResult.FileContents); // SHA of file currently in use

                            if (sha1 == scriptBundle.Value.GameSHA1) // Not modified
                            {
                                status = TranslationFileStatus.NotModified;
                            }
                            else if (sha1 == scriptBundle.Value.TranslatedSHA1) // English is installed
                            {
                                status = TranslationFileStatus.Translated;
                            }
                            else if (validSha.Contains(sha1) && (_installedBundle == null || (_installedBundle != null && scriptBundleSet.Group != _installedBundle.Group)))
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

                        results.Add(new Tuple<long, long>(scriptBundle.Value.LastModified, scriptBundle.Value.Size));
                    }, maxDegreeOfParallelism:4);

                    long lastModified = results.Max(m => m.Item1);
                    long totalSize = results.Sum(s => s.Item2);

                    scriptBundleSet.TotalSize = totalSize;
                    _translations.Add(scriptBundleSet.Group, scriptBundleSet);
                    var statusString = InstallerUtil.GenerateStatusString(scriptBundleSet.Scripts);
                    var timespan = DateTime.UtcNow.Subtract(DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds((long)lastModified).DateTime,
                        DateTimeKind.Utc));
                    var lastUpdated = InstallerUtil.PeriodOfTimeOutput(timespan);
                    bool enableButton = statusString.Item1 != AppResources.StatusInstalled;
                    var i1 = scriptBundleSet.Group;

                    _guiObjects.Add(new TranslationGUIObject()
                    {
                        BundleID = scriptBundleSet.Group,
                        InstallEnabled = enableButton,
                        InstallClick = new Command(async () => await Install(region, i1), () => enableButton && ButtonsEnabled),
                        Name = scriptBundleSet.Name,
                        Status = statusString.Item1,
                        TextColor = statusString.Item2,
                        LastUpdated = lastUpdated,
                        ButtonInstallText = statusString.Item1 != AppResources.StatusInstalled ? AppResources.InstallText : AppResources.InstalledText
                    });
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
                    _android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                    region,
                    _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path).ToList(),
                    _storageLocation,
                    toInstall,
                    null,
                    _guiObjects
                );

                if (!installResult.IsSuccessful)
                {
                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        false, installResult.ErrorMessage, _android11Access);
                    await DisplayAlert(AppResources.Error,
                        installResult.ErrorMessage, AppResources.OK);

                }
                else
                {
                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        true, "", _android11Access);
                    await Task.Delay(1000);
                }

            }
            catch (System.UnauthorizedAccessException ex)
            {
                if (!_android11Access)
                {

                    successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                        false, "Unauthorized error handler: " + ex.ToString(), _android11Access);
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
                successSendTask = rest.SendSuccess(region, language, TranslationInstallType.Manual, toInstall,
                    false, ex.ToString(), _android11Access);
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
            HashSet<string> filesToRemove = new HashSet<string>();

            try
            {
                foreach (var game in _installedFgoInstances.Where(w => w.Region == region).Select(s => s.Path))
                {
                    foreach (var basePath in from script
                            in _translations
                        from script2
                            in script.Value.Scripts
                        select script2.Key
                        into file
                        select _android11Access
                            ? $"data/{game}/files/data/d713/{file}"
                            : $"{game}/files/data/d713/{file}")
                    {
                        filesToRemove.Add(basePath);
                    }
                }

                if (filesToRemove.Count == 0)
                {
                    return;
                }


                int i = 0;
                foreach (var file in filesToRemove)
                {
                    i += 1;
                    RevertButton.Text = String.Format(AppResources.UninstallingText, i, filesToRemove.Count);
                    OnPropertyChanged();
                    await _cm.RemoveFileIfExists(_android11Access ? ContentType.StorageFramework : ContentType.DirectAccess,
                        file, _storageLocation);
                }

                RevertButton.Text = AppResources.UninstallFinished;
                OnPropertyChanged();
                Preferences.Remove($"InstalledScript_{region}");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
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