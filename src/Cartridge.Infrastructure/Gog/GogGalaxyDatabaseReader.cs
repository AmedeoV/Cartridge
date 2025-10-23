using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Cartridge.Core.Models;

namespace Cartridge.Infrastructure.Gog;

/// <summary>
/// Reads game library data from GOG Galaxy's local SQLite database
/// </summary>
public class GogGalaxyDatabaseReader
{
    private readonly ILogger<GogGalaxyDatabaseReader> _logger;

    // Possible GOG Galaxy database locations
    private static readonly string[] PossibleDbPaths = new[]
    {
        @"%LOCALAPPDATA%\GOG.com\Galaxy\storage\galaxy-2.0.db",
        @"C:\ProgramData\GOG.com\Galaxy\storage\galaxy-2.0.db",
        @"C:\Program Files (x86)\GOG Galaxy\storage\galaxy-2.0.db",
        @"C:\Program Files\GOG Galaxy\storage\galaxy-2.0.db",
        @"%PROGRAMFILES(X86)%\GOG Galaxy\storage\galaxy-2.0.db",
        @"%PROGRAMFILES%\GOG Galaxy\storage\galaxy-2.0.db",
        @"%PROGRAMDATA%\GOG.com\Galaxy\storage\galaxy-2.0.db"
    };

    public GogGalaxyDatabaseReader(ILogger<GogGalaxyDatabaseReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if GOG Galaxy is installed and database exists
    /// </summary>
    public bool IsGogGalaxyInstalled()
    {
        var dbPath = FindGalaxyDatabase();
        return dbPath != null;
    }

    /// <summary>
    /// Find the GOG Galaxy database from known locations
    /// </summary>
    private string? FindGalaxyDatabase()
    {
        foreach (var path in PossibleDbPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(expandedPath))
            {
                _logger.LogInformation("Found GOG Galaxy database at: {Path}", expandedPath);
                return expandedPath;
            }
        }

        _logger.LogInformation("GOG Galaxy database not found in any known location");
        return null;
    }

    /// <summary>
    /// Get the GOG Galaxy database path
    /// </summary>
    public string? GetDatabasePath()
    {
        return FindGalaxyDatabase();
    }

