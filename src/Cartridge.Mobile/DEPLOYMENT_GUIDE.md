# 🚀 Complete Deployment Guide

## Issue: Android SDK Not Found

You're getting this error because the Android SDK path isn't configured for command-line builds.

## Quick Fix (Choose One)

### Option A: Use Visual Studio (Easiest)

1. Open `Cartridge.sln` in Visual Studio
2. Right-click `Cartridge.Mobile` project → Set as Startup Project
3. Select your Android device/emulator from the dropdown
4. Press F5 or click "Run"

Visual Studio handles the Android SDK automatically.

### Option B: Setup Android SDK for Command Line

Run PowerShell as Administrator and execute:

```powershell
cd D:\Projects\Cartridge\src\Cartridge.Mobile
.\setup-android-env.ps1
```

Then set it permanently:
```powershell
[System.Environment]::SetEnvironmentVariable("ANDROID_HOME", "$env:LOCALAPPDATA\Android\Sdk", "User")
```

Restart your terminal and try building again.

### Option C: Install Android SDK if Missing

If you don't have the Android SDK installed at all:

1. **Install via Visual Studio Installer:**
   - Open Visual Studio Installer
   - Modify your Visual Studio installation
   - Check "Mobile development with .NET"
   - Install

2. **Or install via Android Studio:**
   - Download from https://developer.android.com/studio
   - Install and run once
   - SDK will be at `C:\Users\<YourUser>\AppData\Local\Android\Sdk`

## 🎯 Priority: Deploy Web App First (CRITICAL!)

**The web app changes are MORE important** because without them, the mobile app can never persist cookies, no matter what we do on the mobile side.

### Step 1: Deploy Web Application Changes

```cmd
cd D:\Projects\Cartridge\src\Cartridge.Web
dotnet build -c Release
```

Then restart your web server. The cookie `SameSite=None` setting is ESSENTIAL.

### Step 2: Verify Web App Cookie Settings

Open https://cartridge.step0fail.com in Chrome:
1. Sign in
2. Press F12 → Application → Cookies
3. Find `.AspNetCore.Identity.Application`
4. Verify:
   - ✅ SameSite = None
   - ✅ Secure = ✓
   - ✅ Expires = (30 days from now)

If SameSite is NOT "None", the deployment didn't work.

### Step 3: Deploy Mobile App (After Web App is Fixed)

**Using Visual Studio (Recommended):**
1. Open `Cartridge.sln`
2. Set `Cartridge.Mobile` as startup project
3. Select your Android device
4. Press F5

**Using Command Line (After SDK setup):**
```cmd
cd D:\Projects\Cartridge\src\Cartridge.Mobile
dotnet build -f net9.0-android -c Debug
```

Then deploy with:
```cmd
adb install -r .\bin\Debug\net9.0-android\com.companyname.cartridge.mobile-Signed.apk
```

## 📊 Testing After Deployment

### 1. Monitor Logs (Windows)

Open PowerShell and run:
```powershell
adb logcat -c
adb logcat | Select-String -Pattern "===|✓|>>>"
```

### 2. Test Flow

1. Open mobile app → Check logs for initialization
2. Sign in → Wait 30 seconds → Check logs for "Has cookies: True"
3. Close app (swipe away)
4. Reopen app → Should stay logged in!

## 🎯 What You Should Do RIGHT NOW

### Immediate Priority (Choose One):

**Option 1: Use Visual Studio (5 minutes)**
```
1. Open Cartridge.sln in Visual Studio
2. Deploy Cartridge.Web to your server
3. Deploy Cartridge.Mobile to your phone
4. Test!
```

**Option 2: Fix Command Line Build (10 minutes)**
```
1. Run setup-android-env.ps1 in PowerShell as Admin
2. Set ANDROID_HOME permanently
3. Restart terminal
4. Build and deploy both apps
```

## 🔍 Quick Diagnostic

Run this to check your setup:

```powershell
# Check if Android SDK exists
Test-Path "$env:LOCALAPPDATA\Android\Sdk"

# Check if ANDROID_HOME is set
$env:ANDROID_HOME

# Check if ADB works
adb version
```

If any of these fail, you need to setup the Android SDK.

## 💡 Recommended Approach

**For quickest results:**

1. ✅ **Deploy web app first** (you can do this without Android SDK)
   ```cmd
   cd D:\Projects\Cartridge\src\Cartridge.Web
   dotnet publish -c Release
   ```
   Then copy to your web server.

2. ✅ **Use Visual Studio to deploy mobile app**
   - Much easier than command line
   - Handles Android SDK automatically
   - Built-in debugging

3. ✅ **Test the authentication persistence**

## 📞 Next Steps

**If you're using Visual Studio:**
- Just open the solution and deploy both projects
- You'll see the logs in Visual Studio's Output window

**If you're using command line:**
- Setup Android SDK first using the PowerShell script
- Then rebuild the mobile app
- Monitor logs with the batch file

**Either way:** The web app changes MUST be deployed for cookies to work!

---

## Summary

**The Problem:** Android SDK not configured for command-line builds

**The Solution:** Use Visual Studio (easier) OR setup Android SDK for command line

**Priority:** Deploy web app first - the `SameSite=None` cookie fix is critical!

**Once deployed:** Follow the testing steps in ACTION_REQUIRED.md

