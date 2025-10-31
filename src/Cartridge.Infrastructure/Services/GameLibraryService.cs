using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Data;
using Cartridge.Infrastructure.GameSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Services;

/// <summary>
/// Implementation of Game Library Service with database persistence
/// </summary>
public class GameLibraryService : IGameLibraryService
{
    private readonly IEnumerable<IPlatformConnector> _platformConnectors;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly RawgApiClient _rawgApiClient;
    private readonly ILogger<GameLibraryService> _logger;

    public GameLibraryService(
        IEnumerable<IPlatformConnector> platformConnectors,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        RawgApiClient rawgApiClient,
        ILogger<GameLibraryService> logger)
    {
        _platformConnectors = platformConnectors;
        _contextFactory = contextFactory;
        _rawgApiClient = rawgApiClient;
        _logger = logger;
    }

    public async Task<UserLibrary> GetUserLibraryAsync(string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var library = new UserLibrary
        {
            UserId = userId,
            LastSync = DateTime.UtcNow
        };

        // Get games from database
        var userGames = await context.UserGames
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.LastPlayed ?? g.AddedAt)
            .ToListAsync();
        
        library.Games = userGames.Select(ug => new Game
        {
            Id = ug.ExternalId ?? ug.Id,
            Title = ug.Title,
            Platform = ug.Platform,
            CoverImageUrl = ug.CoverImageUrl,
            PlaytimeMinutes = ug.PlaytimeMinutes,
            LastPlayed = ug.LastPlayed,
            AddedToLibrary = ug.AddedAt,
            Description = ug.Description,
            ReleaseDate = ug.ReleaseDate,
            Developer = ug.Developer,
            Publisher = ug.Publisher,
            IsManuallyAdded = ug.IsManuallyAdded,
            Genres = string.IsNullOrEmpty(ug.Genres) 
                ? new List<string>() 
                : ug.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        }).ToList();
        
        // Get connected platforms from database
        var connections = await context.PlatformConnections
            .Where(c => c.UserId == userId && c.IsConnected)
            .ToListAsync();
        
        foreach (var conn in connections)
        {
            library.ConnectedPlatforms[conn.Platform] = true;
        }
        
        // Also mark platforms as connected if we have games from them
        // (for platforms that don't have explicit connections)
        foreach (var platform in library.Games.Select(g => g.Platform).Distinct())
        {
            if (!library.ConnectedPlatforms.ContainsKey(platform))
            {
                library.ConnectedPlatforms[platform] = true;
            }
        }

