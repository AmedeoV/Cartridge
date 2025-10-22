using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;

namespace Cartridge.Infrastructure.Services;

/// <summary>
/// Mock implementation of Game Library Service for development
/// </summary>
public class GameLibraryService : IGameLibraryService
{
    private readonly IEnumerable<IPlatformConnector> _platformConnectors;

    public GameLibraryService(IEnumerable<IPlatformConnector> platformConnectors)
    {
        _platformConnectors = platformConnectors;
    }

    public async Task<UserLibrary> GetUserLibraryAsync(string userId)
    {
        var library = new UserLibrary
        {
            UserId = userId,
            LastSync = DateTime.UtcNow
        };

        // Fetch games from all connected platforms
        foreach (var connector in _platformConnectors)
        {
            if (await connector.IsConnectedAsync(userId))
            {
                var games = await connector.FetchGamesAsync(userId);
                library.Games.AddRange(games);
                library.ConnectedPlatforms[connector.PlatformType] = true;
            }
        }

        // Also mark platforms as connected if we have games from them
        // (e.g., Epic games imported via GOG Galaxy database)
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
        var library = await GetUserLibraryAsync(userId);
        return library.Games.Where(g => g.Platform == platform).ToList();
    }

    public Task<Game?> GetGameByIdAsync(string gameId)
    {
        // TODO: Implement proper game lookup
        return Task.FromResult<Game?>(null);
    }

    public async Task SyncLibraryAsync(string userId)
    {
        // Force refresh from all platforms
        foreach (var connector in _platformConnectors)
        {
            if (await connector.IsConnectedAsync(userId))
            {
                await connector.FetchGamesAsync(userId);
            }
        }
    }
}
