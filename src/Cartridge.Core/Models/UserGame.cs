namespace Cartridge.Core.Models;

public class UserGame
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string? ExternalId { get; set; }
    public string? CoverImageUrl { get; set; }
    public int? PlaytimeMinutes { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsManuallyAdded { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Game metadata
    public string? Description { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public string? Genres { get; set; } // Stored as comma-separated string
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
}


