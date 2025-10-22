using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;

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

    public PlatformConnectionService(IEnumerable<IPlatformConnector> connectors)
    {
        _connectors = connectors;
    }

    public async Task<bool> ConnectPlatformAsync(string userId, Platform platform, string credentials)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector == null)
            return false;

        return await connector.ConnectAsync(userId, credentials);
    }

    public async Task DisconnectPlatformAsync(string userId, Platform platform)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector != null)
        {
            await connector.DisconnectAsync(userId);
        }
    }

    public async Task<bool> IsPlatformConnectedAsync(string userId, Platform platform)
    {
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
