using Cartridge.Core.Models;

namespace Cartridge.Core.Interfaces;

/// <summary>
/// Interface for connecting to external gaming platforms
/// </summary>
public interface IPlatformConnector
{
    Platform PlatformType { get; }
    Task<bool> IsConnectedAsync(string userId);
    Task<List<Game>> FetchGamesAsync(string userId);
    Task<bool> ConnectAsync(string userId, string credentials);
    Task DisconnectAsync(string userId);
}
