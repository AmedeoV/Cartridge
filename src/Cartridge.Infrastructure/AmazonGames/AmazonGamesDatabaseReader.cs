using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Cartridge.Core.Models;

namespace Cartridge.Infrastructure.AmazonGames;

/// <summary>
/// Reads game library data from Amazon Games' local SQLite database
/// </summary>
public class AmazonGamesDatabaseReader
{
    private readonly ILogger<AmazonGamesDatabaseReader> _logger;

    // Possible Amazon Games database locations
    private static readonly string[] PossibleDbPaths = new[]
    {
        @"%LOCALAPPDATA%\Amazon Games\Data\Games\Sql\GameProductInfo.sqlite",
        @"C:\Users\%USERNAME%\AppData\Local\Amazon Games\Data\Games\Sql\GameProductInfo.sqlite"
    };

    public AmazonGamesDatabaseReader(ILogger<AmazonGamesDatabaseReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if Amazon Games is installed and database exists
    /// </summary>
    public bool IsAmazonGamesInstalled()
    {
        var dbPath = FindAmazonGamesDatabase();
        return dbPath != null;
    }

    /// <summary>
    /// Find the Amazon Games database from known locations
    /// </summary>
    private string? FindAmazonGamesDatabase()
    {
        foreach (var path in PossibleDbPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(expandedPath))
            {
                _logger.LogInformation("Found Amazon Games database at: {Path}", expandedPath);
                return expandedPath;
            }
        }

        _logger.LogInformation("Amazon Games database not found in any known location");
        return null;
    }

    /// <summary>
    /// Get the Amazon Games database path
    /// </summary>
    public string? GetDatabasePath()
    {
        return FindAmazonGamesDatabase();
    }

