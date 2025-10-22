using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// Epic Games connector - Currently does not support automatic import.
/// Users should use the GOG import functionality to manually import their Epic Games library.
/// </summary>
public class EpicGamesConnector : IPlatformConnector
{
    private readonly ILogger<EpicGamesConnector> _logger;

    public EpicGamesConnector(ILogger<EpicGamesConnector> logger)
    {
        _logger = logger;
    }

    public Platform PlatformType => Platform.EpicGames;

    public Task<bool> IsConnectedAsync(string userId)
    {
        // Epic Games integration not available - direct users to GOG import
        _logger.LogInformation("Epic Games direct integration not available. Please use GOG import functionality.");
        return Task.FromResult(false);
    }

    public Task<List<Game>> FetchGamesAsync(string userId)
    {
        _logger.LogInformation("Epic Games direct integration not available for user {UserId}. Please use GOG import to add Epic Games manually.", userId);
        return Task.FromResult(new List<Game>());
    }

    public Task<bool> ConnectAsync(string userId, string credentials)
    {
        _logger.LogInformation("Epic Games direct integration not available. Please use the GOG import functionality to manually import your Epic Games library.");
        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string userId)
    {
        _logger.LogInformation("Epic Games disconnect called for user {UserId} - no action needed", userId);
        return Task.CompletedTask;
    }
}
