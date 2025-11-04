# 📱 Cartridge Mobile App

The Cartridge Android application brings your gaming library to your mobile device!

## Overview

A native Android app built with .NET MAUI that allows you to:
- View your gaming library on the go
- Browse games from all connected platforms
- See game details, playtime, and statistics
- Sync your library with the cloud
- Manage platform connections

## Features

✅ **Dashboard**: Overview of your gaming collection
✅ **Library Browser**: Search and filter your games
✅ **Game Details**: View comprehensive game information
✅ **Retro UI**: Classic terminal-inspired design
✅ **Cross-Platform Ready**: Built with .NET MAUI

## Quick Start

### Prerequisites
- Visual Studio 2022 with .NET MAUI workload
- OR .NET 9.0 SDK + Android SDK
- Android device or emulator (API 21+)

### Setup
```bash
# Install MAUI workload
dotnet workload install maui

# Navigate to mobile project
cd src/Cartridge.Mobile

# Build for Android
dotnet build -f net9.0-android

# Run on device/emulator
dotnet build -t:Run -f net9.0-android
```

## Configuration

Before running, update the API endpoint in `MauiProgram.cs`:

```csharp
builder.Services.AddHttpClient("CartridgeAPI", client =>
{
    // For Android emulator
    client.BaseAddress = new Uri("http://10.0.2.2:5000/");
    
    // For physical device
    // client.BaseAddress = new Uri("http://YOUR_IP:5000/");
});
```

## Project Structure

```
Cartridge.Mobile/
├── Pages/              # XAML UI pages
├── ViewModels/         # MVVM view models
├── Services/           # API client & business logic
├── Resources/          # Images, fonts, styles
├── Platforms/Android/  # Android-specific code
└── MauiProgram.cs     # Dependency injection
```

## Screenshots

### Dashboard
- Total games count
- Connected platforms
- Last sync status
- Quick access buttons

### Library
- Searchable game list
- Platform filtering
- Game cards with cover art
- Playtime information

### Game Details
- Full game information
- Platform details
- Play statistics
- Cover art display

## Development

### Key Technologies
- **.NET MAUI**: Cross-platform UI framework
- **MVVM Pattern**: Clean architecture
- **RESTful API**: Communication with backend
- **Material Design**: Modern Android UI

### Architecture
```
View (XAML) → ViewModel → Service → API
```

## Building for Production

### Create Release APK
```bash
dotnet publish -f net9.0-android -c Release
```

### Sign APK for Play Store
1. Generate keystore
2. Configure signing in project
3. Build release version
4. Upload to Google Play Console

## Troubleshooting

### Android SDK Not Found
Install via Visual Studio or Android Studio, then set `ANDROID_HOME` environment variable.

### Can't Connect to API
- Emulator: Use `10.0.2.2` instead of `localhost`
- Device: Use your computer's IP address
- Ensure API is running and firewall allows connections

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build -f net9.0-android
```

## Roadmap

- [ ] Authentication flow
- [ ] Offline mode with caching
- [ ] Push notifications
- [ ] Game achievement tracking
- [ ] Platform connection management
- [ ] Statistics and charts
- [ ] Theme customization
- [ ] Tablet optimization

## Documentation

- [Setup Guide](src/Cartridge.Mobile/SETUP.md) - Detailed setup instructions
- [README](src/Cartridge.Mobile/README.md) - Complete mobile app documentation
- [MAUI Docs](https://docs.microsoft.com/dotnet/maui) - Official .NET MAUI documentation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement your changes
4. Test on Android device/emulator
5. Submit a pull request

## License

Part of the Cartridge project - see main LICENSE file

---

**Download now and take your gaming library anywhere!** 🎮📱

