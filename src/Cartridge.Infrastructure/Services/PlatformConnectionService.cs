using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlatformConnectionService> _logger;

    public PlatformConnectionService(
        IEnumerable<IPlatformConnector> connectors,
        ApplicationDbContext context,
        ILogger<PlatformConnectionService> logger)
    {
        _connectors = connectors;
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ConnectPlatformAsync(string userId, Platform platform, string credentials)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector == null)
            return false;

        var success = await connector.ConnectAsync(userId, credentials);
        
        if (success)
        {
            // Fetch games from the platform
            var games = await connector.FetchGamesAsync(userId);
            
            // Save games to database
            foreach (var game in games)
            {
                // Check if game already exists for this user
                var existing = await _context.UserGames
                    .FirstOrDefaultAsync(g => g.UserId == userId && 
                                            g.ExternalId == game.Id && 
                                            g.Platform == game.Platform);
                
                if (existing == null)
                {
                    // Add new game
                    var userGame = new UserGame
                    {
                        UserId = userId,
                        Title = game.Title,
                        Platform = game.Platform,
                        ExternalId = game.Id,
                        CoverImageUrl = game.CoverImageUrl,
                        PlaytimeMinutes = game.PlaytimeMinutes,
                        LastPlayed = game.LastPlayed,
                        IsManuallyAdded = false,
                        AddedAt = DateTime.UtcNow,
                        Description = game.Description,
                        ReleaseDate = game.ReleaseDate,
                        Developer = game.Developer,
                        Publisher = game.Publisher,
                        Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null
                    };
                    
                    _logger.LogDebug("Creating new game {Title}: Description={DescLen} chars, Playtime={Playtime} min", 
                        game.Title, game.Description?.Length ?? 0, game.PlaytimeMinutes ?? 0);
                    
                    _context.UserGames.Add(userGame);
                }
                else
                {
                    // Update existing game
                    existing.Title = game.Title;
                    existing.CoverImageUrl = game.CoverImageUrl;
                    existing.PlaytimeMinutes = game.PlaytimeMinutes;
                    existing.LastPlayed = game.LastPlayed;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Description = game.Description;
                    existing.ReleaseDate = game.ReleaseDate;
                    existing.Developer = game.Developer;
                    existing.Publisher = game.Publisher;
                    existing.Genres = game.Genres.Any() ? string.Join(", ", game.Genres) : null;
                    
                    _logger.LogDebug("Updating game {Title}: Description={DescLen} chars, Playtime={Playtime} min", 
                        game.Title, game.Description?.Length ?? 0, game.PlaytimeMinutes ?? 0);
                }
            }
            
            // Save or update platform connection
            var connection = await _context.PlatformConnections
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
            
            if (connection == null)
            {
                connection = new PlatformConnection
                {
                    UserId = userId,
                    Platform = platform,
                    IsConnected = true,
                    ConnectedAt = DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow
                };
                _context.PlatformConnections.Add(connection);
            }
            else
            {
                connection.IsConnected = true;
                connection.LastSyncedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
        }
        
        return success;
    }

    public async Task DisconnectPlatformAsync(string userId, Platform platform)
    {
        var connector = _connectors.FirstOrDefault(c => c.PlatformType == platform);
        if (connector != null)
        {
            await connector.DisconnectAsync(userId);
        }
        
        // Update platform connection in database
        var connection = await _context.PlatformConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
        
        if (connection != null)
        {
            connection.IsConnected = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsPlatformConnectedAsync(string userId, Platform platform)
    {
        // Check database first
        var connection = await _context.PlatformConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == platform);
        
        if (connection?.IsConnected == true)
        {
            return true;
        }
        
        // Fall back to connector check (for backward compatibility)
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
