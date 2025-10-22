using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors
{
    /// <summary>
    /// Connector for Rockstar Games Launcher - Currently not supported.
    /// This platform integration is not yet implemented.
    /// </summary>
    public class RockstarConnector : IPlatformConnector
    {
        private readonly ILogger<RockstarConnector> _logger;

        public RockstarConnector(ILogger<RockstarConnector> logger)
        {
            _logger = logger;
        }

        public Platform PlatformType => Platform.Rockstar;

        public Task<bool> IsConnectedAsync(string userId)
        {
            _logger.LogInformation("Rockstar Games Launcher integration not yet supported.");
            return Task.FromResult(false);
        }

        public Task<List<Game>> FetchGamesAsync(string userId)
        {
            _logger.LogInformation("Rockstar Games Launcher integration not yet supported for user {UserId}.", userId);
            return Task.FromResult(new List<Game>());
        }

        public Task<bool> ConnectAsync(string userId, string credentials)
        {
            _logger.LogInformation("Rockstar Games Launcher integration not yet supported.");
            return Task.FromResult(false);
        }

        public Task DisconnectAsync(string userId)
        {
            _logger.LogInformation("Rockstar Games Launcher disconnect called for user {UserId} - no action needed", userId);
            return Task.CompletedTask;
        }
    }
}
