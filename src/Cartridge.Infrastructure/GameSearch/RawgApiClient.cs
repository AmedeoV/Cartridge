using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cartridge.Infrastructure.GameSearch;

public class RawgApiClient
{
    private readonly HttpClient _httpClient;
    private readonly RawgApiSettings _settings;
    private readonly ILogger<RawgApiClient> _logger;

    public RawgApiClient(HttpClient httpClient, IOptions<RawgApiSettings> settings, ILogger<RawgApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<Game>> SearchGamesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Game>();

        try
        {
            var response = await _httpClient.GetFromJsonAsync<RawgSearchResponse>(
                $"{_settings.BaseUrl}/games?key={_settings.ApiKey}&search={Uri.EscapeDataString(query)}&page_size=20");

            if (response?.Results == null || !response.Results.Any())
                return new List<Game>();

            var games = new List<Game>();
            
            // Fetch details for each game to get descriptions (limited to first 10 for performance)
            var gamesWithDetails = response.Results.Take(10);
            
            foreach (var r in gamesWithDetails)
            {
                var gameDetails = await GetGameDetailsAsync(r.Id);
                
                games.Add(new Game
                {
                    Id = r.Id.ToString(),
                    Title = r.Name ?? "Unknown",
                    Description = gameDetails?.DescriptionRaw ?? gameDetails?.Description,
                    CoverImageUrl = r.BackgroundImage ?? gameDetails?.BackgroundImage,
                    ReleaseDate = r.Released ?? gameDetails?.Released,
                    Platform = Platform.Other, // Will be set by user
                    Developer = gameDetails?.Developers?.FirstOrDefault()?.Name ?? r.Developers?.FirstOrDefault()?.Name,
                    Publisher = gameDetails?.Publishers?.FirstOrDefault()?.Name ?? r.Publishers?.FirstOrDefault()?.Name,
                    Genres = (gameDetails?.Genres ?? r.Genres)?.Select(g => g.Name).ToList() ?? new List<string>(),
                    AddedToLibrary = DateTime.UtcNow
                });
            }
            
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching games for query: {Query}", query);
            return new List<Game>();
        }
    }

    /// <summary>
    /// Fetches detailed game information from RAWG API including description
    /// </summary>
    private async Task<RawgGameDetails?> GetGameDetailsAsync(int gameId)
    {
        try
        {
            _logger.LogDebug("Fetching RAWG game details for ID: {GameId}", gameId);
            var details = await _httpClient.GetFromJsonAsync<RawgGameDetails>(
                $"{_settings.BaseUrl}/games/{gameId}?key={_settings.ApiKey}");
            
            if (details != null)
            {
                _logger.LogDebug("Successfully fetched details for game ID {GameId}: {GameName}, Description length: {DescLength}", 
                    gameId, details.Name, details.DescriptionRaw?.Length ?? details.Description?.Length ?? 0);
            }
            
            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game details for ID {GameId}", gameId);
            return null;
        }
    }

