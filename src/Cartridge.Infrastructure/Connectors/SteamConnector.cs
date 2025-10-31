using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Steam;
using Microsoft.Extensions.Logging;

namespace Cartridge.Infrastructure.Connectors;

/// <summary>
/// Steam connector using real Steam Web API
/// </summary>
public class SteamConnector : IPlatformConnector
{
    private readonly SteamApiClient _steamClient;
    private readonly ILogger<SteamConnector> _logger;
    
    // In-memory storage for demo - replace with database in production
    private static readonly Dictionary<string, string> _userSteamIds = new();

    public Platform PlatformType => Platform.Steam;

    public SteamConnector(SteamApiClient steamClient, ILogger<SteamConnector> logger)
    {
        _steamClient = steamClient;
        _logger = logger;
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        // Check if user has a Steam ID stored
        return Task.FromResult(_userSteamIds.ContainsKey(userId));
    }

    public async Task<List<Game>> FetchGamesAsync(string userId)
    {
        var games = new List<Game>();

        // Check if user has connected their Steam account
        if (!_userSteamIds.TryGetValue(userId, out var steamId))
        {
            _logger.LogError("No Steam ID found for user {UserId}. User must connect their Steam account first.", userId);
            throw new InvalidOperationException("Steam account not connected. Please connect your Steam account first.");
        }

        try
        {
            _logger.LogInformation("Fetching games from Steam API for Steam ID {SteamId}", steamId);
            
            // Fetch owned games from Steam API
            var gamesResponse = await _steamClient.GetOwnedGamesAsync(steamId);
            
            if (gamesResponse == null)
            {
                _logger.LogError("Steam API returned null response. Check if API key is configured correctly.");
                throw new InvalidOperationException("Failed to fetch games from Steam API. Please check your Steam API configuration.");
            }
            
            if (gamesResponse?.Response?.Games == null || gamesResponse.Response.Games.Count == 0)
            {
                _logger.LogWarning("No games found for Steam ID {SteamId}. The account may be private or have no games.", steamId);
                return games;
            }

            _logger.LogInformation("✅ Successfully found {Count} games for Steam ID {SteamId}", 
                gamesResponse.Response.GameCount, steamId);

            // Convert Steam games to our Game model
            // Note: Removed limit of 50 games to include all owned games
            var steamGames = gamesResponse.Response.Games.ToList();

            foreach (var steamGame in steamGames)
            {
                var game = new Game
                {
                    Id = $"steam_{steamGame.AppId}",
                    Title = steamGame.Name ?? "Unknown Game",
                    Platform = Platform.Steam,
                    PlaytimeMinutes = steamGame.PlaytimeForever,
                    AddedToLibrary = DateTime.UtcNow, // Steam API doesn't provide this
                    CoverImageUrl = _steamClient.GetGameLibraryImageUrl(steamGame.AppId)
                };

                // Set last played if available
                if (steamGame.RtimeLastPlayed > 0)
                {
                    game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(steamGame.RtimeLastPlayed).DateTime;
                }

                // Optionally fetch detailed game info (be mindful of rate limits)
                // Commented out to avoid hitting rate limits - enable for production with proper caching
                /*
                var appDetails = await _steamClient.GetAppDetailsAsync(steamGame.AppId);
                if (appDetails != null)
                {
                    game.Description = appDetails.ShortDescription;
                    game.Developer = appDetails.Developers?.FirstOrDefault();
                    game.Publisher = appDetails.Publishers?.FirstOrDefault();
                    game.Genres = appDetails.Genres?.Select(g => g.Description ?? "").Where(d => !string.IsNullOrEmpty(d)).ToList() ?? new List<string>();
                    
                    if (DateTime.TryParse(appDetails.ReleaseDate?.Date, out var releaseDate))
                    {
                        game.ReleaseDate = releaseDate;
                    }
                }
                */

                games.Add(game);
            }

            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching games from Steam for user {UserId}", userId);
            throw;
        }
    }

    public Task<bool> ConnectAsync(string userId, string steamId)
    {
        // Store the Steam ID for the user
        // In production, validate the Steam ID first and store in database
        if (!string.IsNullOrWhiteSpace(steamId))
        {
            _userSteamIds[userId] = steamId;
            _logger.LogInformation("Connected Steam account {SteamId} for user {UserId}", steamId, userId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string userId)
    {
        _userSteamIds.Remove(userId);
        _logger.LogInformation("Disconnected Steam account for user {UserId}", userId);
        return Task.CompletedTask;
    }
}
