## Rayshift.io Translate Fate/Grand Order
[![GitHub Release](https://img.shields.io/github/release/rayshift/translatefgo.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![Github All Releases](https://img.shields.io/github/downloads/rayshift/translatefgo/total.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![license: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](http://creativecommons.org/licenses/by-nc-sa/4.0/) [![Discord Chat](https://img.shields.io/discord/665980614998097941.svg)](https://discord.gg/6vncnjj)  

This application translates cutscenes in Fate/Grand Order JP & NA into a variety of different languages. It features real time machine translation of the most recent event using [DeepL](https://www.deepl.com/). Older content uses the official translation from the North American region. Teams of translators are working on human translations in various different languages.

### Tunguska Sanctuary is currently being translated.
### This app does not and will not support Android 12.

Translators: Gaius, Louay, Neo, fumei, anonymous

#### Video tutorial on how to use this app: https://www.youtube.com/watch?v=OCL6e62u5AI

If you're able, become a [Patreon](https://www.patreon.com/rayshift) to help support the app. There aren't currently any pre-releases available for the English translation while LB6 is underway.

![Example](https://i.imgur.com/1fO8L8Y.png)

If the text is too fast or slow, you can change text speed in the [game settings](https://i.imgur.com/UhmoZI9.png). (Top = text speed, bottom = scroll speed)

Currently, English is supported for:
- Official: Fuyuki to LB4 & Ooku (including events older than 2 years).
- Machine & Edited: Everything else
- Human: LB5 (WIP, see app for status), LB6 (WIP, see app for status).

A Spanish translation for F/GO is also available through this app. Please see https://proyectograndorder.es/ for more information.

### Installation
Requirements: Android 5, 6, 7, 8, 9, 10, 11 are supported. Root is not required.

1. Ensure you have the latest Fate/Grand Order installed.
2. Log into Fate/Grand Order JP to ensure your game data is up to date - click "Download" if promoted for a data update. Afterwards, close the application.
3. Download and install the latest APK from the [releases page](https://github.com/rayshift/translatefgo/releases).
4. Click "install" below the bundle you want to install. You only need to install 1 bundle at a time.
5. Reopen Fate/Grand Order.

#### Updates
The scripts will update on a regular basis. Please keep the app installed and check it regularly - the "installed" status will change to "update available" when an update is ready.

To update after new content is added, you must do the following:
1. Open Fate/Grand Order and accept the data download.
2. Close Fate/Grand Order and click "Update" in the translation app.
3. Reopen Fate/Grand Order.

The application will also auto-update an installed set of scripts when they have minor changes, provided your device is compatible. This can be disabled in the About tab.

#### Uninstallation
1. Close Fate/Grand Order.
2. Open the application and click "Uninstall".
3. Reopen Fate/Grand Order.
4. You can also clear the cache in-game if you cannot use the application.

#### Troubleshooting
Please check our [troubleshooting wiki](https://github.com/rayshift/translatefgo/wiki/Troubleshooting) for help with issues. Alternatively, visit the [discord server](https://discord.gg/6vncnjj) for help.

### Is this safe, will I get banned?
**Yes, it is safe, no, you will not be banned.**

Here is why:
- The app modifies cached story scripts on your SD card (or internal storage), it does not modify or alter the base game.
- The game does not and can not detect the modification of cached story scripts. 
- The app has 17,000+ downloads with 2,000+ active users, and has been operating for nearly 1 year without any bans or warnings being issued because of the app. 
- It is not in the interest of the game's developers or publishers to ban or otherwise punish people for translating story text.
- The app is open source, so you can vet what it does, and build it yourself if you are worried.

However, there are some things you should do before using this app to ensure the safety of your account.
- Always ensure you download the app from rayshift.io or this GitHub page. Do not trust third party sources.
- Create and write down a bind code for your account before using the app.

### Does this work on NA?
NA is supported for English to Spanish translations starting with Translate/FGO version 2.0.0.

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
Add your google services json file to `RayshiftTranslateFGO.Android/google-services.json`.

Run commands similar to these, changing the paths as required:
```
msbuild /t:clean RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj
msbuild /t:restore RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj
msbuild /p:Configuration=Release /t:PackageForAndroid RayshiftTranslateFGO.Android/RayshiftTranslateFGO.Android.csproj /p:AndroidSdkDirectory=/path/to/android-toolchain/sdk /p:AndroidNdkDirectory=/path/to/android-toolchain/ndk
```

You then need to sign and zipalign your APK (both applications are part of the Android SDK Tools):
```
jarsigner -verbose -sigalg SHA1withRSA -digestalg SHA1 -storepass passwordhere -keystore /path/to/the.keystore RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo.apk alias_name
zipalign -f -v -p 4 RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo.apk RayshiftTranslateFGO.Android/bin/Release/io.rayshift.translatefgo-release.apk
```

## Contributing
Please ensure your contribution is approved by first submitting a github issue with the topic of feature request and waiting for approval. Approval is not needed for fixing open bugs, but please notify others you are working on an issue first.

Please note all code relating to bundle creation, translation and the API is private and will not be made available. This app only functions as an installer for those pre-created bundles.

If the change you are suggesting requires an API change, as the API is not open source, please clearly state what changes need to be made in your pull request.

### Pull requests
1. Ensure code written is compatible with .NET Standard 2.0.
2. Test your changes in an android emulator or on a real device.
3. Do not increase version numbers in your pull request. We will merge into a development branch and increase the version numbers before release.
4. Submit a new pull request.

## Licence
The code in this repository, the bundles from our API, and any android applications released are licenced under a [Creative Commons Attribution-NonCommercial-ShareAlike 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) licence. This means it is forbidden to use or distribute any version of the application or any translated bundle for commercial purposes. This is for the protection and longevity of the app.

Google Play and the Google Play logo are trademarks of Google LLC.

Fate/Grand Order is Copyright Aniplex Inc., DELiGHTWORKS, Aniplex of America and Sony Music Entertainment (Japan) Inc. All images and names owned and trademarked by Aniplex Inc., DELiGHTWORKS, Aniplex of America and Sony Music Entertainment (Japan) Inc. are property of their respective owners.

## Script Changelog

- 4th January 2021: All remaining Japanese text is now translated, and Atlantis and Olympus are now available for non-patreons.
- 4th December 2020: LB5.5 is now available with a machine translation.
- 11th November 2020: LB4.5: The Imaginary Naval Battle Imaginary Scramble: Raise the Nautilus! is now translated with a machine translation.
- 9th October 2020: Lostbelt 4, Yuga Kshetra, is now fully translated. Atlantis and Olympus machine translations are available for [Patreons](https://www.patreon.com/rayshift).
- 17th August 2020: Summer 5 "Servant Summer Camp!" is now translated with a machine translation.
- 22nd July 2020: Ooku is fully translated using a human translation by PkFreeze.
