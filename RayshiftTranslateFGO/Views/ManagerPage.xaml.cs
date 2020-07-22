using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Text.Method;
using Humanizer;
using Java.Util;
using Newtonsoft.Json;
using RayshiftTranslateFGO.Models;
using RayshiftTranslateFGO.Services;
using RayshiftTranslateFGO.Util;
using RayshiftTranslateFGO.ViewModels;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Browser = Xamarin.Essentials.Browser;

namespace RayshiftTranslateFGO.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ManagerPage : ContentPage
    {
        private bool _pageOpened = false;
        private HandshakeAPIResponse _handshake;
        private readonly Dictionary<int, TranslationList> _translations = new Dictionary<int, TranslationList>();
        private readonly ObservableCollection<TranslationGUIObject> _guiObjects = new ObservableCollection<TranslationGUIObject>();
        private readonly Dictionary<int, bool> _assetSubmitRequired = new Dictionary<int, bool>();

        public const string _assetList = "cfb1d36393fd67385e046b084b7cf7ed";

        private TranslationList installedBundle { get; set; }

        private bool _firstLoad = true;

        public ManagerPage()
        {

            InitializeComponent();
            EnableButtons(false);
            RetryButton.Clicked += HandshakeClick;
            RefreshButton.Clicked += HandshakeClick;
            Refresh.Refreshing += HandshakeClick;
        }
        
        /// <summary>
        /// Handshake the first time the view opens
        /// </summary>
        protected override async void OnAppearing()
        {
            if (!_pageOpened)
            {
                _pageOpened = true;
                await Handshake();
            }
        }

        public async void HandshakeClick(object obj, EventArgs args)
        {
            await Handshake();
        }

        public async Task SendHandshake()
        {
            var rest = new RestfulAPI();
            var handshake = await rest.GetHandshakeApiResponse();
            _handshake = handshake;

            // Check for valid status
            if (handshake == null || handshake.Status != 200)
            {
                LoadingText.Text = handshake == null ? $"Website down or unresponsive, please try again later." : $"An error has occurred:\n{handshake?.Message}";

                LoadingText.HorizontalTextAlignment = TextAlignment.Center;
                ActivityIndicatorLoading.IsRunning = false;
                RetryButton.IsVisible = true;
                EnableButtons();
                return;
            }

            // Check for app updates
            var currentVersion = ScriptUtil.GetBuild();
            if (int.Parse(handshake.Response.UpdateVer) > currentVersion)
            {
                await ShowMessage("Update required", $"An update from version {currentVersion} to {handshake.Response.UpdateVer} is required to use the application.", "Exit", async () =>
                {
                    await Xamarin.Essentials.Browser.OpenAsync("https://github.com/rayshift/translatefgo/releases");
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                });
                return;
            }

            // Display main layout
            LoadingLayout.IsVisible = false;
            ManagerLayout.IsVisible = true;
            Refresh.IsRefreshing = false;

            AppVersion.Text = handshake.Response.AppVer;

            var existingFateApp = Android.App.Application.Context.PackageManager
                .GetInstalledApplications(PackageInfoFlags.MatchAll)
                .FirstOrDefault(x => x.PackageName == "com.aniplex.fategrandorder");

            if (existingFateApp == null)
            {
                AppVersionInstalled.Text = "not installed";
                AppVersionInstalled.TextColor = Color.Crimson;
                ManagerError.Text =
                    "Your app version is out of date or you haven't installed Fate/Grand Order.";
                ManagerError.IsVisible = true;
                EnableButtons();
                return;
            }
            else
            {
                var packageVersion = Android.App.Application.Context.PackageManager.GetPackageInfo("com.aniplex.fategrandorder", 0).VersionName;
                bool valid = ScriptUtil.IsValidAppVersion(handshake.Response.AppVer, packageVersion);
                if (valid)
                {
                    AppVersionInstalled.Text = packageVersion;
                    AppVersionInstalled.TextColor = Color.LimeGreen;
                }
                else
                {
                    AppVersionInstalled.Text = packageVersion;
                    AppVersionInstalled.TextColor = Color.Crimson;
                    ManagerError.Text =
                        "Your app version is out of date or you haven't installed Fate/Grand Order.";
                    ManagerError.IsVisible = true;
                    EnableButtons();
                    return;
                }
            }

            var externalPath = System.IO.Directory.GetParent(Android.App.Application.Context.GetExternalFilesDir(null).Parent);
            if (await Permissions.CheckStatusAsync<Permissions.StorageWrite>() == PermissionStatus.Granted && externalPath.Exists)
            {
                var assetPath = Path.Combine(externalPath.ToString(), "com.aniplex.fategrandorder/files/data/d713/");
                if (System.IO.Directory.Exists(assetPath))
                {
                    var assetStorage = Path.Combine(assetPath, _assetList);
                    if (File.Exists(assetStorage))
                    {
                        using var testFs = new System.IO.FileStream(assetStorage, FileMode.Open);
                        if (testFs.CanRead && testFs.CanWrite)
                        {
                            ProgramStatus.Text = "ready";
                            ProgramStatus.TextColor = Color.LimeGreen;
                            if (_firstLoad)
                            {
                                RevertButton.Clicked += async (object obj, EventArgs args) =>
                                    await Uninstall(assetPath);
                                _firstLoad = false;
                            }

                            try
                            {
                                await ProcessAssets(assetPath);
                            }
                            catch (Exception ex)
                            {
                                await DisplayAlert("Error", $"An exception has occurred:\n{ex.ToString()}", "OK");
                                throw;
                            }

                            return; 
                        }
                        ProgramStatus.Text = "no write access";
                        ProgramStatus.TextColor = Color.Crimson;
                        
                    }
                    else
                    {
                        ProgramStatus.Text = "empty";
                        ProgramStatus.TextColor = Color.Crimson;
                    }
                }
                else
                {
                    ProgramStatus.Text = "unreadable or missing";
                    ProgramStatus.TextColor = Color.Crimson;
                }
            }
            else
            {
                ProgramStatus.Text = "missing sd card directory";
                ProgramStatus.TextColor = Color.Crimson;
            }

            ManagerError.Text =
                "An error has occurred trying to access the storage for Fate/Grand Order. Check the GitHub wiki for solutions.";
            ManagerError.HorizontalTextAlignment = TextAlignment.Center;
            ManagerError.IsVisible = true;
            WikiButton.Command = new Command(async () => await Browser.OpenAsync("https://github.com/rayshift/translatefgo/wiki/Troubleshooting"),
                () => true);
            WikiButton.IsVisible = true;
            EnableButtons();
        }

        /// <summary>
        /// Processes valid assets to know which buttons to display
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task ProcessAssets(string path)
        {
            List<string> _validSha = new List<string>();

            var installedScriptString = Preferences.Get("InstalledScript", null);

            if (installedScriptString != null)
            {
                installedBundle = JsonConvert.DeserializeObject<TranslationList>(installedScriptString);
                
                foreach (var scriptBundle in installedBundle.Scripts)
                {
                    _validSha.Add(scriptBundle.Value.TranslatedSHA1); // Add existing
                }
            }

            foreach (var scriptBundleSet in _handshake.Response.Translations)
            {
                foreach (var scriptBundle in scriptBundleSet.Scripts)
                {
                    _validSha.Add(scriptBundle.Value.TranslatedSHA1); // Add all valid sha1s
                }
            }

            if (_handshake.Response.Translations.Count > 0)
            {
                var i = 0;
                foreach (var scriptBundleSet in _handshake.Response.Translations)
                {
                    long lastModified = 0;
                    long totalSize = 0;
                    foreach (var scriptBundle in scriptBundleSet.Scripts)
                    {
                        // Check hashes
                        var filename = Path.Combine(path, scriptBundle.Key);

                        if (scriptBundle.Key.Contains('/') || scriptBundle.Key.Contains('\\')) // for security purposes, don't allow directory traversal
                        {
                            throw new FileNotFoundException();
                        }

                        totalSize += scriptBundle.Value.Size;

                        TranslationFileStatus status;
                        if (File.Exists(filename))
                        {
                            using var file = File.OpenRead(filename);
                            var sha1 = ScriptUtil.Sha1(file); // SHA of file currently in use

                            if (sha1 == scriptBundle.Value.GameSHA1) // Not modified
                            {
                                status = TranslationFileStatus.NotModified;
                            }
                            else if (sha1 == scriptBundle.Value.TranslatedSHA1) // English is installed
                            {
                                status = TranslationFileStatus.Translated;
                            }
                            else if (_validSha.Contains(sha1) && (installedBundle == null || (installedBundle != null && scriptBundleSet.Group != installedBundle.Group)))
                            {
                                status = TranslationFileStatus.DifferentTranslation;
                            }
                            else if (installedBundle != null && scriptBundleSet.Group == installedBundle.Group)
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

                        if (scriptBundle.Value.LastModified > lastModified)
                        {
                            lastModified = scriptBundle.Value.LastModified;
                        }
                    }

                    scriptBundleSet.TotalSize = totalSize;
                    _translations.Add(i, scriptBundleSet);
                    var statusString = GenerateStatusString(scriptBundleSet.Scripts, i, scriptBundleSet.Group);
                    var timespan = DateTime.Now.Subtract(DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds((long)lastModified).DateTime,
                        DateTimeKind.Utc));
                    var lastUpdated = PeriodOfTimeOutput(timespan);
                    bool enableButton = statusString.Item1 != "installed";
                    var i1 = i;
                    _guiObjects.Add(new TranslationGUIObject()
                    {
                        BundleID = i,
                        InstallEnabled = enableButton,
                        InstallClick = new Command(async () => await Install(i1, path), () => enableButton && ButtonsEnabled),
                        Name = scriptBundleSet.Name,
                        Status = statusString.Item1,
                        TextColor = statusString.Item2,
                        LastUpdated = lastUpdated
                    });
                    i += 1;
                }

                await LoadTranslationList();
            }
            else
            {
                ManagerError.Text =
                    "No scripts are currently available, please try again later.";
                ManagerError.HorizontalTextAlignment = TextAlignment.Center;
                ManagerError.IsVisible = true;
                EnableButtons();
                return;
            }
        }

        private string MismatchLabel = "update required - ensure game has been launched before updating";

        private Tuple<string, Color> GenerateStatusString(IDictionary<string, TranslationHandshakeList> translationList, int index, int group)
        {
            int translatedScripts = 0;
            int missingScripts = 0;
            int notModified = 0;
            int differentTranslation = 0;
            foreach (var item in translationList)
            {
                var status = item.Value.Status;
                switch (status)
                {
                    case TranslationFileStatus.UpdateAvailable:
                        return new Tuple<string, Color>("update available", Color.Coral);
                    case TranslationFileStatus.NotModified:
                        notModified += 1;
                        break;
                    case TranslationFileStatus.DifferentTranslation: // Different translation installed
                        return new Tuple<string, Color>("not installed", Color.Crimson);
                    case TranslationFileStatus.Missing: // Some scripts are missing
                        missingScripts += 1;
                        break;
                    case TranslationFileStatus.Translated:
                        translatedScripts += 1;
                        break;
                    case TranslationFileStatus.Invalid: // Player hasn't launched their game yet
                        _assetSubmitRequired[index] = true;
                        return new Tuple<string, Color>(MismatchLabel, Color.Crimson);
                    case TranslationFileStatus.Default: // This shouldn't happen
                    default:
                        return new Tuple<string, Color>("error, try reinstalling", Color.Crimson);
                }
            }

            if (missingScripts > 0)
            {
                _assetSubmitRequired[index] = true;
            }

            if (translatedScripts == translationList.Count) // Installed
            {
                return new Tuple<string, Color>("installed", Color.LimeGreen);
            }

            if (missingScripts == translationList.Count || notModified == translationList.Count || differentTranslation == translationList.Count) // Nothing installed
            {
                return new Tuple<string, Color>("not installed", Color.Crimson);
            }

            if (translatedScripts > 0 && Math.Abs(notModified - translatedScripts) == translationList.Count) // One or more scripts have changed, requiring an update
            {
                return new Tuple<string, Color>("update required", Color.Crimson);
            }

            _assetSubmitRequired[index] = true;
            return new Tuple<string, Color>("unknown state, try reinstalling", Color.Crimson);
        }

        public async Task LoadTranslationList()
        {
            TranslationListView.ItemsSource = _guiObjects;
            EnableButtons();
            MasterButtons.IsVisible = true;
            await Task.Delay(1);
        }

        /// <summary>
        /// Install
        /// </summary>
        /// <param name="typeToInstall">Type to install</param>
        /// <param name="assetPath">Folder location on disk</param>
        /// <returns></returns>
        public async Task Install(int toInstall, string assetPath)
        {
            // Warn user
            bool answer = await DisplayAlert("Warning", $"You are about to install a set of scripts to your game.\nEnsure you have made and written down your bind code before using this application.\n\nTotal download size: {_translations[toInstall].TotalSize.Bytes().Humanize("#.## MB")}\n\nDo you want to continue?", "Yes", "No");
            if (!answer)
            {
                return;
            }

            ProgramStatus.Text = "installing...";
            EnableButtons(false);
            ProgramStatus.TextColor = Color.Chocolate;

            var filesModified = false;
            try
            {
                Dictionary<string, byte[]> filesToWrite = new Dictionary<string, byte[]>();
                var rs = new RestfulAPI();

                // If we need to modify the asset list - ie. the player has never loaded a cutscene from one or more files to install
                // Seems this breaks after updates because the old files are still on disk but dl status set to 0 in asset list - maybe comparing sha1 would fix - TODO
                //if (_assetSubmitRequired.ContainsKey(toInstall) && _assetSubmitRequired[toInstall])
                //{
                    ProgramStatus.Text = "grabbing new asset list...";
                    var assetStorage = Path.Combine(assetPath, _assetList);
                    if (File.Exists(assetStorage))
                    {
                        string assetList = File.ReadAllText(assetStorage);
                        var list = await rs.SendAssetList(assetList, _translations[toInstall].Group);

                        if (list.Status != 200)
                        {
                            throw new Exception($"API failure, please retry again later.\nCode: {list.Status}\nMessage: {list.Message}");
                        }
                        filesToWrite.Add(_assetList, Encoding.ASCII.GetBytes(list.Response["data"]));
                    }
                    else
                    {
                        throw new Exception("Asset storage doesn't exist any more.");
                    }
                //}

                // Get files to write
                var items = _translations[toInstall].Scripts.ToList();
                for (var i = 0; i < _translations[toInstall].Scripts.Count; i++)
                {
                    ProgramStatus.Text = $"downloading script {i+1} of {_translations[toInstall].Scripts.Count}...";
                    var downloadUrl = items[i].Value.DownloadURL;
                    var script = await rs.GetScript(downloadUrl);

                    if (script == null)
                    {
                        throw new Exception($"Empty file.\nDownload URL: {downloadUrl}");
                    }

                    var scriptSha = ScriptUtil.Sha1(script);

                    if (scriptSha != items[i].Value.TranslatedSHA1)
                    {
                        throw new Exception($"Checksum failure.\nReal file length: {script.Length}\nDownload URL: {downloadUrl}");
                    }
                    filesToWrite.Add(items[i].Key, script);
                }

                filesModified = true;
                // Write files
                ProgramStatus.Text = $"copying replacement files...";

                foreach (var file in filesToWrite)
                {
                    File.WriteAllBytes(Path.Combine(assetPath, file.Key), file.Value);
                }

                Preferences.Set("InstalledScript", JsonConvert.SerializeObject(_translations[toInstall]));  // SimpleJSON is broken here q_q

                ProgramStatus.Text = $"done";
                ProgramStatus.TextColor = Color.LimeGreen;
                await DisplayAlert("Finished", "Successfully installed.", "OK");
            }
            catch (Exception ex)
            {
                ProgramStatus.Text = "error";
                ProgramStatus.TextColor = Color.Crimson;
                if (!filesModified)
                {
                    await DisplayAlert("Error",
                        $"An error has occurred, please try again later, or report to GitHub if this persists.\nNo files were modified.\n\n{ex}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Error",
                        $"An error has occurred, please try again later, or report to GitHub if this persists.\nFiles may have been modified, reload this app and click \"Remove All\" if something broke, or see the wiki for manual removal.\n\n{ex}",
                        "OK");
                }

                await Task.Delay(2000);
                await Handshake();
                return;
            }

            await Task.Delay(2000);
            await Handshake();
        }

        /// <summary>
        /// Uninstall
        /// </summary>
        /// <param name="assetPath">Directory on disk</param>
        /// <returns></returns>
        public async Task Uninstall(string assetPath)
        {
            // Warn user
            bool answer = await DisplayAlert("Warning", "This will remove any altered scripts from your game.\nEnsure you have made and written down your bind code before using this application.\n\nDo you want to continue?", "Yes", "No");
            if (!answer)
            {
                return;
            }

            EnableButtons(false);

            try
            {
                List<string> files = new List<string>();
                foreach (var bundle in _translations)
                {
                    foreach (var translations in bundle.Value.Scripts)
                    {
                        if (translations.Key.Contains('/') || translations.Key.Contains('\\')) // for security purposes, don't allow directory traversal
                        {
                            throw new FileNotFoundException();
                        }
                        if (!files.Contains(translations.Key))
                        {
                            files.Add(translations.Key);
                        }
                    }
                }

                ProgramStatus.Text = $"checking {files.Count} files...";
                ProgramStatus.TextColor = Color.Chocolate;

                await Task.Delay(1000); // enough time to read the status

                int deletedFiles = 0;

                foreach (var file in files)
                {
                    var filePath = Path.Combine(assetPath, file);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath); // game will re-download them
                        deletedFiles += 1;
                    }
                }

                Preferences.Remove("InstalledScript");

                ProgramStatus.Text = $"done, {deletedFiles} files cleaned up";
                ProgramStatus.TextColor = Color.LimeGreen;
            }
            catch (Exception ex)
            {
                ProgramStatus.Text = "error";
                ProgramStatus.TextColor = Color.Crimson;

                await DisplayAlert("Error",
                    $"An error has occurred, please try again later, or report to GitHub if this persists.\nNo files were modified.\n\n{ex}",
                    "OK");

                await Task.Delay(2000);
                await Handshake();
                return;
            }

            await Task.Delay(2000);
            await Handshake();
        }

        public void EnableButtons(bool enable=true)
        { // Bindings aren't working at all for me with buttons, and multibindings don't exist anyway
            RevertButton.IsEnabled = enable;
            RefreshButton.IsEnabled = enable;
            ButtonsEnabled = enable;
            RetryButton.IsEnabled = enable;
            Refresh.IsEnabled = enable;
            if (!enable)
            {
                foreach (var button in _guiObjects)
                {
                    button.InstallEnabled = false;
                    button.InstallClick.ChangeCanExecute();
                }
            }

        }

        public bool ButtonsEnabled { get; set; } = false;

        public class TranslationGUIObject
        {
            public string Name { get; set; }
            public string Status { get; set; }

            public Color TextColor { get; set; }
            public Command InstallClick { get; set; }
            public bool InstallEnabled { get; set; }
            public string LastUpdated { get; set; }
            public int BundleID { get; set; }
        }

        /// <summary>
        /// UI handling for handshake
        /// </summary>
        /// <returns></returns>
        public async Task Handshake()
        {
            EnableButtons(false);
            ManagerError.IsVisible = false;
            WikiButton.IsVisible = false;
            MasterButtons.IsVisible = false;

            ManagerLayout.IsVisible = false;
            LoadingLayout.IsVisible = true;
            RetryButton.IsVisible = false;
            _assetSubmitRequired.Clear();
            _translations.Clear();
            _guiObjects.Clear();

            ActivityIndicatorLoading.IsRunning = true;
            ActivityIndicatorLoading.VerticalOptions = LayoutOptions.Center;
            ActivityIndicatorLoading.HorizontalOptions = LayoutOptions.Center;
            
            LoadingText.Text = "Loading, please wait...";
            LoadingText.VerticalOptions = LayoutOptions.CenterAndExpand;
            LoadingText.HorizontalOptions = LayoutOptions.CenterAndExpand;
            try
            {
                await SendHandshake();
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"An exception occurred, please report to GitHub:\n{ex}";
                LoadingText.HorizontalTextAlignment = TextAlignment.Center;
                ActivityIndicatorLoading.IsRunning = false;
                RetryButton.IsVisible = true;
                Refresh.IsRefreshing = false;
                EnableButtons();
            }
        }

        /// <summary>
        /// Show alert with callback
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="buttonText"></param>
        /// <param name="afterHideCallback">Callback function</param>
        /// <returns></returns>
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

        private string PeriodOfTimeOutput(TimeSpan tspan, int level = 0)
        {
            string how_long_ago = "ago";
            if (level >= 2) return how_long_ago;
            if (tspan.Days > 1)
                how_long_ago = $"{tspan.Days} days ago";
            else if (tspan.Days == 1)
                how_long_ago =
                    $"1 day {PeriodOfTimeOutput(new TimeSpan(tspan.Hours, tspan.Minutes, tspan.Seconds), level + 1)}";
            else if (tspan.Hours >= 1)
                how_long_ago =
                    $"{tspan.Hours} {((tspan.Hours > 1) ? "hours" : "hour")} {PeriodOfTimeOutput(new TimeSpan(0, tspan.Minutes, tspan.Seconds), level + 1)}";
            else if (tspan.Minutes >= 1)
                how_long_ago =
                    $"{tspan.Minutes} {((tspan.Minutes > 1) ? "minutes" : "minute")} {PeriodOfTimeOutput(new TimeSpan(0, 0, tspan.Seconds), level + 1)}";
            else if (tspan.Seconds >= 1)
                how_long_ago = $"{tspan.Seconds} {((tspan.Seconds > 1) ? "seconds" : "second")} ago";
            return how_long_ago;
        }
    }
}