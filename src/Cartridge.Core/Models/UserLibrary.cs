namespace Cartridge.Core.Models;

/// <summary>
/// Represents a user's complete game library across all platforms
/// </summary>
public class UserLibrary
{
    public string UserId { get; set; } = string.Empty;
    public List<Game> Games { get; set; } = new();
    public DateTime LastSync { get; set; }
    public Dictionary<Platform, bool> ConnectedPlatforms { get; set; } = new();
}
