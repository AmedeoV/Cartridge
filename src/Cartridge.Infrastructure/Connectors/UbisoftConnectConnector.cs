using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// Connector for Ubisoft Connect (formerly Uplay) - Currently does not support automatic import.
/// Users should use the GOG import functionality to manually import their Ubisoft Connect library.
/// </summary>
public class UbisoftConnectConnector : IPlatformConnector
{
    private readonly ILogger<UbisoftConnectConnector> _logger;

    public Platform PlatformType => Platform.UbisoftConnect;

    public UbisoftConnectConnector(ILogger<UbisoftConnectConnector> logger)
    {
        _logger = logger;
    }

    public Task<bool> ConnectAsync(string userId, string credentials)
    {
        _logger.LogInformation("Ubisoft Connect direct integration not available. Please use the GOG import functionality to manually import your Ubisoft Connect library.");
        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string userId)
    {
        _logger.LogInformation("Ubisoft Connect disconnect called for user {UserId} - no action needed", userId);
        return Task.CompletedTask;
    }

    public Task<List<Game>> FetchGamesAsync(string userId)
    {
        _logger.LogInformation("Ubisoft Connect direct integration not available for user {UserId}. Please use GOG import to add Ubisoft Connect games manually.", userId);
        return Task.FromResult(new List<Game>());
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        // Ubisoft Connect integration not available - direct users to GOG import
        _logger.LogInformation("Ubisoft Connect direct integration not available. Please use GOG import functionality.");
        return Task.FromResult(false);
    }
}
