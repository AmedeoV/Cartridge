# Cartridge Mobile - WebView App

A simple Android app that wraps your Cartridge website (https://cartridge.step0fail.com) in a native WebView.

## What It Does

This app loads your existing web application in a WebView, providing a native Android app experience while using your existing website functionality.

**Benefits:**
- ✅ All website features work immediately
- ✅ No backend changes needed
- ✅ Automatic updates when you update the website
- ✅ Single codebase to maintain (your website)
- ✅ Native app experience (home screen icon, offline detection)

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- .NET MAUI workload: `dotnet workload install maui`
- Android SDK installed

### Build & Run

```powershell
# Set Android SDK path (run each time you open new terminal)
.\setup-android-env.ps1

# Build for Android
dotnet build -f net9.0-android

# Run on connected device/emulator
dotnet build -t:Run -f net9.0-android
```

### Or Use Visual Studio 2022
1. Open `Cartridge.sln`
2. Set `Cartridge.Mobile` as startup project
3. Select Android device/emulator
4. Press F5

## Files

```
Cartridge.Mobile/
├── MainPage.xaml          # WebView displaying website
├── MainPage.xaml.cs       # Loading and error handling
├── AppShell.xaml          # App shell configuration
├── MauiProgram.cs         # App initialization
├── Platforms/Android/     # Android-specific settings
├── Resources/             # App icons, splash screen
├── setup-android-env.ps1  # Environment setup script
└── README.md              # This file
```

## Configuration

### Change Website URL

Edit `MainPage.xaml` line 8:

```xml
<WebView x:Name="CartridgeWebView"
         Source="https://cartridge.step0fail.com"
         BackgroundColor="#0a0a0a" />
```

Replace with your URL if hosting elsewhere.

## How It Works

1. App launches
2. Shows loading indicator (green spinner)
3. WebView loads website URL
4. Website appears and functions normally
5. All interactions happen within the WebView

**Technical:**
- Uses native Android WebView (Chromium-based)
- Maintains cookies/sessions for authentication
- Supports all modern web features
- Hardware back button navigates within website

## Building for Release

```powershell
# Create release APK
dotnet publish -f net9.0-android -c Release

# APK location:
# bin\Release\net9.0-android\publish\
```

## Publishing to Play Store

1. Sign the APK with your keystore
2. Create listing in Google Play Console
3. Upload signed APK/AAB
4. Submit for review

## Troubleshooting

### Android SDK Not Found
Run `.\setup-android-env.ps1` to set environment variable.

### Website Won't Load
- Check internet connection
- Verify URL is correct in MainPage.xaml
- Check Android logs: `adb logcat | findstr Cartridge`

### App Crashes
- Ensure Android emulator/device is API 21+
- Check for SSL/certificate issues with HTTPS

## Documentation

- **WEBVIEW_README.md** - Detailed WebView documentation
- **NEXT_STEPS.md** - Testing and deployment guide
- **SETUP.md** - Android SDK installation help

## License

Part of the Cartridge project.

---

**Made with 💚 for gamers** 🎮📱

