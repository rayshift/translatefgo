## Rayshift.io Translate Fate/Grand Order
[![GitHub Release](https://img.shields.io/github/release/rayshift/translatefgo.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![Github All Releases](https://img.shields.io/github/downloads/rayshift/translatefgo/total.svg?style=flat)](https://github.com/rayshift/translatefgo/releases)  [![license: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](http://creativecommons.org/licenses/by-nc-sa/4.0/) [![Discord Chat](https://img.shields.io/discord/665980614998097941.svg)](https://discord.gg/6vncnjj)  

This application translates cutscenes in Fate/Grand Order JP & NA into a variety of different languages. It features real time machine translation of the most recent event using [DeepL](https://www.deepl.com/). Older content uses the official translation from the North American region. Teams of translators are working on human translations in various different languages.

### Android 14+ is now fully supported. [Click to download.](https://github.com/rayshift/translatefgo/releases)

#### This app uses [Shizuku](https://shizuku.rikka.app/) to support Android 14+ devices that were previously getting a "choose another folder" error

#### NEW! Updated video tutorial on how to use this app: [https://www.youtube.com/watch?v=xjYcUaMOD4A](https://www.youtube.com/watch?v=xjYcUaMOD4A)

If you're able, become a [Patreon](https://www.patreon.com/rayshift) to help support the hosting and maintenance costs of the app. Patreons have access to beta features.

![Example](https://i.imgur.com/1fO8L8Y.png)

If the text is too fast or slow, you can change text speed in the [game settings](https://i.imgur.com/UhmoZI9.png). (Top = text speed, bottom = scroll speed)

Currently, English translations are available for:
- Official: Anything the Global/US version of the app has.
- Machine & Edited: Everything else
- Human Main Story: LB6, Tunguska Sanctuary, Traum, LB7, OC1
- Human Events: Valentines 2022-2024, Xmas 2023, Lilim Harlot Collab, Samurai Remnant Collab

#### Languages
- A Spanish translation for F/GO is also available through this app. Please see https://proyectograndorder.es/ for more information.
- A Portuguese Brazilian translation is also available.
- New! A French translation is now available.
- New! An Indonesian translation is now available.

### Installation
Requirements: Android 7-14+ are supported. Root is not required. Shizuku is required 

1. Ensure you have the latest Fate/Grand Order installed.
2. Log into Fate/Grand Order JP to ensure your game data is up to date - click "Download" if promoted for a data update. Afterwards, close the application.
3. Download and install the latest APK from the [releases page](https://github.com/rayshift/translatefgo/releases).
4. Follow the instructions to set the app up. Consult the tutorial video if you're stuck.
5. Click "install" below the bundle you want to install. You only need to install 1 bundle at a time.
6. Reopen Fate/Grand Order.

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
If you're getting a long error, it may be your internet connection isn't stable. Try switching between wifi and 4G, or try again in a different location.

Visit the [discord server](https://discord.gg/6vncnjj) for help. If you've encountered a bug, please open an issue on GitHub.

### "An internal error has occurred"
Try these troubleshooting steps:
1. Switch between Wifi and 4G. If the error persists,
2. Restart your phone. If the error persists,
3. Go to About -> Configure Android 11 Setup. If the error persists,
4. Reinstall the translatefgo app. If the error still persists,
5. Report as a bug on github.

### Is this safe, will I get banned?
**No action has been taken against players for using this app.**

TranslateFGO has existed for over 4 years and is trusted by tens of thousands of players.

Aniplex is well aware the app exists, and for the last 4 years has chosen not to take any action against it or its users. This is likely because this is a free, non-profit community tool designed to help more players access the game.

However, we cannot provide any guarantee that action will not be taken in the future. Even so, it is far more likely the app would receive a takedown notice, than for players to be banned outright with no warning.

There are some things you should do before using this app to ensure the safety of your account.
- Always ensure you download the app from rayshift.io or this GitHub page. Do not trust third party downloads.
- Create and write down a bind code for your account before using the app.

### Does this work on NA?
NA is supported for English to Spanish and PtBr translations starting with Translate/FGO version 2.0.0.

### Contact Information
This tool operates in good faith to increase player accessibility to Fate/Grand Order. Please use the GitHub issues section for bug reports and suggestions. Should you wish to privately contact the current maintainer of this application, please email webmaster at rayshift.io.

## DMCA
Rayshift takes no responsibility for content made available by others. Should you wish to request the takedown of user-generated content, please send an email to dmca at rayshift dot io.

Please provide a detailed description of where to find the content in question, and a written declaration that you are the copyright owner, or have permission to act on the copyright owner's behalf. We aim to respond to all DMCA requests within two working days.

By submitting a DMCA report, you consent to us using your personal data (such as your email address) for the sole purposes of processing the report. 

## Build Instructions
Should you wish to build the android app yourself, please follow these instructions.

### Prerequisites
#### Windows: 
Visual Studio 2019/2022 with the Xamarin application framework installed.

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

You may also need to rebuild https://github.com/rayshift/nextgenfs and its bindings (overwrite RayshiftTranslateFGO.NextGenFS/Jars) if you want to change any of the AIDL api functions. For detail on how the AIDL bindings work, please see [the developer guide](https://github.com/rayshift/translatefgo/wiki/Shizuku-with-Xamarin-Developer-Guide).

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
