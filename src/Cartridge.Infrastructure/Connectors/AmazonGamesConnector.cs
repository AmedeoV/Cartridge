using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// Connector for Amazon Games (Prime Gaming) - Currently not supported.
/// This platform integration is not yet implemented.
/// </summary>
public class AmazonGamesConnector : IPlatformConnector
{
    private readonly ILogger<AmazonGamesConnector> _logger;

    public Platform PlatformType => Platform.AmazonGames;

    public AmazonGamesConnector(ILogger<AmazonGamesConnector> logger)
    {
        _logger = logger;
    }

    public Task<bool> ConnectAsync(string userId, string credentials)
    {
        _logger.LogInformation("Amazon Games integration not yet supported.");
        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string userId)
    {
        _logger.LogInformation("Amazon Games disconnect called for user {UserId} - no action needed", userId);
        return Task.CompletedTask;
    }

    public Task<List<Game>> FetchGamesAsync(string userId)
    {
        _logger.LogInformation("Amazon Games integration not yet supported for user {UserId}.", userId);
        return Task.FromResult(new List<Game>());
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        _logger.LogInformation("Amazon Games integration not yet supported.");
        return Task.FromResult(false);
    }
}
