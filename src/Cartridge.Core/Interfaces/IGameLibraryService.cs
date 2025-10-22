using Cartridge.Core.Models;

namespace Cartridge.Core.Interfaces;

/// <summary>
/// Service for managing game library operations
/// </summary>
public interface IGameLibraryService
{
    Task<UserLibrary> GetUserLibraryAsync(string userId);
    Task<List<Game>> GetGamesByPlatformAsync(string userId, Platform platform);
    Task<Game?> GetGameByIdAsync(string gameId);
    Task SyncLibraryAsync(string userId);
}
