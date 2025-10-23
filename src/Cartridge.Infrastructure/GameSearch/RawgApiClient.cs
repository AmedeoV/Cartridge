using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cartridge.Infrastructure.GameSearch;

public class RawgApiClient
{
    private readonly HttpClient _httpClient;
    private readonly RawgApiSettings _settings;

    public RawgApiClient(HttpClient httpClient, IOptions<RawgApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
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

            return response.Results.Select(r => new Game
            {
                Id = r.Id.ToString(),
                Title = r.Name ?? "Unknown",
                Description = r.Description ?? r.DescriptionRaw,
                CoverImageUrl = r.BackgroundImage,
                ReleaseDate = r.Released,
                Platform = Platform.Other, // Will be set by user
                Developer = r.Developers?.FirstOrDefault()?.Name,
                Publisher = r.Publishers?.FirstOrDefault()?.Name,
                Genres = r.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                AddedToLibrary = DateTime.UtcNow
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching games: {ex.Message}");
            return new List<Game>();
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
