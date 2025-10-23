using System.Text.Json;
using Microsoft.Extensions.Logging;
using Cartridge.Core.Models;

namespace Cartridge.Infrastructure.AmazonGames;

/// <summary>
/// Service for manually importing Amazon Games data
/// </summary>
public class AmazonGamesManualImportService
{
    private readonly ILogger<AmazonGamesManualImportService> _logger;

    public AmazonGamesManualImportService(ILogger<AmazonGamesManualImportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse games from JSON import
    /// Expected format: Array of game objects with title, id, etc.
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
                var importedGames = JsonSerializer.Deserialize<List<AmazonImportGame>>(jsonContent, options);
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
                var importedGame = JsonSerializer.Deserialize<AmazonImportGame>(jsonContent, options);
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
            _logger.LogError(ex, "Error parsing Amazon Games import JSON");
            throw new InvalidOperationException("Invalid JSON format. Please ensure the data is properly formatted.", ex);
        }

        return games;
    }

    /// <summary>
    /// Parse games from CSV import
    /// Expected format: title,productId
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
                        Id = $"amazon-{parts[1].Trim().Trim('"')}",
                        Platform = Platform.AmazonGames,
                        AddedToLibrary = DateTime.UtcNow
                    };

                    games.Add(game);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CSV line {LineNumber}", i + 1);
                }
            }

            _logger.LogInformation("Successfully imported {Count} games from CSV", games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Amazon Games import CSV");
            throw new InvalidOperationException("Invalid CSV format. Please ensure the data is properly formatted.", ex);
        }

        return games;
    }

    private Game ConvertToGame(AmazonImportGame importGame)
    {
        return new Game
        {
            Id = $"amazon-{importGame.ProductId ?? Guid.NewGuid().ToString()}",
            Title = importGame.Title ?? "Unknown Game",
            Platform = Platform.AmazonGames,
            AddedToLibrary = DateTime.UtcNow,
            Description = importGame.Description,
            Developer = importGame.Developer,
            Publisher = importGame.Publisher,
            IsManuallyAdded = true
        };
    }

    // DTO for JSON import
    private class AmazonImportGame
    {
        public string? Title { get; set; }
        public string? ProductId { get; set; }
        public string? Description { get; set; }
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
    }
}