    /// <summary>
    /// Check if a custom path contains a valid Amazon Games database
    /// </summary>
    public bool IsValidAmazonGamesDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);

            if (!File.Exists(expandedPath))
                return false;

            // Try to open the database to verify it's valid
            var connectionString = $"Data Source={expandedPath};Mode=ReadOnly";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Check if it has the expected tables (DbSet for GameProductInfo/GameInstallInfo, or game_product_info for ProductDetails)
            using var command =
                new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND (name='DbSet' OR name='game_product_info')",
                    connection);
            var result = command.ExecuteScalar();

            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Amazon Games database at {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Read games from Amazon Games database
    /// </summary>
    public async Task<List<Game>> ReadGamesFromDatabaseAsync(string? customPath = null)
    {
        var allGames = new List<Game>();

        // Try to read from both ProductDetails and GameProductInfo databases
        string? productInfoPath = null;
        string? productDetailsPath = null;

        // If no custom path, try to find both databases
        if (string.IsNullOrEmpty(customPath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var sqlFolder = Path.Combine(localAppData, @"Amazon Games\Data\Games\Sql");
            productInfoPath = Path.Combine(sqlFolder, "GameProductInfo.sqlite");
            productDetailsPath = Path.Combine(sqlFolder, "ProductDetails.sqlite");
        }
        else
        {
            // Get the folder containing the uploaded database
            var folder = Path.GetDirectoryName(customPath);
            if (!string.IsNullOrEmpty(folder))
            {
                productInfoPath = Path.Combine(folder, "GameProductInfo.sqlite");
                productDetailsPath = Path.Combine(folder, "ProductDetails.sqlite");
            }
        }

        // Read from ProductDetails if it exists (primary source with rich metadata)
        if (!string.IsNullOrEmpty(productDetailsPath) && File.Exists(productDetailsPath))
        {
            try
            {
                var detailsGames = await ReadFromSingleDatabaseAsync(productDetailsPath);
                allGames.AddRange(detailsGames);
                _logger.LogInformation("Read {Count} games from ProductDetails.sqlite", detailsGames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from ProductDetails database");
            }
        }

        // Read from GameProductInfo if it exists (additional owned games)
        if (!string.IsNullOrEmpty(productInfoPath) && File.Exists(productInfoPath))
        {
            try
            {
                var ownedGames = await ReadFromSingleDatabaseAsync(productInfoPath);
                
                // Only add games that aren't already in the list (avoid duplicates)
                var newGamesCount = 0;
                foreach (var game in ownedGames)
                {
                    if (!allGames.Any(g => g.Id == game.Id))
                    {
                        allGames.Add(game);
                        newGamesCount++;
                    }
                }
                
                _logger.LogInformation("Read {Count} additional games from GameProductInfo.sqlite (total: {Total})", 
                    newGamesCount, allGames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from GameProductInfo database");
            }
        }

        if (allGames.Count == 0)
        {
            _logger.LogWarning("No Amazon Games found in any database");
        }
        else
        {
            _logger.LogInformation("Total games imported from Amazon databases: {Count}", allGames.Count);
        }

        return allGames;
    }

    /// <summary>
    /// Read games from a single Amazon Games database file
    /// </summary>
    private async Task<List<Game>> ReadFromSingleDatabaseAsync(string dbPath)
    {
        var games = new List<Game>();

        if (!File.Exists(dbPath))
        {
            _logger.LogWarning("Amazon Games database file does not exist: {Path}", dbPath);
            return games;
        }

        try
        {
            // Create a temporary copy to avoid file locking issues
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"amazon_games_{Guid.NewGuid()}.sqlite");
            File.Copy(dbPath, tempDbPath, true);

            try
            {
                var connectionString = $"Data Source={tempDbPath};Mode=ReadOnly";
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                _logger.LogInformation("Connected to Amazon Games database");

                // Determine which database type we're reading from by checking table structure
                var tablesQuery = "SELECT name FROM sqlite_master WHERE type='table'";
                var tables = new List<string>();
                using (var tablesCommand = new SqliteCommand(tablesQuery, connection))
                using (var tablesReader = await tablesCommand.ExecuteReaderAsync())
                {
                    while (await tablesReader.ReadAsync())
                    {
                        tables.Add(tablesReader.GetString(0));
                    }
                }
                
                // ProductDetails has game_product_info table
                if (tables.Contains("game_product_info"))
                {
                    _logger.LogInformation("Reading from ProductDetails database (owned games with rich metadata)");
                    games.AddRange(await ReadOwnedGamesFromProductDetailsAsync(connection));
                }
                // DbSet table exists in GameProductInfo and GameInstallInfo
                else if (tables.Contains("DbSet"))
                {
                    bool hasInstalledColumn = false;
                    bool hasProductIdStrColumn = false;
                    
                    var columnsQuery = "PRAGMA table_info(DbSet)";
                    using (var columnsCommand = new SqliteCommand(columnsQuery, connection))
                    using (var columnsReader = await columnsCommand.ExecuteReaderAsync())
                    {
                        while (await columnsReader.ReadAsync())
                        {
                            var columnName = columnsReader.GetString(1);
                            if (columnName == "Installed")
                                hasInstalledColumn = true;
                            if (columnName == "ProductIdStr")
                                hasProductIdStrColumn = true;
                        }
                    }
                    
                    // GameProductInfo has ProductIdStr but not Installed
                    // GameInstallInfo has both
                    var isProductInfoDb = hasProductIdStrColumn && !hasInstalledColumn;
                    
                    if (isProductInfoDb)
                    {
                        _logger.LogInformation("Reading from GameProductInfo database (owned games)");
                        games.AddRange(await ReadOwnedGamesFromProductInfoAsync(connection));
                    }
                    else
                    {
                        _logger.LogInformation("Reading from GameInstallInfo database (installed games only)");
                        games.AddRange(await ReadInstalledGamesAsync(connection));
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown database structure - no recognized tables found");
                }

                _logger.LogInformation("Successfully read {Count} games from Amazon Games database", games.Count);
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempDbPath))
                        File.Delete(tempDbPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary database file");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Amazon Games database from {Path}", dbPath);
            throw;
        }

        return games;
    }

    /// <summary>
    /// Read installed games from the DbSet table
    /// </summary>
    private async Task<List<Game>> ReadInstalledGamesAsync(SqliteConnection connection)
    {
        var games = new List<Game>();

        try
        {
            // First, let's check what tables exist in the database
            var tablesQuery = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            using (var tablesCommand = new SqliteCommand(tablesQuery, connection))
            using (var tablesReader = await tablesCommand.ExecuteReaderAsync())
            {
                _logger.LogInformation("Tables in Amazon Games database:");
                while (await tablesReader.ReadAsync())
                {
                    var tableName = tablesReader.GetString(0);
                    _logger.LogInformation("  - {TableName}", tableName);
                }
            }

            // Check columns in DbSet table
            var columnsQuery = "PRAGMA table_info(DbSet)";
            using (var columnsCommand = new SqliteCommand(columnsQuery, connection))
            using (var columnsReader = await columnsCommand.ExecuteReaderAsync())
            {
                _logger.LogInformation("Columns in DbSet table:");
                while (await columnsReader.ReadAsync())
                {
                    var columnName = columnsReader.GetString(1);
                    var columnType = columnsReader.GetString(2);
                    _logger.LogInformation("  - {ColumnName} ({ColumnType})", columnName, columnType);
                }
            }

            // Query to get ALL games first (not just installed) to see what's in the database
            var countQuery = "SELECT COUNT(*) FROM DbSet";
            using (var countCommand = new SqliteCommand(countQuery, connection))
            {
                var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                _logger.LogInformation("Total games in DbSet table: {Count}", totalCount);
            }

            // Try different possible column name variations
            var installedQuery = "SELECT COUNT(*) FROM DbSet WHERE Installed = 1";
            using (var installedCommand = new SqliteCommand(installedQuery, connection))
            {
                var installedCount = Convert.ToInt32(await installedCommand.ExecuteScalarAsync());
                _logger.LogInformation("Installed games in DbSet table: {Count}", installedCount);
            }

            // Query to get installed games - try without ProductIdStr first, use Id instead
            var query = @"
                SELECT 
                    ProductTitle,
                    Id,
                    InstallDirectory,
                    Installed
                FROM DbSet 
                WHERE Installed = 1
                ORDER BY ProductTitle";

            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var gameCount = 0;
            while (await reader.ReadAsync())
            {
                gameCount++;
                try
                {
                    var title = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var productId = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var installDir = reader.IsDBNull(2) ? null : reader.GetString(2);

                    _logger.LogInformation("Processing game #{Number}: Title={Title}, ProductId={ProductId}, Installed={Installed}", 
                        gameCount, title ?? "(null)", productId ?? "(null)", reader.IsDBNull(3) ? "(null)" : reader.GetInt32(3).ToString());

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(productId))
                    {
                        _logger.LogWarning("Skipping game #{Number} with missing title or product ID", gameCount);
                        continue;
                    }

                    var game = new Game
                    {
                        Id = $"amazon-{productId}",
                        Title = title,
                        Platform = Platform.AmazonGames,
                        AddedToLibrary = DateTime.UtcNow
                    };

                    games.Add(game);
                    _logger.LogInformation("✓ Added Amazon game: {Title} (ID: {ProductId})", title, productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing game #{Number} from database", gameCount);
                }
            }

            _logger.LogInformation("Processed {ProcessedCount} game records, successfully added {AddedCount} games", gameCount, games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading installed games from Amazon Games database");
        }

        return games;
    }

    /// <summary>
    /// Read all owned games from the GameProductInfo.sqlite database
    /// </summary>
    private async Task<List<Game>> ReadOwnedGamesFromProductInfoAsync(SqliteConnection connection)
    {
        var games = new List<Game>();

        try
        {
            // Query to get all owned games with full metadata
            var query = @"
                SELECT 
                    ProductTitle,
                    ProductIdStr,
                    ProductDescription,
                    ProductIconUrl,
                    ProductPublisher,
                    GenresJson,
                    DevelopersJson,
                    ReleaseDate
                FROM DbSet 
                ORDER BY ProductTitle";

            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var gameCount = 0;
            while (await reader.ReadAsync())
            {
                gameCount++;
                try
                {
                    var title = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var productId = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var description = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var iconUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var publisher = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var genresJson = reader.IsDBNull(5) ? null : reader.GetString(5);
                    var developersJson = reader.IsDBNull(6) ? null : reader.GetString(6);
                    var releaseDate = reader.IsDBNull(7) ? null : reader.GetString(7);

                    _logger.LogInformation("Processing owned game #{Number}: Title={Title}, ProductId={ProductId}", 
                        gameCount, title ?? "(null)", productId ?? "(null)");

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(productId))
                    {
                        _logger.LogWarning("Skipping game #{Number} with missing title or product ID", gameCount);
                        continue;
                    }

                    // Parse genres from JSON array
                    var genres = new List<string>();
                    if (!string.IsNullOrWhiteSpace(genresJson))
                    {
                        try
                        {
                            var genresArray = System.Text.Json.JsonSerializer.Deserialize<string[]>(genresJson);
                            if (genresArray != null)
                                genres.AddRange(genresArray);
                        }
                        catch { /* Ignore JSON parsing errors */ }
                    }

                    // Parse developer from JSON array
                    string? developer = null;
                    if (!string.IsNullOrWhiteSpace(developersJson))
                    {
                        try
                        {
                            var developersArray = System.Text.Json.JsonSerializer.Deserialize<string[]>(developersJson);
                            if (developersArray != null && developersArray.Length > 0)
                                developer = developersArray[0];
                        }
                        catch { /* Ignore JSON parsing errors */ }
                    }

                    // Parse release date
                    DateTime? releaseDateParsed = null;
                    if (!string.IsNullOrWhiteSpace(releaseDate))
                    {
                        if (DateTime.TryParse(releaseDate, out var parsedDate))
                            releaseDateParsed = parsedDate;
                    }

                    var game = new Game
                    {
                        Id = $"amazon-{productId}",
                        Title = title,
                        Platform = Platform.AmazonGames,
                        AddedToLibrary = DateTime.UtcNow,
                        Description = description,
                        CoverImageUrl = iconUrl,
                        Publisher = publisher,
                        Developer = developer,
                        Genres = genres,
                        ReleaseDate = releaseDateParsed
                    };

                    games.Add(game);
                    _logger.LogInformation("✓ Added owned Amazon game: {Title} (ID: {ProductId})", title, productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing game #{Number} from GameProductInfo database", gameCount);
                }
            }

            _logger.LogInformation("Processed {ProcessedCount} game records from GameProductInfo, successfully added {AddedCount} games", gameCount, games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading owned games from GameProductInfo database");
        }

        return games;
    }

    /// <summary>
    /// Read owned games from ProductDetails database (JSON-based storage)
    /// </summary>
    private async Task<List<Game>> ReadOwnedGamesFromProductDetailsAsync(SqliteConnection connection)
    {
        var games = new List<Game>();

        try
        {
            var query = @"
                SELECT 
                    key,
                    CAST(value AS TEXT) as json_value
                FROM game_product_info 
                WHERE key LIKE 'amzn1.adg.product.%'
                ORDER BY key";

            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var gameCount = 0;
            while (await reader.ReadAsync())
            {
                gameCount++;
                try
                {
                    var productKey = reader.GetString(0);
                    var jsonValue = reader.GetString(1);

                    // Parse the JSON to extract game details
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonValue);
                    var root = jsonDoc.RootElement;

                    // Extract basic fields
                    var title = root.GetProperty("ProductTitle").GetString();
                    var productId = root.GetProperty("ProductId").GetProperty("Id").GetString();
                    var description = root.TryGetProperty("ProductDescription", out var descProp) ? descProp.GetString() : null;
                    var iconUrl = root.TryGetProperty("ProductIconUrl", out var iconProp) ? iconProp.GetString() : null;
                    var publisher = root.TryGetProperty("ProductPublisher", out var pubProp) ? pubProp.GetString() : null;
                    
                    _logger.LogInformation("Processing game #{Number}: {Title} (ID: {ProductId})", 
                        gameCount, title ?? "(null)", productId ?? "(null)");

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(productId))
                    {
                        _logger.LogWarning("Skipping game #{Number} with missing title or product ID", gameCount);
                        continue;
                    }

                    // Extract genres
                    var genres = new List<string>();
                    if (root.TryGetProperty("Genres", out var genresProp) && genresProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var genre in genresProp.EnumerateArray())
                        {
                            if (genre.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var genreStr = genre.GetString();
                                if (!string.IsNullOrWhiteSpace(genreStr))
                                    genres.Add(genreStr);
                            }
                        }
                    }

                    // Extract developers
                    string? developer = null;
                    if (root.TryGetProperty("Developers", out var devsProp) && devsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var devsArray = devsProp.EnumerateArray().ToList();
                        if (devsArray.Count > 0 && devsArray[0].ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            developer = devsArray[0].GetString();
                        }
                    }

                    // Parse release date
                    DateTime? releaseDateParsed = null;
                    if (root.TryGetProperty("ReleaseDate", out var releaseProp))
                    {
                        var releaseDateStr = releaseProp.GetString();
                        if (!string.IsNullOrWhiteSpace(releaseDateStr) && DateTime.TryParse(releaseDateStr, out var parsedDate))
                        {
                            releaseDateParsed = parsedDate;
                        }
                    }

                    var game = new Game
                    {
                        Id = $"amazon-{productId}",
                        Title = title,
                        Platform = Platform.AmazonGames,
                        AddedToLibrary = DateTime.UtcNow,
                        Description = description,
                        CoverImageUrl = iconUrl,
                        Publisher = publisher,
                        Developer = developer,
                        Genres = genres,
                        ReleaseDate = releaseDateParsed
                    };

                    games.Add(game);
                    _logger.LogInformation("✓ Added Amazon game from ProductDetails: {Title} (ID: {ProductId})", title, productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing game #{Number} from ProductDetails database", gameCount);
                }
            }

            _logger.LogInformation("Processed {ProcessedCount} game records from ProductDetails, successfully added {AddedCount} games", gameCount, games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading owned games from ProductDetails database");
        }

        return games;
    }

    /// <summary>
    /// Import games from uploaded database file
    /// </summary>
    public async Task<List<Game>> ImportFromUploadedDatabaseAsync(Stream databaseStream)
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"amazon_games_upload_{Guid.NewGuid()}.sqlite");

        try
        {
            // Save uploaded stream to temp file
            using (var fileStream = File.Create(tempDbPath))
            {
                await databaseStream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Saved uploaded Amazon Games database to temporary location");

            // Validate it's a valid Amazon Games database
            if (!IsValidAmazonGamesDatabase(tempDbPath))
            {
                throw new InvalidOperationException("The uploaded file is not a valid Amazon Games database");
            }

            // Read games from the uploaded database
            return await ReadGamesFromDatabaseAsync(tempDbPath);
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempDbPath))
                    File.Delete(tempDbPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary database file");
            }
        }
    }
}
