## Rayshift.io Translate Fate/Grand Order
[![GitHub Release](https://img.shields.io/github/release/rayshift/translatefgo.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![Github All Releases](https://img.shields.io/github/downloads/rayshift/translatefgo/total.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![license: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](http://creativecommons.org/licenses/by-nc-sa/4.0/) [![Discord Chat](https://img.shields.io/discord/665980614998097941.svg)](https://discord.gg/6vncnjj)  

This application translates cutscenes in Fate/Grand Order JP into a variety of different languages. It features real time machine translation of the most recent event using [DeepL](https://www.deepl.com/). Older content uses the official translation from the North American region. Teams of translators are working on manual translations in various different languages.

![Example](https://i.imgur.com/dNLFbxG.png)

If the text is too fast or slow, you can change text speed in the [game settings](https://i.imgur.com/UhmoZI9.png). (Top = text speed, bottom = scroll speed)

Currently, English is supported for Fuyuki to LB1 (including events older than 2 years), an English machine translation is available for Gudaguda 4 and Requiem.

A spanish translation for Fuyuki and Orleans is also available.

### Installation
Requirements: Android 5, 6, 7, 8, 9, 10 are supported. Root is not required.

1. Ensure you have the latest Fate/Grand Order JP installed.
2. Log into Fate/Grand Order JP to ensure your game data is up to date. Afterwards, close the application.
3. Download and install the latest APK from the [releases page](https://github.com/rayshift/translatefgo/releases).
4. Click "install" below the language you want to install.
5. Reopen Fate/Grand Order.

#### Emulator support
Only Nox is supported officially, Bluestacks is completely incompatible, other emulators may have mixed results.

#### Updates
The scripts will update on a regular basis. Please keep the app installed and check it regularly - the "installed" status will change to "update available" when an update is ready.

To update after new content is added, you must do the following:
1. Open Fate/Grand Order and accept the content download.
2. Close Fate/Grand Order and click "Update" in the translation app.
3. Reopen Fate/Grand Order.

#### Uninstallation
1. Close Fate/Grand Order.
2. Open the application and click "Uninstall".
3. Reopen Fate/Grand Order.
4. You can also clear the cache in-game if you cannot use the application.

#### Troubleshooting
Please check our [troubleshooting wiki](https://github.com/rayshift/translatefgo/wiki/Troubleshooting) for help with issues. Alternatively, visit the [discord server](https://discord.gg/6vncnjj) for help.

### Is this safe, will I get banned?
As of patch 2.13.4, **yes, it is safe, no, you will not be banned**, for a number of reasons. 

1. The game does not and cannot detect anything this application does.
2. This application does not violate the current [terms of service](http://anonym.es/?http://webview.fate-go.jp/webview/userpolicy/index.html), which only specifically prevents modification of the APK. This application does not do that. Instead, it modifies cached text assets on your SD card. 
Note: Account suspension notices do mention that modifying game data is not allowed, however this is not reflected properly in the terms of service. You use this at your own risk, but please refer back to the first point in particular.
3. It is not in the interests of the game's developers to ban people translating their game - it does not give an unfair advantage like cheating. Many other games have had similar modifications in use for years without issues. 

Ultimately, you use this tool at your own discretion - we are not liable for any issues that could arise. Ensure you always have a bind code active. Ensure you only download the application from this repository or rayshift.io - do not trust other sources.

### Does this work on NA?
For the purposes of an English to other language translation - no, this is not supported at this time, but can be explored after the engine update in June.

### Contact Information
This tool operates in good faith to increase player accessibility to Fate/Grand Order. Please use the GitHub issues section for bug reports and suggestions. Should you wish to privately contact the creator of this application, please email webmaster at rayshift.io.

## Build Instructions
Should you wish to build the android app yourself, please follow these instructions.

### Prerequisites
#### Windows: 
Visual Studio 2019 with the Xamarin application framework installed.

#### Linux:
Install the [mono development framework](https://www.mono-project.com/download/stable/#download-lin) and build [the Xamarin framework](https://github.com/xamarin/xamarin-android/blob/master/Documentation/building/unix/instructions.md).

### Building
Run commands similar to these, changing the paths as required:
```
msbuild /t:clean RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj
msbuild /t:restore RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj
msbuild /p:Configuration=Release /t:PackageForAndroid RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj /p:AndroidSdkDirectory=/path/to/android-toolchain/sdk
```

You then need to sign and zipalign your APK (both applications are part of the Android SDK Tools):
```
jarsigner -verbose -sigalg SHA1withRSA -digestalg SHA1 -storepass passwordhere -keystore /path/to/the.keystore RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo.apk alias_name
zipalign -f -v -p 4 RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo.apk RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo-release.apk
```

## Contributing
Please ensure your contribution is approved by first submitting a github issue with the topic of feature request and waiting for approval. Approval is not needed for fixing open bugs, but please notify others you are working on an issue first.

Please note all code relating to bundle creation, translation and the API is private and will not be made available. This app only functions as an installer for those pre-created bundles.

### API
You can mock API responses by adding a file to `Assets/Mock/{ResponseType}.txt` with build type "copy", where `ResponseType` is the type passed to `RestfulApi.ExecuteAsync` (for example, AssetListAPIResponse.txt). You also need to set `Mock = true;` in Services/RestfulAPI.cs.

If the change you are suggesting requires an API change, as the API is not open source, please clearly state what changes need to be made in your pull request.

### Pull requests
1. Ensure code written is compatible with .NET Standard 2.0.
2. Test your changes in an android emulator or on a real device.
3. Do not increase version numbers in your pull request. We will merge into a development branch and increase the version numbers before release.
4. Submit a new pull request.

## Licence
The code in this repository, the bundles from our API, and any android applications released are licenced under a [Creative Commons Attribution-NonCommercial-ShareAlike 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) licence. This means it is forbidden to use or distribute any version of the application or any translated bundle for commercial purposes.