    /// <summary>
    /// Enriches a game with metadata from RAWG API (description, developer, publisher, genres)
    /// </summary>
    public async Task<bool> EnrichGameMetadataAsync(Game game)
    {
        if (game == null || string.IsNullOrWhiteSpace(game.Title))
        {
            _logger.LogWarning("Cannot enrich game: game is null or has no title");
            return false;
        }

        try
        {
            _logger.LogInformation("🔍 Searching RAWG for: {GameTitle}", game.Title);
            
            // Check if API key is configured
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey == "YOUR_RAWG_API_KEY_HERE")
            {
                _logger.LogWarning("⚠️ RAWG API key not configured. Cannot enrich {GameTitle}", game.Title);
                return false;
            }
            
            // Search for the game by title
            var searchUrl = $"{_settings.BaseUrl}/games?key={_settings.ApiKey}&search={Uri.EscapeDataString(game.Title)}&page_size=5";
            _logger.LogDebug("RAWG API search URL: {Url}", searchUrl.Replace(_settings.ApiKey, "***KEY***"));
            
            var response = await _httpClient.GetFromJsonAsync<RawgSearchResponse>(searchUrl);

            if (response?.Results == null || !response.Results.Any())
            {
                _logger.LogWarning("❌ No RAWG results found for: {GameTitle}", game.Title);
                return false;
            }

            // Get the first result (best match)
            var rawgGame = response.Results.First();
            _logger.LogInformation("✓ Found RAWG match: '{RawgTitle}' (ID: {RawgId}) for '{GameTitle}'", 
                rawgGame.Name, rawgGame.Id, game.Title);
            
            // Fetch detailed information to get description
            var gameDetails = await GetGameDetailsAsync(rawgGame.Id);
            
            if (gameDetails == null)
            {
                _logger.LogWarning("❌ Failed to fetch details for RAWG game ID: {RawgId}", rawgGame.Id);
                return false;
            }

            // Update game with RAWG data (only if not already set)
            bool updated = false;
            
            if (string.IsNullOrEmpty(game.Description) && gameDetails != null)
            {
                game.Description = gameDetails.DescriptionRaw ?? gameDetails.Description;
                if (!string.IsNullOrEmpty(game.Description))
                {
                    _logger.LogInformation("✓ Set description for '{GameTitle}': {DescLength} characters", 
                        game.Title, game.Description.Length);
                    updated = true;
                }
                else
                {
                    _logger.LogWarning("⚠️ RAWG returned no description for '{GameTitle}'", game.Title);
                }
            }

            if (string.IsNullOrEmpty(game.Developer))
            {
                game.Developer = gameDetails?.Developers?.FirstOrDefault()?.Name ?? rawgGame.Developers?.FirstOrDefault()?.Name;
                if (!string.IsNullOrEmpty(game.Developer))
                {
                    _logger.LogDebug("✓ Set developer for '{GameTitle}': {Developer}", game.Title, game.Developer);
                    updated = true;
                }
            }

            if (string.IsNullOrEmpty(game.Publisher))
            {
                game.Publisher = gameDetails?.Publishers?.FirstOrDefault()?.Name ?? rawgGame.Publishers?.FirstOrDefault()?.Name;
                if (!string.IsNullOrEmpty(game.Publisher))
                {
                    _logger.LogDebug("✓ Set publisher for '{GameTitle}': {Publisher}", game.Title, game.Publisher);
                    updated = true;
                }
            }

            if (game.Genres == null || !game.Genres.Any())
            {
                game.Genres = (gameDetails?.Genres ?? rawgGame.Genres)?.Select(g => g.Name).ToList() ?? new List<string>();
                if (game.Genres.Any())
                {
                    _logger.LogDebug("✓ Set genres for '{GameTitle}': {Genres}", game.Title, string.Join(", ", game.Genres));
                    updated = true;
                }
            }

            if (!game.ReleaseDate.HasValue && rawgGame.Released.HasValue)
            {
                game.ReleaseDate = rawgGame.Released;
                _logger.LogDebug("✓ Set release date for '{GameTitle}': {ReleaseDate}", game.Title, game.ReleaseDate);
                updated = true;
            }

            if (updated)
            {
                _logger.LogInformation("✅ Successfully enriched '{GameTitle}' with RAWG data", game.Title);
            }
            else
            {
                _logger.LogWarning("⚠️ No new data to add for '{GameTitle}'", game.Title);
            }

            return updated;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ HTTP error enriching game metadata for '{GameTitle}'. Status: {StatusCode}", 
                game.Title, ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error enriching game metadata for '{GameTitle}'", game.Title);
            return false;
        }
    }
}

public class RawgSearchResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("results")]
    public List<RawgGame> Results { get; set; } = new();
}

public class RawgGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("description_raw")]
    public string? DescriptionRaw { get; set; }

    [JsonPropertyName("released")]
    public DateTime? Released { get; set; }

    [JsonPropertyName("background_image")]
    public string? BackgroundImage { get; set; }

    [JsonPropertyName("rating")]
    public decimal Rating { get; set; }

    [JsonPropertyName("ratings_count")]
    public int RatingsCount { get; set; }

    [JsonPropertyName("metacritic")]
    public int? Metacritic { get; set; }

    [JsonPropertyName("playtime")]
    public int Playtime { get; set; }

    [JsonPropertyName("genres")]
    public List<RawgGenre>? Genres { get; set; }

    [JsonPropertyName("developers")]
    public List<RawgDeveloper>? Developers { get; set; }

    [JsonPropertyName("publishers")]
    public List<RawgPublisher>? Publishers { get; set; }
}

public class RawgGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class RawgDeveloper
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class RawgPublisher
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Detailed game information from RAWG API /games/{id} endpoint
/// </summary>
public class RawgGameDetails
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("description_raw")]
    public string? DescriptionRaw { get; set; }

    [JsonPropertyName("released")]
    public DateTime? Released { get; set; }

    [JsonPropertyName("background_image")]
    public string? BackgroundImage { get; set; }

    [JsonPropertyName("genres")]
    public List<RawgGenre>? Genres { get; set; }

    [JsonPropertyName("developers")]
    public List<RawgDeveloper>? Developers { get; set; }

    [JsonPropertyName("publishers")]
    public List<RawgPublisher>? Publishers { get; set; }
}
