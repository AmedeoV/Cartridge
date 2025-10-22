using System.Text.Json;
using Cartridge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cartridge.Infrastructure.Steam;

/// <summary>
/// Client for interacting with Steam Web API
/// </summary>
public class SteamApiClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _storeClient;
    private readonly SteamApiSettings _settings;
    private readonly ILogger<SteamApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SteamApiClient(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IOptions<SteamApiSettings> settings,
        ILogger<SteamApiClient> logger)
    {
        _httpClient = httpClient;
        _storeClient = httpClientFactory.CreateClient("SteamStore");
        _settings = settings.Value;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    /// <summary>
    /// Get owned games for a Steam user
    /// </summary>
    /// <param name="steamId">64-bit Steam ID</param>
    /// <param name="includeAppInfo">Include game name and logo</param>
    /// <param name="includePlayedFreeGames">Include free games the user has played</param>
    public async Task<SteamGamesResponse?> GetOwnedGamesAsync(
        string steamId, 
        bool includeAppInfo = true, 
        bool includePlayedFreeGames = true)
    {
        try
        {
            // Check if API key is configured
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey == "YOUR_STEAM_API_KEY_HERE")
            {
                _logger.LogError("❌ Steam API key is not configured! Set your API key in appsettings.json or user secrets.");
                _logger.LogError("Get your API key at: https://steamcommunity.com/dev/apikey");
                return null;
            }

            var url = $"/IPlayerService/GetOwnedGames/v0001/" +
                     $"?key={_settings.ApiKey}" +
                     $"&steamid={steamId}" +
                     $"&include_appinfo={(includeAppInfo ? 1 : 0)}" +
                     $"&include_played_free_games={(includePlayedFreeGames ? 1 : 0)}" +
                     "&format=json";

            _logger.LogDebug("Calling Steam API: {Url}", url.Replace(_settings.ApiKey, "***API_KEY***"));

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Steam API request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SteamGamesResponse>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching owned games for Steam ID {SteamId}", steamId);
            return null;
        }
    }


    /// <summary>
    /// Get the header image URL for a Steam game
    /// </summary>
    public string GetGameHeaderImageUrl(int appId)
    {
        return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
    }

    /// <summary>
    /// Get the library image URL for a Steam game
    /// </summary>
    public string GetGameLibraryImageUrl(int appId)
    {
        return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
    }

    /// <summary>
    /// Get the icon URL for a Steam game
    /// </summary>
    public string GetGameIconUrl(int appId, string iconHash)
    {
        return $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg";
    }
}
