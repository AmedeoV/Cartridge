using System.Text.Json;
using Microsoft.Extensions.Logging;
using Cartridge.Core.Models;

namespace Cartridge.Infrastructure.Gog;

/// <summary>
/// Service for manually importing GOG game data
/// </summary>
public class GogManualImportService
{
    private readonly ILogger<GogManualImportService> _logger;

    public GogManualImportService(ILogger<GogManualImportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse games from JSON import
    /// Expected format: Array of game objects with title, id, playtime, etc.
    /// </summary>
    public List<Game> ParseGamesFromJson(string jsonContent)
    {
        var games = new List<Game>();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Try to parse as array first
            try
            {
                var importedGames = JsonSerializer.Deserialize<List<GogImportGame>>(jsonContent, options);
                if (importedGames != null)
                {
                    games = importedGames.Select(ConvertToGame).ToList();
                    _logger.LogInformation("Successfully imported {Count} games from JSON array", games.Count);
                    return games;
                }
            }
            catch
            {
                // Try parsing as single object
                var importedGame = JsonSerializer.Deserialize<GogImportGame>(jsonContent, options);
                if (importedGame != null)
                {
                    games.Add(ConvertToGame(importedGame));
                    _logger.LogInformation("Successfully imported 1 game from JSON object");
                    return games;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing GOG import JSON");
            throw new InvalidOperationException("Invalid JSON format. Please ensure the data is properly formatted.", ex);
        }

        return games;
    }

    /// <summary>
    /// Parse games from CSV import
    /// Expected format: title,gogId,playtime,releaseDate
    /// </summary>
    public List<Game> ParseGamesFromCsv(string csvContent)
    {
        var games = new List<Game>();

        try
        {
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Skip header if present
            var startIndex = lines[0].ToLower().Contains("title") ? 1 : 0;

            for (int i = startIndex; i < lines.Length; i++)
            {
                try
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 2) continue;

                    var game = new Game
                    {
                        Title = parts[0].Trim().Trim('"'),
                        Id = $"gog_{parts[1].Trim().Trim('"')}",
                        Platform = Platform.GOG,
                        AddedToLibrary = DateTime.UtcNow
                    };

                    // Parse optional playtime (in minutes)
                    if (parts.Length > 2 && int.TryParse(parts[2].Trim(), out var playtime))
                    {
                        game.PlaytimeMinutes = playtime;
                    }

                    // Parse optional release date
                    if (parts.Length > 3 && DateTime.TryParse(parts[3].Trim().Trim('"'), out var releaseDate))
                    {
                        game.ReleaseDate = releaseDate;
                    }

                    games.Add(game);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CSV line {LineNumber}: {Line}", i + 1, lines[i]);
                }
            }

            _logger.LogInformation("Successfully imported {Count} games from CSV", games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing GOG import CSV");
            throw new InvalidOperationException("Invalid CSV format. Please ensure the data is properly formatted.", ex);
        }

        return games;
    }

    /// <summary>
    /// Generate a template CSV for users to fill out
    /// </summary>
    public string GenerateCsvTemplate()
    {
        return "title,gogId,playtime,releaseDate\n" +
               "\"The Witcher 3: Wild Hunt\",1207658930,2847,2015-05-19\n" +
               "\"Cyberpunk 2077\",1207658924,1920,2020-12-10\n" +
               "\"Example Game\",1234567890,0,2024-01-01";
    }

    /// <summary>
    /// Generate a template JSON for users to fill out
    /// </summary>
    public string GenerateJsonTemplate()
    {
        var template = new[]
        {
            new GogImportGame
            {
                Title = "The Witcher 3: Wild Hunt",
                GogId = "1207658930",
                Playtime = 2847,
                ReleaseDate = "2015-05-19",
                Developer = "CD PROJEKT RED",
                Publisher = "CD PROJEKT RED"
            },
            new GogImportGame
            {
                Title = "Cyberpunk 2077",
                GogId = "1207658924",
                Playtime = 1920,
                ReleaseDate = "2020-12-10",
                Developer = "CD PROJEKT RED",
                Publisher = "CD PROJEKT RED"
            }
        };

        return JsonSerializer.Serialize(template, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    private Game ConvertToGame(GogImportGame importGame)
    {
        var game = new Game
        {
            Id = $"gog_{importGame.GogId}",
            Title = importGame.Title ?? "Unknown Game",
            Description = importGame.Description,
            Platform = Platform.GOG,
            PlaytimeMinutes = importGame.Playtime,
            Developer = importGame.Developer,
            Publisher = importGame.Publisher,
            AddedToLibrary = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(importGame.ReleaseDate) && 
            DateTime.TryParse(importGame.ReleaseDate, out var releaseDate))
        {
            game.ReleaseDate = releaseDate;
        }

        if (!string.IsNullOrEmpty(importGame.CoverImageUrl))
        {
            game.CoverImageUrl = importGame.CoverImageUrl;
        }
        else if (!string.IsNullOrEmpty(importGame.GogId))
        {
            // Use GOG's CDN for cover images
            game.CoverImageUrl = $"https://images.gog-statics.com/{importGame.GogId}.jpg";
        }

        if (importGame.Genres != null && importGame.Genres.Any())
        {
            game.Genres = importGame.Genres;
        }

        return game;
    }

    /// <summary>
    /// DTO for importing GOG game data
    /// </summary>
    public class GogImportGame
    {
        public string? Title { get; set; }
        public string? GogId { get; set; }
        public int Playtime { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Description { get; set; }
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
        public string? CoverImageUrl { get; set; }
        public List<string>? Genres { get; set; }
    }
}
