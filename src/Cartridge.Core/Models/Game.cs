namespace Cartridge.Core.Models;

/// <summary>
/// Represents a game in the user's library
/// </summary>
public class Game
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Platform Platform { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime AddedToLibrary { get; set; }
    public int? PlaytimeMinutes { get; set; }
    public DateTime? LastPlayed { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public bool IsManuallyAdded { get; set; }
}
