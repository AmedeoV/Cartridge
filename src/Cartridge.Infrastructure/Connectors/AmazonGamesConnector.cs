using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.AmazonGames;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// Amazon Games connector
/// Supports Amazon Games local database reading via upload
/// </summary>
public class AmazonGamesConnector : IPlatformConnector
{
    private readonly ILogger<AmazonGamesConnector> _logger;
    private readonly AmazonGamesDatabaseReader _databaseReader;
    
    // In-memory storage for demo - replace with database in production
    private static readonly Dictionary<string, bool> _userConnections = new();
    private static readonly Dictionary<string, string> _customDatabasePaths = new();

    public Platform PlatformType => Platform.AmazonGames;

    public AmazonGamesConnector(
        ILogger<AmazonGamesConnector> logger,
        AmazonGamesDatabaseReader databaseReader)
    {
        _logger = logger;
        _databaseReader = databaseReader;
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        return Task.FromResult(_userConnections.ContainsKey(userId) && _userConnections[userId]);
    }

    public async Task<List<Game>> FetchGamesAsync(string userId)
    {
        // Check if user has connected their Amazon Games account
        if (!_userConnections.TryGetValue(userId, out var isConnected) || !isConnected)
        {
            _logger.LogWarning("No Amazon Games account connected for user {UserId}", userId);
            return new List<Game>();
        }

        _logger.LogInformation("Fetching Amazon Games for user {UserId}", userId);
        
        var games = new List<Game>();

        // Try to read from Amazon Games database
        if (_databaseReader.IsAmazonGamesInstalled())
        {
            try
            {
                _logger.LogInformation("Amazon Games detected, reading from local database...");
                
                // Check if user has a custom path
                string? customPath = null;
                _customDatabasePaths.TryGetValue(userId, out customPath);
                
                var amazonGames = await _databaseReader.ReadGamesFromDatabaseAsync(customPath);
                games.AddRange(amazonGames);
                
                _logger.LogInformation("Found {Count} Amazon Games for user {UserId}", amazonGames.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from Amazon Games database");
            }
        }
        else if (_customDatabasePaths.TryGetValue(userId, out var customPath))
        {
            // User has a custom path but Amazon Games not in standard location
            try
            {
                _logger.LogInformation("Trying custom Amazon Games path for user {UserId}", userId);
                var amazonGames = await _databaseReader.ReadGamesFromDatabaseAsync(customPath);
                games.AddRange(amazonGames);
                
                _logger.LogInformation("Found {Count} Amazon Games for user {UserId}", amazonGames.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from custom Amazon Games database path");
            }
        }
        else
        {
            _logger.LogWarning("Amazon Games not detected and no custom path set for user {UserId}", userId);
        }

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
                if (_databaseReader.IsValidAmazonGamesDatabase(dbPath))
                {
                    _customDatabasePaths[userId] = dbPath;
                    _userConnections[userId] = true;
                    _logger.LogInformation("Connected Amazon Games with uploaded database for user {UserId}", userId);
                    return Task.FromResult(true);
                }
                _logger.LogWarning("Invalid uploaded Amazon Games database");
                return Task.FromResult(false);
            }
            
            // Check if it's a custom path request
            if (credentials.StartsWith("custom-path:"))
            {
                var customPath = credentials.Substring("custom-path:".Length);
                if (_databaseReader.IsValidAmazonGamesDatabase(customPath))
                {
                    _customDatabasePaths[userId] = customPath;
                    _userConnections[userId] = true;
                    _logger.LogInformation("Connected Amazon Games with custom path for user {UserId}", userId);
                    return Task.FromResult(true);
                }
                _logger.LogWarning("Invalid custom Amazon Games database path provided");
                return Task.FromResult(false);
            }
            
            _userConnections[userId] = true;
            _logger.LogInformation("Connected Amazon Games for user {UserId}", userId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string userId)
    {
        _userConnections.Remove(userId);
        _customDatabasePaths.Remove(userId);
        _logger.LogInformation("Amazon Games disconnected for user {UserId}", userId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Set a custom database path for a user
    /// </summary>
    public void SetCustomDatabasePath(string userId, string path)
    {
        if (_databaseReader.IsValidAmazonGamesDatabase(path))
        {
            _customDatabasePaths[userId] = path;
            _logger.LogInformation("Set custom Amazon Games database path for user {UserId}", userId);
        }
        else
        {
            throw new InvalidOperationException("Invalid Amazon Games database path");
        }
    }

    /// <summary>
    /// Import games from uploaded database
    /// </summary>
    public async Task<List<Game>> ImportFromUploadedDatabaseAsync(string userId, Stream databaseStream)
    {
        try
        {
            _logger.LogInformation("Importing Amazon Games from uploaded database for user {UserId}", userId);
            
            var games = await _databaseReader.ImportFromUploadedDatabaseAsync(databaseStream);
            
            // Mark user as connected after successful import
            if (games.Any())
            {
                _userConnections[userId] = true;
            }
            
            _logger.LogInformation("Successfully imported {Count} Amazon Games for user {UserId}", games.Count, userId);
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Amazon Games database for user {UserId}", userId);
            throw;
        }
    }
}
