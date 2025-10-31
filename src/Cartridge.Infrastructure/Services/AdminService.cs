using Cartridge.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cartridge.Infrastructure.Services;

public interface IAdminService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> ResetUserPasswordAsync(string userId, string newPassword);
    Task<bool> DeleteUserAsync(string userId);
    Task<bool> ToggleAdminStatusAsync(string userId);
    Task<UserDto?> GetUserByIdAsync(string userId);
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int GamesCount { get; set; }
}

public class AdminService : IAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _userManager.Users
            .Include(u => u.UserGames)
            .ToListAsync();

        return users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            DisplayName = u.DisplayName,
            IsAdmin = u.IsAdmin,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            GamesCount = u.UserGames?.Count ?? 0
        }).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.Users
            .Include(u => u.UserGames)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            GamesCount = user.UserGames?.Count ?? 0
        };
    }

    public async Task<bool> ResetUserPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        // Remove existing password
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            return false;

        // Add new password
        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        return addResult.Succeeded;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        // Prevent deleting the last admin
        if (user.IsAdmin)
        {
            var adminCount = await _userManager.Users.CountAsync(u => u.IsAdmin);
            if (adminCount <= 1)
                return false;
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> ToggleAdminStatusAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        // Prevent removing the last admin
        if (user.IsAdmin)
        {
            var adminCount = await _userManager.Users.CountAsync(u => u.IsAdmin);
            if (adminCount <= 1)
                return false;
        }

        user.IsAdmin = !user.IsAdmin;
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }
}
