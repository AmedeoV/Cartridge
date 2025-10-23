using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Data;
using Cartridge.Infrastructure.GameSearch;
using Microsoft.EntityFrameworkCore;

namespace Cartridge.Infrastructure.Services;

/// <summary>
/// Implementation of Game Library Service with database persistence
/// </summary>
public class GameLibraryService : IGameLibraryService
{
    private readonly IEnumerable<IPlatformConnector> _platformConnectors;
    private readonly ApplicationDbContext _context;
    private readonly RawgApiClient _rawgApiClient;

    public GameLibraryService(
        IEnumerable<IPlatformConnector> platformConnectors,
        ApplicationDbContext context,
        RawgApiClient rawgApiClient)
    {
        _platformConnectors = platformConnectors;
        _context = context;
        _rawgApiClient = rawgApiClient;
    }

    public async Task<UserLibrary> GetUserLibraryAsync(string userId)
    {
        var library = new UserLibrary
        {
            UserId = userId,
            LastSync = DateTime.UtcNow
        };

        // Get games from database
        var userGames = await _context.UserGames
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
            Genres = string.IsNullOrEmpty(ug.Genres) 
                ? new List<string>() 
                : ug.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        }).ToList();
        
        // Get connected platforms from database
        var connections = await _context.PlatformConnections
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
        var userGames = await _context.UserGames
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
            Genres = string.IsNullOrEmpty(ug.Genres) 
                ? new List<string>() 
                : ug.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        }).ToList();
    }

    public async Task<Game?> GetGameByIdAsync(string gameId)
    {
        var userGame = await _context.UserGames
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
            Genres = string.IsNullOrEmpty(userGame.Genres) 
                ? new List<string>() 
                : userGame.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        };
    }

    public async Task SyncLibraryAsync(string userId)
    {
        // Force refresh from all connected platforms
        var connections = await _context.PlatformConnections
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
                    var existing = await _context.UserGames
                        .FirstOrDefaultAsync(g => g.UserId == userId && 
                                                g.ExternalId == game.Id && 
                                                g.Platform == game.Platform);
                    
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
                        
                        _context.UserGames.Add(userGame);
                    }
                    else
                    {
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
                    }
                }
                
                connection.LastSyncedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task<Game> AddManualGameAsync(string userId, Game game)
    {
        // Check if game already exists
        var existing = await _context.UserGames
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

        _context.UserGames.Add(userGame);
        await _context.SaveChangesAsync();

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
            Genres = string.IsNullOrEmpty(userGame.Genres) 
                ? new List<string>() 
                : userGame.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
        };
    }

    public async Task<List<Game>> SearchGamesAsync(string searchQuery)
    {
        return await _rawgApiClient.SearchGamesAsync(searchQuery);
    }
}
