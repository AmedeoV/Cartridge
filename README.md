# 🎮 CARTRIDGE - Gaming Library Aggregator

**A retro-styled unified gaming library for all your platforms**

<p align="center">
  <strong>◄► Track all your games in one place ◄►</strong>
</p>

## 🕹️ About

Cartridge is a .NET 9 web application that aggregates your gaming libraries from multiple platforms (Steam, Epic Games, GOG, etc.) into one unified, retro-styled interface. Never lose track of your game collection again!

### ✨ Features

- 📚 **Unified Library View** - See all your games from different platforms in one place
- 🎨 **Retro Aesthetic** - Beautiful 80s/90s inspired UI with CRT screen effects and neon colors
- 📊 **Statistics Dashboard** - Track your total games, playtime, and connected platforms
- 🔍 **Filter & Search** - Easily find games by platform, genre, or title
- 🏗️ **Extensible Architecture** - Clean architecture ready for mobile app development

### 🎯 Supported Platforms

Currently in development with mock data:
- ✅ Steam
- ✅ Epic Games Store
- 🔜 GOG
- 🔜 Origin
- 🔜 Ubisoft Connect
- 🔜 Xbox Game Pass
- 🔜 PlayStation
- 🔜 Nintendo

## 🏗️ Project Structure

```
Cartridge/
├── src/
│   ├── Cartridge.Core/              # Domain models and interfaces
│   │   ├── Models/                  # Game, Platform, UserLibrary
│   │   └── Interfaces/              # Service contracts
│   ├── Cartridge.Infrastructure/     # External API integrations
│   │   ├── Services/                # Business logic implementations
│   │   └── Connectors/              # Platform-specific connectors
│   ├── Cartridge.Shared/            # Code shared between Web & Mobile
│   └── Cartridge.Web/               # Blazor Server web application
│       ├── Components/
│       │   ├── Layout/              # RetroLayout
│       │   └── Pages/               # Home, Library, etc.
│       └── wwwroot/
│           └── css/                 # Retro styling
└── README.md
```

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- A code editor (Visual Studio 2022, VS Code, or Rider recommended)
- **OR** [Docker](https://www.docker.com/get-started) and Docker Compose (for containerized deployment)

### Quick Start with Docker 🐳

The easiest way to run Cartridge is with Docker:

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd Cartridge
   ```

2. **Set up environment variables (optional)**
   ```bash
   cp .env.example .env
   # Edit .env and add your Steam and RAWG API keys
   ```

3. **Start the application**
   ```bash
   # Using docker-compose
   docker-compose up -d
   
   # OR using PowerShell script (Windows)
   .\docker.ps1 up
   
   # OR using Makefile (Linux/Mac)
   make up
   ```

4. **Access the application**
   - Open your browser to `http://localhost:8080`
   - Enjoy your retro gaming library! 🎮

For detailed Docker instructions, see [README.Docker.md](README.Docker.md)

### Installation (Local Development)

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd Cartridge
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure Steam API (Optional but Recommended)**
   
   To use real Steam integration instead of mock data:
   
   a. Get your Steam Web API key: https://steamcommunity.com/dev/apikey
   
   b. Set up user secrets (recommended):
   ```bash
   cd src/Cartridge.Web
   dotnet user-secrets init
   dotnet user-secrets set "SteamApi:ApiKey" "YOUR_API_KEY_HERE"
   ```
   
   See [STEAM_SETUP.md](STEAM_SETUP.md) for detailed instructions.

4. **Build the solution**
   ```bash
   dotnet build
   ```

5. **Run the web application**
   ```bash
   cd src/Cartridge.Web
   dotnet run
   ```

6. **Open your browser**
   - Navigate to `https://localhost:5001` (or the port shown in the terminal)
   - Enjoy your retro gaming library! 🎮

## 🎨 UI Preview

The application features a retro-styled interface inspired by 80s/90s gaming:

- **Neon green primary color** (#00ff41) reminiscent of classic CRT monitors
- **Scanline effects** for authentic CRT feel
- **Pixel-perfect borders** and retro fonts
- **Glowing text effects** and smooth hover animations
- **Dark background** with radial gradient for screen depth

## 🛠️ Technology Stack

- **Framework**: .NET 9 C#
- **Web**: ASP.NET Core with Blazor Server
- **Future Mobile**: Architecture ready for .NET MAUI
- **Styling**: Custom CSS with retro/cyberpunk aesthetic
- **Architecture**: Clean architecture with DI

## 📱 Future Development

### Mobile App (Planned)

The project is architected to support a future .NET MAUI mobile application:
- Shared business logic in `Cartridge.Shared`
- Platform connectors reusable across web and mobile
- Clean separation of concerns for easy mobile integration

### Planned Features

- 🔐 User authentication and personal accounts
- ✅ **Steam API Integration** - Real data from Steam Web API (implemented!)
- 🔗 Complete API integrations with other gaming platforms
- 🔐 Steam OpenID authentication
- 📈 Advanced statistics and achievements tracking
- 🎮 Game recommendations based on your library
- 👥 Social features to share libraries with friends
- 📱 iOS and Android mobile apps
- 🔄 Automatic library synchronization
- 🎯 Wishlist and tracking for unreleased games
- 💾 Database persistence (currently in-memory)

## 🤝 Contributing

This is currently a personal project, but contributions are welcome! Feel free to:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is open source and available under the [MIT License](LICENSE).

## 🎮 Development Notes

### Current Status

- ✅ Project structure set up
- ✅ Domain models created
- ✅ **Steam Web API integration implemented**
- ✅ Mock data connectors for Epic Games (and fallback for Steam)
- ✅ Retro UI styling complete
- ✅ Home and Library pages functional
- ✅ HTTP client configuration with proper DI
- 🔄 Other platform integrations (planned)
- 🔄 User authentication with Steam OpenID (planned)
- 🔄 Database persistence (planned)

### Steam Integration

The app now includes **real Steam Web API integration**! 🎮

**Features:**
- ✅ Fetch owned games from Steam library
- ✅ Display playtime and last played information
- ✅ Show game cover art from Steam CDN
- ✅ Fallback to mock data if API key not configured
- ✅ Proper error handling and logging

**Setup:**
1. Get your Steam API key: https://steamcommunity.com/dev/apikey
2. Configure using user secrets (see [STEAM_SETUP.md](STEAM_SETUP.md))
3. Your Steam profile must be set to Public for the API to work

**What's Next:**
- Add Steam OpenID authentication
- Implement caching to reduce API calls
- Fetch detailed game information (descriptions, genres, etc.)
- Add UI for connecting/disconnecting Steam accounts
- Store Steam IDs in a database instead of memory

## 🙏 Acknowledgments

- Inspired by classic gaming UIs and cyberpunk aesthetics
- Built with modern .NET technologies
- Special thanks to the gaming community

---

<p align="center">
  <strong>◄►◄►◄► Made with 💚 for gamers ◄►◄►◄►</strong>
</p>
