# Cartridge Mobile - Minimal File Structure

## Essential Files Only (WebView Wrapper)

### Core App Files (7 files)
```
App.xaml.cs              - App entry point
AppShell.xaml            - Navigation shell (XAML)
AppShell.xaml.cs         - Navigation shell (code)
MainPage.xaml            - WebView page (XAML) - THE MAIN FILE
MainPage.xaml.cs         - WebView loading logic
MauiProgram.cs           - App configuration
Cartridge.Mobile.csproj  - Project file
```

### Android Platform Files (4 files)
```
Platforms/Android/
├── AndroidManifest.xml              - App permissions (INTERNET, etc.)
├── MainActivity.cs                  - Android entry point
├── MainApplication.cs               - Android app class
└── Resources/values/colors.xml      - Android theme colors (green)
```

### Resources (3 files)
```
Resources/
├── AppIcon/appicon.svg              - App icon
├── AppIcon/appiconfg.svg           - App icon foreground
└── Splash/splash.svg                - Splash screen
```

### Documentation & Scripts (2 files)
```
README.md                - Documentation
setup-android-env.ps1    - Environment setup script
```

---

## Total: 16 Essential Files

**What was removed:**
- ❌ ViewModels/ folder (4 files)
- ❌ Pages/ folder (4 files)
- ❌ Services/ folder (1 file)
- ❌ Resources/Fonts/ (2 files)
- ❌ Resources/Images/ (1 file)
- ❌ Resources/Raw/ (1 file)
- ❌ Resources/Styles/ (2 files)
- ❌ Platforms/iOS/ (entire folder)
- ❌ Platforms/MacCatalyst/ (entire folder)
- ❌ Platforms/Windows/ (entire folder)
- ❌ Platforms/Tizen/ (entire folder)
- ❌ Properties/ (launchSettings.json)
- ❌ App.xaml (using code-only approach)
- ❌ GlobalXmlns.cs
- ❌ Multiple documentation files
- ❌ appsettings.json

**Result:** From 44+ files down to **16 essential files**

---

## What Each Essential File Does

### MainPage.xaml (THE KEY FILE)
```xml
<WebView Source="https://cartridge.step0fail.com" />
```
This is the heart of your app - loads your website.

### AndroidManifest.xml
```xml
<uses-permission android:name="android.permission.INTERNET" />
```
Required for network access.

### Cartridge.Mobile.csproj
Defines what to build and how.

### App Icons & Splash
Visual branding when app launches and on home screen.

### Everything Else
Standard MAUI boilerplate needed to run an Android app.

---

## Build & Run

```powershell
.\setup-android-env.ps1; dotnet build -f net9.0-android; dotnet build -t:Run -f net9.0-android
```

---

**Status:** ✅ Minimized to bare essentials  
**Type:** WebView wrapper (loads website)  
**Maintenance:** Just update your website!

