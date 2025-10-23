namespace Cartridge.Infrastructure.Configuration;

public class RawgApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.rawg.io/api";
}
