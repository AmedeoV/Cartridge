using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Gog;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// GOG (Good Old Games) connector
/// Supports GOG Galaxy local database reading
/// </summary>
public class GogConnector : IPlatformConnector
{
    private readonly ILogger<GogConnector> _logger;
    private readonly GogGalaxyDatabaseReader _galaxyReader;
    
    // In-memory storage for demo - replace with database in production
    private static readonly Dictionary<string, bool> _userConnections = new();
    private static readonly Dictionary<string, string> _customGalaxyPaths = new();

    public Platform PlatformType => Platform.GOG;

    public GogConnector(
        ILogger<GogConnector> logger,
        GogGalaxyDatabaseReader galaxyReader)
    {
        _logger = logger;
        _galaxyReader = galaxyReader;
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        return Task.FromResult(_userConnections.ContainsKey(userId) && _userConnections[userId]);
    }

    public async Task<List<Game>> FetchGamesAsync(string userId)
    {
        // Check if user has connected their GOG account
        if (!_userConnections.TryGetValue(userId, out var isConnected) || !isConnected)
        {
            _logger.LogWarning("No GOG account connected for user {UserId}", userId);
            return new List<Game>();
        }

        _logger.LogInformation("Fetching GOG games for user {UserId}", userId);
        
        var games = new List<Game>();

        // Try to read from GOG Galaxy database
        if (_galaxyReader.IsGogGalaxyInstalled())
        {
            try
            {
                _logger.LogInformation("GOG Galaxy detected, reading from local database...");
                
                // Check if user has a custom path
                string? customPath = null;
                _customGalaxyPaths.TryGetValue(userId, out customPath);
                
                var galaxyGames = await _galaxyReader.ReadGamesFromGalaxyAsync(customPath);
                
                // Read playtime data
                _logger.LogInformation("Reading playtime data from GOG Galaxy database...");
                var playtimes = await _galaxyReader.ReadPlaytimeDataAsync(customPath);
                _logger.LogInformation("Playtime dictionary contains {Count} entries", playtimes.Count);
                
                // Update games with playtime data
                foreach (var game in galaxyGames)
                {
                    // The game.Id is in format "platform-productId" (e.g., "gog-gog_123", "epicgames-abc123")
                    // The playtime dictionary keys are the raw releaseKeys from database (e.g., "gog_123", "epic_abc123")
                    // We need to match against the database releaseKey format
                    
                    var parts = game.Id.Split('-', 2); // Split into at most 2 parts
                    var platformPrefix = parts[0]; // "gog", "epicgames", etc.
                    var productId = parts.Length > 1 ? parts[1] : game.Id;
                    
                    // Try different releaseKey formats based on platform
                    string? matchedKey = null;
                    int playtime = 0;
                    
                    // Strategy 1: Direct match (for GOG games like "gog_123")
                    if (playtimes.TryGetValue(productId, out playtime))
                    {
                        matchedKey = productId;
                    }
                    // Strategy 2: Add platform prefix for non-GOG platforms (e.g., "epic_abc123" for EpicGames)
                    else if (platformPrefix != "gog")
                    {
                        // Map platform names to their database prefix
                        var dbPrefix = platformPrefix switch
                        {
                            "epicgames" => "epic",
                            "ubisoftconnect" => "uplay",
                            "amazon" => "amazon",
                            "rockstar" => "rockstar",
                            _ => platformPrefix
                        };
                        
                        var altKey = $"{dbPrefix}_{productId}";
                        if (playtimes.TryGetValue(altKey, out playtime))
                        {
                            matchedKey = altKey;
                        }
                    }
                    
                    if (matchedKey != null)
                    {
                        game.PlaytimeMinutes = playtime;
                        _logger.LogInformation("✓ Set playtime for {Title}: {Minutes} minutes (key: {Key})", 
                            game.Title, playtime, matchedKey);
                    }
                    else
                    {
                        _logger.LogDebug("✗ No playtime found for {Title} (ID: {GameId}, tried: {ProductId})", 
                            game.Title, game.Id, productId);
                    }
                }                games.AddRange(galaxyGames);
                
                _logger.LogInformation("Found {Count} GOG Galaxy games for user {UserId}", galaxyGames.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from GOG Galaxy database");
            }
        }
        else if (_customGalaxyPaths.TryGetValue(userId, out var customPath))
        {
            // User has a custom path but Galaxy not in standard location
            try
            {
                _logger.LogInformation("Trying custom GOG Galaxy path for user {UserId}", userId);
                var galaxyGames = await _galaxyReader.ReadGamesFromGalaxyAsync(customPath);
                var playtimes = await _galaxyReader.ReadPlaytimeDataAsync(customPath);
                
                foreach (var game in galaxyGames)
                {
                    // The game.Id is in format "platform-productId" (e.g., "gog-gog_123", "epicgames-abc123")
                    // The playtime dictionary keys are the raw releaseKeys from database (e.g., "gog_123", "epic_abc123")
                    
                    var parts = game.Id.Split('-', 2);
                    var platformPrefix = parts[0];
                    var productId = parts.Length > 1 ? parts[1] : game.Id;
                    
                    string? matchedKey = null;
                    int playtime = 0;
                    
                    // Try direct match first
                    if (playtimes.TryGetValue(productId, out playtime))
                    {
                        matchedKey = productId;
                    }
                    // Try with platform prefix for non-GOG platforms
                    else if (platformPrefix != "gog")
                    {
                        var dbPrefix = platformPrefix switch
                        {
                            "epicgames" => "epic",
                            "ubisoftconnect" => "uplay",
                            "amazon" => "amazon",
                            "rockstar" => "rockstar",
                            _ => platformPrefix
                        };
                        
                        var altKey = $"{dbPrefix}_{productId}";
                        if (playtimes.TryGetValue(altKey, out playtime))
                        {
                            matchedKey = altKey;
                        }
                    }
                    
                    if (matchedKey != null)
                    {
                        game.PlaytimeMinutes = playtime;
                        _logger.LogDebug("Set playtime for {Title}: {Minutes} minutes", game.Title, playtime);
                    }
                }
                
                games.AddRange(galaxyGames);
                
                _logger.LogInformation("Found {Count} GOG Galaxy games from custom path for user {UserId}", galaxyGames.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from custom GOG Galaxy path");
            }
        }

        // If no games found from either source, show mock data
        if (games.Count == 0)
        {
            _logger.LogInformation("No GOG games found, returning mock data");
            games = GetMockGogGames();
        }
        
        _logger.LogInformation("Total {Count} GOG games for user {UserId}", games.Count, userId);
        
        return games;
    }

    public Task<bool> ConnectAsync(string userId, string credentials)
    {
        if (!string.IsNullOrWhiteSpace(credentials))
        {
            // Check if it's an uploaded database file
            if (credentials.StartsWith("uploaded-db:"))
            {
                var dbPath = credentials.Substring("uploaded-db:".Length);
                if (_galaxyReader.IsValidGalaxyDatabase(dbPath))
                {
                    _customGalaxyPaths[userId] = dbPath;
                    _userConnections[userId] = true;
                    _logger.LogInformation("Connected GOG account with uploaded database for user {UserId}", userId);
                    return Task.FromResult(true);
                }
                _logger.LogWarning("Invalid uploaded GOG Galaxy database");
                return Task.FromResult(false);
            }
            
            // Check if it's a custom path request
            if (credentials.StartsWith("custom-path:"))
            {
                var customPath = credentials.Substring("custom-path:".Length);
                if (_galaxyReader.IsValidGalaxyDatabase(customPath))
                {
                    _customGalaxyPaths[userId] = customPath;
                    _userConnections[userId] = true;
                    _logger.LogInformation("Connected GOG account with custom path for user {UserId}", userId);
                    return Task.FromResult(true);
                }
                _logger.LogWarning("Invalid custom GOG Galaxy path provided");
                return Task.FromResult(false);
            }
            
            _userConnections[userId] = true;
            _logger.LogInformation("Connected GOG account for user {UserId}", userId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Set a custom GOG Galaxy installation path for a user
    /// </summary>
    public Task<bool> SetCustomGalaxyPathAsync(string userId, string path)
    {
        try
        {
            if (_galaxyReader.IsValidGalaxyDatabase(path))
            {
                _customGalaxyPaths[userId] = path;
                _userConnections[userId] = true;
                _logger.LogInformation("Set custom GOG Galaxy path for user {UserId}: {Path}", userId, path);
                return Task.FromResult(true);
            }
            
            _logger.LogWarning("Invalid GOG Galaxy path provided: {Path}", path);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting custom GOG Galaxy path");
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync(string userId)
    {
        _userConnections.Remove(userId);
        _logger.LogInformation("Disconnected GOG account for user {UserId}", userId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns mock GOG games for demonstration
    /// </summary>
    private List<Game> GetMockGogGames()
    {
        return new List<Game>
        {
            new Game
            {
                Id = "gog_1207658930",
                Title = "The Witcher 3: Wild Hunt",
                Description = "You are Geralt of Rivia, mercenary monster slayer. Before you stands a war-torn, monster-infested continent you can explore at will. Your current contract? Tracking down Ciri — the Child of Prophecy, a living weapon that can alter the shape of the world.",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/5643a7c831df452d29005caeca24c231d2d05b70c60cf3275c0101c306c0f3ca.jpg",
                PlaytimeMinutes = 2847,
                LastPlayed = DateTime.UtcNow.AddDays(-5),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-12),
                ReleaseDate = new DateTime(2015, 5, 19),
                Developer = "CD PROJEKT RED",
                Publisher = "CD PROJEKT RED",
                Genres = new List<string> { "RPG", "Action", "Open World" }
            },
            new Game
            {
                Id = "gog_1207658924",
                Title = "Cyberpunk 2077",
                Description = "Cyberpunk 2077 is an open-world, action-adventure RPG set in the megalopolis of Night City, where you play as a cyberpunk mercenary wrapped up in a do-or-die fight for survival.",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/5643a7c831df452d29005caeca24c231d2d05b70c60cf3275c0101c306c0f3ca_product_card_v2_mobile_slider_639.jpg",
                PlaytimeMinutes = 1920,
                LastPlayed = DateTime.UtcNow.AddDays(-2),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-6),
                ReleaseDate = new DateTime(2020, 12, 10),
                Developer = "CD PROJEKT RED",
                Publisher = "CD PROJEKT RED",
                Genres = new List<string> { "RPG", "Action", "Open World", "Sci-Fi" }
            },
            new Game
            {
                Id = "gog_1207658915",
                Title = "Baldur's Gate 3",
                Description = "Gather your party and return to the Forgotten Realms in a tale of fellowship and betrayal, sacrifice and survival, and the lure of absolute power.",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/85a8c2b664e8b6712955648f30d77c898f9faa3fb4ba9c3e83fcf4e38fc6bd8b.jpg",
                PlaytimeMinutes = 4560,
                LastPlayed = DateTime.UtcNow.AddHours(-6),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-3),
                ReleaseDate = new DateTime(2023, 8, 3),
                Developer = "Larian Studios",
                Publisher = "Larian Studios",
                Genres = new List<string> { "RPG", "Strategy", "Turn-Based" }
            },
            new Game
            {
                Id = "gog_1207666883",
                Title = "Disco Elysium - The Final Cut",
                Description = "Disco Elysium - The Final Cut is a groundbreaking role playing game. You're a detective with a unique skill system at your disposal and a whole city to carve your path across.",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/85a8c2b664e8b6712955648f30d77c898f9faa3fb4ba9c3e83fcf4e38fc6bd8b_product_card_v2_mobile_slider_639.jpg",
                PlaytimeMinutes = 1860,
                LastPlayed = DateTime.UtcNow.AddDays(-15),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-8),
                ReleaseDate = new DateTime(2021, 3, 30),
                Developer = "ZA/UM",
                Publisher = "ZA/UM",
                Genres = new List<string> { "RPG", "Detective", "Indie" }
            },
            new Game
            {
                Id = "gog_1207658891",
                Title = "Divinity: Original Sin 2",
                Description = "The Divine is dead. The Void approaches. And the powers latent within you are soon to awaken. Choose your role in a BAFTA-winning story, and explore a world that reacts to who you are, and the choices you make.",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/460da9e18c972f6e69c8e47f4c9e037ea86c34f3ef9963a689dea350ec61bf98.jpg",
                PlaytimeMinutes = 3240,
                LastPlayed = DateTime.UtcNow.AddDays(-30),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-18),
                ReleaseDate = new DateTime(2017, 9, 14),
                Developer = "Larian Studios",
                Publisher = "Larian Studios",
                Genres = new List<string> { "RPG", "Strategy", "Turn-Based" }
            },
            new Game
            {
                Id = "gog_1207658674",
                Title = "Stardew Valley",
                Description = "You've inherited your grandfather's old farm plot in Stardew Valley. Armed with hand-me-down tools and a few coins, you set out to begin your new life!",
                Platform = Platform.GOG,
                CoverImageUrl = "https://images.gog-statics.com/460da9e18c972f6e69c8e47f4c9e037ea86c34f3ef9963a689dea350ec61bf98_product_card_v2_mobile_slider_639.jpg",
                PlaytimeMinutes = 5640,
                LastPlayed = DateTime.UtcNow.AddDays(-7),
                AddedToLibrary = DateTime.UtcNow.AddMonths(-24),
                ReleaseDate = new DateTime(2016, 2, 26),
                Developer = "ConcernedApe",
                Publisher = "ConcernedApe",
                Genres = new List<string> { "Simulation", "RPG", "Indie" }
            }
        };
    }
}

