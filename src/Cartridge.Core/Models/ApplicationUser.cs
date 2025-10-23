using Microsoft.AspNetCore.Identity;

namespace Cartridge.Core.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserGame> UserGames { get; set; } = new List<UserGame>();
}

