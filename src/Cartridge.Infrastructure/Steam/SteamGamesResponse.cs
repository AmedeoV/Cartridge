using System.Text.Json.Serialization;

namespace Cartridge.Infrastructure.Steam;

/// <summary>
/// Response from Steam GetOwnedGames API
/// </summary>
public class SteamGamesResponse
{
    [JsonPropertyName("response")]
    public SteamGamesData? Response { get; set; }
}

public class SteamGamesData
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamGame> Games { get; set; } = new();
}

public class SteamGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string? ImgIconUrl { get; set; }

    [JsonPropertyName("img_logo_url")]
    public string? ImgLogoUrl { get; set; }

    [JsonPropertyName("has_community_visible_stats")]
    public bool? HasCommunityVisibleStats { get; set; }

    [JsonPropertyName("playtime_windows_forever")]
    public int PlaytimeWindowsForever { get; set; }

    [JsonPropertyName("playtime_mac_forever")]
    public int PlaytimeMacForever { get; set; }

    [JsonPropertyName("playtime_linux_forever")]
    public int PlaytimeLinuxForever { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long RtimeLastPlayed { get; set; }
}

