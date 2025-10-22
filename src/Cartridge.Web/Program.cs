using Cartridge.Web.Components;
using Cartridge.Core.Interfaces;
using Cartridge.Infrastructure.Services;
using Cartridge.Infrastructure.Connectors;
using Cartridge.Infrastructure.Configuration;
using Cartridge.Infrastructure.Steam;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Steam API settings
builder.Services.Configure<SteamApiSettings>(
    builder.Configuration.GetSection(SteamApiSettings.SectionName));

// Register HTTP clients
builder.Services.AddHttpClient<SteamApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.steampowered.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("SteamStore", client =>
{
    client.BaseAddress = new Uri("https://store.steampowered.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Steam API client
builder.Services.AddScoped<SteamApiClient>();

// Register GOG services
builder.Services.AddScoped<Cartridge.Infrastructure.Gog.GogGalaxyDatabaseReader>();

// Note: Epic Games and Ubisoft Connect use manual import via GOG import functionality
// Note: Amazon Games and Rockstar are not yet supported

// Register application services
builder.Services.AddScoped<IGameLibraryService, GameLibraryService>();
builder.Services.AddScoped<IPlatformConnectionService, PlatformConnectionService>();

// Register platform connectors
builder.Services.AddScoped<IPlatformConnector, SteamConnector>();
builder.Services.AddScoped<IPlatformConnector, EpicGamesConnector>();
builder.Services.AddScoped<IPlatformConnector, GogConnector>();
builder.Services.AddScoped<IPlatformConnector, AmazonGamesConnector>();
builder.Services.AddScoped<IPlatformConnector, UbisoftConnectConnector>();
builder.Services.AddScoped<IPlatformConnector, RockstarConnector>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
