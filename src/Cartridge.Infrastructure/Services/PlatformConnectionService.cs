using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Data;
using Cartridge.Infrastructure.GameSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Services;

/// <summary>
/// Service for managing platform connections
/// </summary>
public interface IPlatformConnectionService
{
    Task<bool> ConnectPlatformAsync(string userId, Platform platform, string credentials);
    Task DisconnectPlatformAsync(string userId, Platform platform);
    Task<bool> IsPlatformConnectedAsync(string userId, Platform platform);
    Task<string?> GetPlatformCredentialsAsync(string userId, Platform platform);
}

public class PlatformConnectionService : IPlatformConnectionService
{
    private readonly IEnumerable<IPlatformConnector> _connectors;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlatformConnectionService> _logger;
    private readonly RawgApiClient _rawgApiClient;

    public PlatformConnectionService(
        IEnumerable<IPlatformConnector> connectors,
        ApplicationDbContext context,
        ILogger<PlatformConnectionService> logger,
        RawgApiClient rawgApiClient)
    {
        _connectors = connectors;
        _context = context;
        _logger = logger;
        _rawgApiClient = rawgApiClient;
    }

    public async Task<bool> ConnectPlatformAsync(string userId, Platform platform, string credentials)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector == null)
            return false;

        var success = await connector.ConnectAsync(userId, credentials);
        
        if (success)
        {
            // Fetch games from the platform
            var games = await connector.FetchGamesAsync(userId);
            
            // Track games that need enrichment
            var gamesToEnrich = new List<(string GameId, string Title)>();
            
            // Save games to database immediately (without waiting for enrichment)
            foreach (var game in games)
            {
                // Check if game already exists for this user
                var existing = await _context.UserGames
                    .FirstOrDefaultAsync(g => g.UserId == userId && 
                                            g.ExternalId == game.Id && 
                                            g.Platform == game.Platform);
                
                // Check if enrichment is needed
                bool needsEnrichment = string.IsNullOrEmpty(game.Description) || 
                                      (existing != null && string.IsNullOrEmpty(existing.Description));
                
                if (existing == null)
                {
                    // Add new game
                    var userGame = new UserGame
                    {
                        UserId = userId,
                        Title = game.Title,
                        Platform = game.Platform,
                        ExternalId = game.Id,
                        CoverImageUrl = game.CoverImageUrl,
                        PlaytimeMinutes = game.PlaytimeMinutes,
                        LastPlayed = game.LastPlayed,
                        IsManuallyAdded = false,
                        AddedAt = DateTime.UtcNow,
                        Description = game.Description,
                        ReleaseDate = game.ReleaseDate,
                        Developer = game.Developer,
                        Publisher = game.Publisher,
                        Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null
                    };
                    
                    _logger.LogDebug("Creating new game {Title}: Description={DescLen} chars, Playtime={Playtime} min", 
                        game.Title, game.Description?.Length ?? 0, game.PlaytimeMinutes ?? 0);
                    
                    _context.UserGames.Add(userGame);
                    
                    if (needsEnrichment)
                    {
                        gamesToEnrich.Add((game.Id, game.Title));
                    }
                }
                else
                {
                    // Update existing game
                    existing.Title = game.Title;
                    existing.CoverImageUrl = game.CoverImageUrl;
                    existing.PlaytimeMinutes = game.PlaytimeMinutes;
                    existing.LastPlayed = game.LastPlayed;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Description = game.Description;
                    existing.ReleaseDate = game.ReleaseDate;
                    existing.Developer = game.Developer;
                    existing.Publisher = game.Publisher;
                    existing.Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null;
                    
                    _logger.LogDebug("Updating game {Title}: Description={DescLen} chars, Playtime={Playtime} min", 
                        game.Title, game.Description?.Length ?? 0, game.PlaytimeMinutes ?? 0);
                    
                    if (needsEnrichment)
                    {
                        gamesToEnrich.Add((game.Id, game.Title));
                    }
                }
            }
            
            // Save or update platform connection
            var connection = await _context.PlatformConnections
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
            
            if (connection == null)
            {
                connection = new PlatformConnection
                {
                    UserId = userId,
                    Platform = platform,
                    IsConnected = true,
                    ConnectedAt = DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow
                };
                _context.PlatformConnections.Add(connection);
            }
            else
            {
                connection.IsConnected = true;
                connection.LastSyncedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
            
            // Start background enrichment for games without descriptions
            if (gamesToEnrich.Any())
            {
                _logger.LogInformation("📝 Queued {Count} games for background metadata enrichment", gamesToEnrich.Count);
                
                // Fire and forget - enrich in background
                _ = Task.Run(async () => await EnrichGamesInBackgroundAsync(userId, platform, gamesToEnrich));
            }
        }
        
        return success;
    }
    
    /// <summary>
    /// Enriches games with RAWG metadata in the background
    /// </summary>
    private async Task EnrichGamesInBackgroundAsync(string userId, Platform platform, List<(string GameId, string Title)> gamesToEnrich)
    {
        try
        {
            _logger.LogInformation("🔄 Starting background enrichment for {Count} games", gamesToEnrich.Count);
            
            var enrichedCount = 0;
            var failedCount = 0;
            
            foreach (var (gameId, title) in gamesToEnrich)
            {
                try
                {
                    // Fetch the game from database
                    var userGame = await _context.UserGames
                        .FirstOrDefaultAsync(g => g.UserId == userId && 
                                                g.ExternalId == gameId && 
                                                g.Platform == platform);
                    
                    if (userGame == null)
                    {
                        _logger.LogWarning("⚠️ Game not found for enrichment: {Title} (ID: {GameId})", title, gameId);
                        failedCount++;
                        continue;
                    }
                    
                    // Skip if description was added while we were waiting
                    if (!string.IsNullOrEmpty(userGame.Description))
                    {
                        _logger.LogDebug("ℹ️ Game {Title} already has description, skipping", title);
                        continue;
                    }
                    
                    // Create a temporary Game object for enrichment
                    var tempGame = new Game
                    {
                        Id = userGame.ExternalId,
                        Title = userGame.Title,
                        Platform = userGame.Platform,
                        Description = userGame.Description ?? string.Empty,
                        Developer = userGame.Developer,
                        Publisher = userGame.Publisher,
                        ReleaseDate = userGame.ReleaseDate,
                        Genres = string.IsNullOrEmpty(userGame.Genres) 
                            ? new List<string>() 
                            : userGame.Genres.Split(", ").ToList()
                    };
                    
                    // Enrich with RAWG data
                    _logger.LogDebug("🔍 Enriching metadata for: {GameTitle}", title);
                    var enriched = await _rawgApiClient.EnrichGameMetadataAsync(tempGame);
                    
                    if (enriched)
                    {
                        // Update the database record
                        userGame.Description = tempGame.Description;
                        userGame.Developer = tempGame.Developer;
                        userGame.Publisher = tempGame.Publisher;
                        userGame.ReleaseDate = tempGame.ReleaseDate;
                        userGame.Genres = tempGame.Genres.Any() ? string.Join(", ", tempGame.Genres) : null;
                        userGame.UpdatedAt = DateTime.UtcNow;
                        
                        await _context.SaveChangesAsync();
                        
                        enrichedCount++;
                        _logger.LogInformation("✅ Enriched {Title} - Description: {DescLength} chars", 
                            title, tempGame.Description?.Length ?? 0);
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("⚠️ Failed to enrich: {Title}", title);
                    }
                    
                    // Small delay to avoid rate limiting
                    await Task.Delay(250);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Error enriching {Title}", title);
                }
            }
            
            _logger.LogInformation("✅ Background enrichment complete: {Enriched} succeeded, {Failed} failed out of {Total} games", 
                enrichedCount, failedCount, gamesToEnrich.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in background enrichment task");
        }
    }

    public async Task DisconnectPlatformAsync(string userId, Platform platform)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector != null)
        {
            await connector.DisconnectAsync(userId);
        }
        
        // Delete all games from this platform for this user
        var gamesToDelete = await _context.UserGames
            .Where(g => g.UserId == userId && g.Platform == platform)
            .ToListAsync();
        
        if (gamesToDelete.Any())
        {
            _context.UserGames.RemoveRange(gamesToDelete);
            _logger.LogInformation("Removing {Count} games from {Platform} for user {UserId}", 
                gamesToDelete.Count, platform, userId);
        }
        
        // Update platform connection in database
        var connection = await _context.PlatformConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
        
        if (connection != null)
        {
            connection.IsConnected = false;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Disconnected {Platform} for user {UserId} and removed associated games", 
                platform, userId);
        }
        else
        {
            // Still save changes to remove games even if no connection record exists
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsPlatformConnectedAsync(string userId, Platform platform)
    {
        // Check database first
        var connection = await _context.PlatformConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
        
        if (connection?.IsConnected == true)
        {
            return true;
        }
        
        // Fall back to connector check (for backward compatibility)
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector == null)
            return false;

        return await connector.IsConnectedAsync(userId);
    }

    public Task<string?> GetPlatformCredentialsAsync(string userId, Platform platform)
    {
        // This would retrieve from database in production
        // For now, returning null as we don't expose credentials
        return Task.FromResult<string?>(null);
    }
}