        return library;
    }

    public async Task<List<Game>> GetGamesByPlatformAsync(string userId, Platform platform)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var userGames = await context.UserGames
            .Where(g => g.UserId == userId && g.Platform == platform)
            .OrderByDescending(g => g.LastPlayed ?? g.AddedAt)
            .ToListAsync();
        
        return userGames.Select(ug => new Game
        {
            Id = ug.ExternalId ?? ug.Id,
            Title = ug.Title,
            Platform = ug.Platform,
            CoverImageUrl = ug.CoverImageUrl,
            PlaytimeMinutes = ug.PlaytimeMinutes,
            LastPlayed = ug.LastPlayed,
            AddedToLibrary = ug.AddedAt,
            Description = ug.Description,
            ReleaseDate = ug.ReleaseDate,
            Developer = ug.Developer,
            Publisher = ug.Publisher,
            IsManuallyAdded = ug.IsManuallyAdded,
            Genres = string.IsNullOrEmpty(ug.Genres) 
                ? new List<string>() 
                : ug.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        }).ToList();
    }

    public async Task<Game?> GetGameByIdAsync(string gameId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var userGame = await context.UserGames
            .FirstOrDefaultAsync(g => g.Id == gameId || g.ExternalId == gameId);
        
        if (userGame == null)
            return null;
        
        return new Game
        {
            Id = userGame.ExternalId ?? userGame.Id,
            Title = userGame.Title,
            Platform = userGame.Platform,
            CoverImageUrl = userGame.CoverImageUrl,
            PlaytimeMinutes = userGame.PlaytimeMinutes,
            LastPlayed = userGame.LastPlayed,
            AddedToLibrary = userGame.AddedAt,
            Description = userGame.Description,
            ReleaseDate = userGame.ReleaseDate,
            Developer = userGame.Developer,
            Publisher = userGame.Publisher,
            IsManuallyAdded = userGame.IsManuallyAdded,
            Genres = string.IsNullOrEmpty(userGame.Genres) 
                ? new List<string>() 
                : userGame.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        };
    }

    public async Task SyncLibraryAsync(string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Force refresh from all connected platforms
        var connections = await context.PlatformConnections
            .Where(c => c.UserId == userId && c.IsConnected)
            .ToListAsync();
        
        foreach (var connection in connections)
        {
            var connector = _platformConnectors.FirstOrDefault(c => c.PlatformType == connection.Platform);
            if (connector != null && await connector.IsConnectedAsync(userId))
            {
                var games = await connector.FetchGamesAsync(userId);
                
                // Update games in database
                foreach (var game in games)
                {
                    var existing = await context.UserGames
                        .FirstOrDefaultAsync(g => g.UserId == userId && 
                                                g.ExternalId == game.Id && 
                                                g.Platform == game.Platform);
                    
                    // Enrich game metadata from RAWG API if description is missing
                    // Check both new games and existing games without descriptions
                    bool needsEnrichment = string.IsNullOrEmpty(game.Description) || 
                                          (existing != null && string.IsNullOrEmpty(existing.Description));
                    
                    if (needsEnrichment)
                    {
                        _logger.LogInformation("🔍 Enriching metadata for: {GameTitle}", game.Title);
                        var enriched = await _rawgApiClient.EnrichGameMetadataAsync(game);
                        if (enriched)
                        {
                            _logger.LogInformation("✅ Successfully enriched {GameTitle} - Description: {DescLength} chars", 
                                game.Title, game.Description?.Length ?? 0);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Failed to enrich metadata for: {GameTitle}", game.Title);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("ℹ️ Game {GameTitle} already has description, skipping enrichment", game.Title);
                    }
                    
                    if (existing == null)
                    {
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
                        
                        context.UserGames.Add(userGame);
                    }
                    else
                    {
                        existing.Title = game.Title;
                        existing.CoverImageUrl = game.CoverImageUrl;
                        existing.PlaytimeMinutes = game.PlaytimeMinutes;
                        existing.LastPlayed = game.LastPlayed;
                        existing.UpdatedAt = DateTime.UtcNow;
                        
                        // Only update metadata if not already set (preserve existing data)
                        if (string.IsNullOrEmpty(existing.Description))
                            existing.Description = game.Description;
                        if (!existing.ReleaseDate.HasValue)
                            existing.ReleaseDate = game.ReleaseDate;
                        if (string.IsNullOrEmpty(existing.Developer))
                            existing.Developer = game.Developer;
                        if (string.IsNullOrEmpty(existing.Publisher))
                            existing.Publisher = game.Publisher;
                        if (string.IsNullOrEmpty(existing.Genres))
                            existing.Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null;
                    }
                }
                
                connection.LastSyncedAt = DateTime.UtcNow;
            }
        }
        
        await context.SaveChangesAsync();
    }

    public async Task<Game> AddManualGameAsync(string userId, Game game)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Check if game already exists
        var existing = await context.UserGames
            .FirstOrDefaultAsync(g => g.UserId == userId && 
                                    g.Title == game.Title && 
                                    g.Platform == game.Platform);

        if (existing != null)
        {
            // Return the existing game
            return new Game
            {
                Id = existing.Id,
                Title = existing.Title,
                Platform = existing.Platform,
                CoverImageUrl = existing.CoverImageUrl,
                PlaytimeMinutes = existing.PlaytimeMinutes,
                LastPlayed = existing.LastPlayed,
                AddedToLibrary = existing.AddedAt,
                Description = existing.Description,
                ReleaseDate = existing.ReleaseDate,
                Developer = existing.Developer,
                Publisher = existing.Publisher,
                IsManuallyAdded = existing.IsManuallyAdded,
                Genres = string.IsNullOrEmpty(existing.Genres) 
                    ? new List<string>() 
                    : existing.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
            };
        }

        var userGame = new UserGame
        {
            UserId = userId,
            Title = game.Title,
            Platform = game.Platform,
            ExternalId = game.Id != Guid.NewGuid().ToString() ? game.Id : null,
            CoverImageUrl = game.CoverImageUrl,
            PlaytimeMinutes = game.PlaytimeMinutes,
            LastPlayed = game.LastPlayed,
            IsManuallyAdded = true,
            AddedAt = DateTime.UtcNow,
            Description = game.Description,
            ReleaseDate = game.ReleaseDate,
            Developer = game.Developer,
            Publisher = game.Publisher,
            Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null
        };

        context.UserGames.Add(userGame);
        await context.SaveChangesAsync();

        return new Game
        {
            Id = userGame.Id,
            Title = userGame.Title,
            Platform = userGame.Platform,
            CoverImageUrl = userGame.CoverImageUrl,
            PlaytimeMinutes = userGame.PlaytimeMinutes,
            LastPlayed = userGame.LastPlayed,
            AddedToLibrary = userGame.AddedAt,
            Description = userGame.Description,
            ReleaseDate = userGame.ReleaseDate,
            Developer = userGame.Developer,
            Publisher = userGame.Publisher,
            IsManuallyAdded = userGame.IsManuallyAdded,
            Genres = string.IsNullOrEmpty(userGame.Genres) 
                ? new List<string>() 
                : userGame.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        };
    }

    public async Task<List<Game>> SearchGamesAsync(string searchQuery)
    {
        return await _rawgApiClient.SearchGamesAsync(searchQuery);
    }

    public async Task<bool> RemoveManualGameAsync(string userId, string gameId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var userGame = await context.UserGames
            .FirstOrDefaultAsync(g => (g.Id == gameId || g.ExternalId == gameId) && g.UserId == userId);

        if (userGame == null)
        {
            _logger.LogWarning("❌ Game {GameId} not found for user {UserId}", gameId, userId);
            return false;
        }

        if (!userGame.IsManuallyAdded)
        {
            _logger.LogWarning("⚠️ Cannot delete game {GameTitle} - it was not manually added", userGame.Title);
            return false;
        }

        _logger.LogInformation("🗑️ Removing manually added game: {GameTitle}", userGame.Title);
        context.UserGames.Remove(userGame);
        await context.SaveChangesAsync();
        
        return true;
    }
}
