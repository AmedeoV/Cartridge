namespace Cartridge.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Steam Web API
/// </summary>
public class SteamApiSettings
{
    public const string SectionName = "SteamApi";

    /// <summary>
    /// Steam Web API key - Get yours at https://steamcommunity.com/dev/apikey
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Steam Web API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.steampowered.com";

    /// <summary>
    /// Base URL for Steam Store API (for game details)
    /// </summary>
    public string StoreBaseUrl { get; set; } = "https://store.steampowered.com/api";
}