    /// <summary>
    /// Check if a custom path contains a valid GOG Galaxy database
    /// </summary>
    public bool IsValidGalaxyDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);

            // Check if it's a directory or file
            if (Directory.Exists(expandedPath))
            {
                // If it's a directory, look for galaxy-2.0.db inside
                expandedPath = Path.Combine(expandedPath, "storage", "galaxy-2.0.db");
            }

            if (!File.Exists(expandedPath))
                return false;

            // Try to open the database to verify it's valid
            var connectionString = $"Data Source={expandedPath};Mode=ReadOnly";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Check if it has the expected tables
            using var command =
                new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GamePieces'",
                    connection);
            var result = command.ExecuteScalar();

            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid GOG Galaxy database at path: {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Read games from GOG Galaxy database
    /// </summary>
    public async Task<List<Game>> ReadGamesFromGalaxyAsync(string? customPath = null)
    {
        var games = new List<Game>();
        string? dbPath;

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            // Use custom path if provided
            dbPath = Environment.ExpandEnvironmentVariables(customPath);

            // If it's a directory, append the database file path
            if (Directory.Exists(dbPath))
            {
                dbPath = Path.Combine(dbPath, "storage", "galaxy-2.0.db");
            }
        }
        else
        {
            // Try to find database in known locations
            dbPath = GetDatabasePath();
        }

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            _logger.LogWarning("GOG Galaxy database not found. Is GOG Galaxy installed?");
            return games;
        }

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly";

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            
            // Ensure WAL mode is enabled for reading latest data
            using (var pragmaCommand = connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA journal_mode=WAL;";
                await pragmaCommand.ExecuteNonQueryAsync();
            }

            var query = @"
                SELECT DISTINCT
                    lr.releaseKey,
                    gp_title.value as title,
                    gp_meta.value as metadata,
                    gp_images.value as images
                FROM LibraryReleases lr
                LEFT JOIN GamePieces gp_title ON lr.releaseKey = gp_title.releaseKey 
                    AND gp_title.gamePieceTypeId = 45
                LEFT JOIN GamePieces gp_meta ON lr.releaseKey = gp_meta.releaseKey 
                    AND gp_meta.gamePieceTypeId = 42
                LEFT JOIN GamePieces gp_images ON lr.releaseKey = gp_images.releaseKey 
                    AND gp_images.gamePieceTypeId = 10
            ";

            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            _logger.LogInformation("📊 Starting to read games from GOG Galaxy database...");
            int rowCount = 0;
            while (await reader.ReadAsync())
            {
                rowCount++;
                if (rowCount == 1)
                {
                    _logger.LogInformation("✅ First row received from database");
                }

                var releaseKey = reader.GetString(0);
                var title = reader.IsDBNull(1) ? null : reader.GetString(1);
                var metadataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                var imagesJson = reader.IsDBNull(3) ? null : reader.GetString(3);

                // Robust platform detection from metadata, with fallback to releaseKey prefix
                Platform platform = Platform.GOG;
                string? productId = releaseKey;
                string? detectedPlatformRaw = null;
                string? detectedSourceRaw = null;
                bool platformDetected = false;
                if (!string.IsNullOrWhiteSpace(metadataJson))
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);
                        if (metadata.ValueKind == JsonValueKind.Object)
                        {
                            // Capture raw values for debug
                            if (metadata.TryGetProperty("platform", out var platformPropRaw) && platformPropRaw.ValueKind == JsonValueKind.String)
                                detectedPlatformRaw = platformPropRaw.GetString();
                            if (metadata.TryGetProperty("source", out var sourcePropRaw) && sourcePropRaw.ValueKind == JsonValueKind.String)
                                detectedSourceRaw = sourcePropRaw.GetString();

                            // Epic Games detection (all known variants)
                            if ((detectedPlatformRaw != null && (detectedPlatformRaw.ToLower().Contains("epic") || detectedPlatformRaw.ToLower().Contains("egs"))) ||
                                (detectedSourceRaw != null && (detectedSourceRaw.ToLower().Contains("epic") || detectedSourceRaw.ToLower().Contains("egs"))))
                            {
                                platform = Platform.EpicGames;
                                platformDetected = true;
                                if (metadata.TryGetProperty("gameId", out var epicIdProp) && epicIdProp.ValueKind == JsonValueKind.String)
                                    productId = epicIdProp.GetString();
                                else if (metadata.TryGetProperty("product_id", out var epicIdAltProp) && epicIdAltProp.ValueKind == JsonValueKind.String)
                                    productId = epicIdAltProp.GetString();
                                _logger.LogDebug("Detected EpicGames for {Title} (platform: {PlatformRaw}, source: {SourceRaw})", title, detectedPlatformRaw, detectedSourceRaw);
                            }
                            // Ubisoft Connect detection (all known variants)
                            else if ((detectedPlatformRaw != null && (detectedPlatformRaw.ToLower().Contains("uplay") || detectedPlatformRaw.ToLower().Contains("ubisoft"))) ||
                                     (detectedSourceRaw != null && (detectedSourceRaw.ToLower().Contains("uplay") || detectedSourceRaw.ToLower().Contains("ubisoft"))))
                            {
                                platform = Platform.UbisoftConnect;
                                platformDetected = true;
                                if (metadata.TryGetProperty("gameId", out var ubiIdProp) && ubiIdProp.ValueKind == JsonValueKind.String)
                                    productId = ubiIdProp.GetString();
                                else if (metadata.TryGetProperty("product_id", out var ubiIdAltProp) && ubiIdAltProp.ValueKind == JsonValueKind.String)
                                    productId = ubiIdAltProp.GetString();
                                _logger.LogDebug("Detected UbisoftConnect for {Title} (platform: {PlatformRaw}, source: {SourceRaw})", title, detectedPlatformRaw, detectedSourceRaw);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not parse metadata for {Title}", title);
                    }
                }
                // Fallback: use releaseKey prefix if platform not detected from metadata
                if (!platformDetected && !string.IsNullOrWhiteSpace(releaseKey))
                {
                    var rk = releaseKey.ToLower();
                    if (rk.StartsWith("epic_"))
                    {
                        platform = Platform.EpicGames;
                        productId = releaseKey.Substring("epic_".Length);
                        _logger.LogDebug("Detected EpicGames by releaseKey for {Title} (releaseKey: {ReleaseKey})", title, releaseKey);
                    }
                    else if (rk.StartsWith("uplay_") || rk.StartsWith("ubisoft_"))
                    {
                        platform = Platform.UbisoftConnect;
                        productId = releaseKey.Substring(rk.StartsWith("uplay_") ? "uplay_".Length : "ubisoft_".Length);
                        _logger.LogDebug("Detected UbisoftConnect by releaseKey for {Title} (releaseKey: {ReleaseKey})", title, releaseKey);
                    }
                    else
                    {
                        _logger.LogDebug("Defaulting to GOG for {Title} (platform: {PlatformRaw}, source: {SourceRaw}, releaseKey: {ReleaseKey})", title, detectedPlatformRaw, detectedSourceRaw, releaseKey);
                    }
                }

                // Skip duplicates
                if (games.Any(g => g.Id == $"{platform.ToString().ToLower()}-{productId}"))
                    continue;

                // Get title - gamePieceTypeId 45 can be either plain string or JSON
                string? gameTitle = null;
                if (!reader.IsDBNull(1))
                {
                    var titleValue = reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(titleValue) && titleValue.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var titleJson = JsonSerializer.Deserialize<JsonElement>(titleValue);
                            if (titleJson.TryGetProperty("title", out var titleProp) &&
                                titleProp.ValueKind == JsonValueKind.String)
                            {
                                gameTitle = titleProp.GetString();
                            }
                        }
                        catch
                        {
                            gameTitle = titleValue;
                        }
                    }
                    else
                    {
                        gameTitle = titleValue;
                    }
                }

                // If no title in dedicated field, try to parse from metadata
                if (string.IsNullOrWhiteSpace(gameTitle) && !reader.IsDBNull(2))
                {
                    var metadataJsonInner = reader.GetString(2);
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJsonInner);
                        if (metadata.TryGetProperty("title", out var titleProp) &&
                            titleProp.ValueKind == JsonValueKind.String)
                        {
                            gameTitle = titleProp.GetString();
                        }
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrWhiteSpace(gameTitle))
                {
                    gameTitle = releaseKey;
                }
                
                // Debug log for Ubisoft games - AFTER title parsing
                if (rowCount <= 50) // Log first 50 games to see the pattern
                {
                    _logger.LogInformation("📋 Row {RowNum}: Key={ReleaseKey}, Title={Title}, Platform={Platform}", 
                        rowCount, releaseKey, gameTitle, platform);
                }
                
                if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                {
                    _logger.LogInformation("🎮 Ubisoft game detected - Key: {ReleaseKey}, Title: {Title}, Platform: {Platform}", 
                        releaseKey, gameTitle, platform);
                }

                // Skip Amazon Prime Gaming games and Game Overlay
                if (gameTitle.Contains("Amazon Prime", StringComparison.OrdinalIgnoreCase) ||
                    gameTitle.Contains("Prime Gaming", StringComparison.OrdinalIgnoreCase) ||
                    gameTitle.Contains("Game Overlay", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Skipping filtered game: {Title}", gameTitle);
                    continue;
                }

                // Filter out games with null, empty, or releaseKey-like titles
                // Only skip if the title is exactly the releaseKey (indicating parsing failure)
                bool isReleaseKeyTitle = false;
                if (string.IsNullOrWhiteSpace(gameTitle))
                {
                    isReleaseKeyTitle = true;
                }
                else
                {
                    var lowerTitle = gameTitle.Trim().ToLower();
                    var lowerReleaseKey = releaseKey.ToLower();
                    // Only skip if title exactly matches the releaseKey
                    if (lowerTitle == lowerReleaseKey)
                    {
                        isReleaseKeyTitle = true;
                    }
                }
                if (isReleaseKeyTitle)
                {
                    _logger.LogDebug("Skipping game with invalid title: {Title} (releaseKey: {ReleaseKey})", gameTitle, releaseKey);
                    continue;
                }

                // Extract cover image URL
                string? coverImageUrl = null;
                bool hasImageData = !reader.IsDBNull(3);
                if (hasImageData)
                {
                    try
                    {
                        var imagesJsonInner = reader.GetString(3);
                        var images = JsonSerializer.Deserialize<JsonElement>(imagesJsonInner);
                        
                        // Debug log for Ubisoft games - show raw image JSON
                        if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                        {
                            _logger.LogInformation("🖼️ Ubisoft image data for {Title}: {ImageJson}", 
                                gameTitle, imagesJsonInner.Length > 200 ? imagesJsonInner.Substring(0, 200) + "..." : imagesJsonInner);
                        }
                        
                        if (images.TryGetProperty("verticalCover", out var verticalCover) &&
                            verticalCover.ValueKind == JsonValueKind.String)
                        {
                            coverImageUrl = verticalCover.GetString();
                        }
                        else if (images.TryGetProperty("background", out var background) &&
                                 background.ValueKind == JsonValueKind.String)
                        {
                            coverImageUrl = background.GetString();
                        }
                        else if (images.TryGetProperty("squareIcon", out var squareIcon) &&
                                 squareIcon.ValueKind == JsonValueKind.String)
                        {
                            coverImageUrl = squareIcon.GetString();
                        }

                        if (!string.IsNullOrWhiteSpace(coverImageUrl))
                        {
                            _logger.LogDebug("Extracted image from GamePieces for {Title}: {Url}",
                                gameTitle, coverImageUrl);
                            
                            // Extra log for Ubisoft games
                            if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                            {
                                _logger.LogInformation("✅ Ubisoft image URL extracted for {Title}: {Url}", 
                                    gameTitle, coverImageUrl);
                            }
                        }
                        else if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                        {
                            _logger.LogWarning("❌ No image URL extracted for Ubisoft game {Title} despite having image data", gameTitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error parsing GamePieces images JSON for {Title}",
                            gameTitle);
                        
                        if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                        {
                            _logger.LogError(ex, "Error parsing images for Ubisoft game {Title}", gameTitle);
                        }
                    }
                }
                else if (releaseKey.ToLower().Contains("uplay") || releaseKey.ToLower().Contains("ubisoft"))
                {
                    _logger.LogWarning("⚠️ No image data in database for Ubisoft game {Title}", gameTitle);
                }

                // Fallback to GOG CDN for GOG games, or leave null for others
                if (string.IsNullOrWhiteSpace(coverImageUrl) && platform == Platform.GOG)
                {
                    coverImageUrl =
                        $"https://images.gog-statics.com/{productId}_product_card_v2_mobile_slider_639.jpg";
                    _logger.LogDebug(
                        "Using CDN fallback for {Title} (productId: {ProductId}): {Url}", gameTitle,
                        productId, coverImageUrl);
                }

                var game = new Game
                {
                    Id = $"{platform.ToString().ToLower()}-{productId}",
                    Title = gameTitle,
                    Platform = platform,
                    CoverImageUrl = coverImageUrl,
                    AddedToLibrary = DateTime.UtcNow
                };

                if (!reader.IsDBNull(2))
                {
                    var metadataJsonInner = reader.GetString(2);
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJsonInner);
                        EnhanceGameWithMetadata(game, metadata);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not parse metadata for {Title}", gameTitle);
                    }
                }

                // Dump metadata JSON for inspection
                if (!string.IsNullOrWhiteSpace(metadataJson))
                {
                    _logger.LogDebug("Metadata JSON for {Title}: {MetadataJson}", title, metadataJson);
                }

                games.Add(game);
            }
            
            _logger.LogInformation("📊 Finished reading {TotalRows} rows from database, found {GameCount} valid games", rowCount, games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading GOG Galaxy database at {Path}", dbPath);
        }

        return games;
    }

    private Game? ParseGogGameData(string releaseKey, JsonElement gameData)
    {
        try
        {
            // GOG Galaxy database structure:
            // The 'meta' pieceKey contains title and basic info
            string? title = null;
            string? summary = null;

            // Try different JSON structures GOG uses
            if (gameData.TryGetProperty("title", out var titleProp))
            {
                title = titleProp.ValueKind == JsonValueKind.String
                    ? titleProp.GetString()
                    : null;
            }

            if (gameData.TryGetProperty("summary", out var summaryProp))
            {
                summary = summaryProp.ValueKind == JsonValueKind.String
                    ? summaryProp.GetString()
                    : null;
            }

            // If no title found, skip this entry
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            // Extract product ID from release key (format: gog_PRODUCTID)
            var productId = releaseKey.Replace("gog_", "");

            var game = new Game
            {
                Id = releaseKey,
                Title = title,
                Description = summary,
                Platform = Platform.GOG,
                AddedToLibrary = DateTime.UtcNow,
                CoverImageUrl =
                    $"https://images.gog-statics.com/{productId}_product_card_v2_mobile_slider_639.jpg"
            };

            // Try to extract release date (can be either string or Unix timestamp number)
            if (gameData.TryGetProperty("releaseDate", out var releaseDate))
            {
                if (releaseDate.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(releaseDate.GetString(), out var parsedDate))
                    {
                        game.ReleaseDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                    }
                }
                else if (releaseDate.ValueKind == JsonValueKind.Number)
                {
                    // Unix timestamp
                    var timestamp = releaseDate.GetInt64();
                    game.ReleaseDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
            }

            // Try to extract developers
            if (gameData.TryGetProperty("developers", out var developers) &&
                developers.ValueKind == JsonValueKind.Array &&
                developers.GetArrayLength() > 0)
            {
                var devElement = developers[0];
                game.Developer = devElement.ValueKind == JsonValueKind.String
                    ? devElement.GetString()
                    : null;
            }

            // Try to extract publishers
            if (gameData.TryGetProperty("publishers", out var publishers) &&
                publishers.ValueKind == JsonValueKind.Array &&
                publishers.GetArrayLength() > 0)
            {
                var pubElement = publishers[0];
                game.Publisher = pubElement.ValueKind == JsonValueKind.String
                    ? pubElement.GetString()
                    : null;
            }

            return game;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing game data for release key {ReleaseKey}", releaseKey);
            return null;
        }
    }

    /// <summary>
    /// Read playtime statistics from GOG Galaxy database
    /// </summary>
    public async Task<Dictionary<string, int>> ReadPlaytimeDataAsync(string? customPath = null)
    {
        var playtimes = new Dictionary<string, int>();
        string? dbPath;

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            dbPath = Environment.ExpandEnvironmentVariables(customPath);
            if (Directory.Exists(dbPath))
            {
                dbPath = Path.Combine(dbPath, "storage", "galaxy-2.0.db");
            }
        }
        else
        {
            dbPath = GetDatabasePath();
        }

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            return playtimes;
        }

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly";

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            
            // Ensure WAL mode is enabled for reading latest data
            using (var pragmaCommand = connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA journal_mode=WAL;";
                await pragmaCommand.ExecuteNonQueryAsync();
            }

            // Query for playtime data from GameTimes table
            var query = @"
                SELECT 
                    releaseKey,
                    minutesInGame
                FROM GameTimes
                WHERE minutesInGame IS NOT NULL AND minutesInGame > 0";

            try
            {
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var releaseKey = reader.GetString(0);
                        var minutesInGame = reader.GetInt32(1);

                        playtimes[releaseKey] = minutesInGame;
                        _logger.LogInformation("Found playtime: {ReleaseKey} = {Minutes} minutes", releaseKey, minutesInGame);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error reading playtime row");
                    }
                }
                
                _logger.LogInformation("Successfully read {Count} playtime entries from GOG Galaxy database", playtimes.Count);
            }
            catch (SqliteException ex)
            {
                _logger.LogWarning(ex, "GameTimes table not available or different structure");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading playtime data from GOG Galaxy database");
        }

        return playtimes;
    }

    /// <summary>
    /// Enhance a Game object with additional metadata from the GOG Galaxy database
    /// </summary>
    private void EnhanceGameWithMetadata(Game game, JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
            return;

        // Note: Descriptions should come from RAWG API, not GOG metadata
        // This method only handles technical metadata that GOG stores
        
        // Release date
        if (metadata.TryGetProperty("releaseDate", out var releaseDateProp))
        {
            if (releaseDateProp.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(releaseDateProp.GetString(), out var parsedDate))
            {
                game.ReleaseDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
            else if (releaseDateProp.ValueKind == JsonValueKind.Number)
            {
                // Unix timestamp
                var timestamp = releaseDateProp.GetInt64();
                game.ReleaseDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
        }
        // Developer
        if (metadata.TryGetProperty("developers", out var devsProp) &&
            devsProp.ValueKind == JsonValueKind.Array && devsProp.GetArrayLength() > 0)
        {
            var devElement = devsProp[0];
            if (devElement.ValueKind == JsonValueKind.String)
                game.Developer = devElement.GetString();
        }
        // Publisher
        if (metadata.TryGetProperty("publishers", out var pubsProp) &&
            pubsProp.ValueKind == JsonValueKind.Array && pubsProp.GetArrayLength() > 0)
        {
            var pubElement = pubsProp[0];
            if (pubElement.ValueKind == JsonValueKind.String)
                game.Publisher = pubElement.GetString();
        }
        // Genres
        if (metadata.TryGetProperty("genres", out var genresProp) &&
            genresProp.ValueKind == JsonValueKind.Array)
        {
            var genres = new List<string>();
            foreach (var genreElement in genresProp.EnumerateArray())
            {
                if (genreElement.ValueKind == JsonValueKind.String)
                {
                    var genre = genreElement.GetString();
                    if (!string.IsNullOrEmpty(genre))
                    {
                        genres.Add(genre);
                    }
                }
            }
            game.Genres = genres;
        }
    }
}
