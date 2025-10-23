using Cartridge.Web.Components;
using Cartridge.Core.Interfaces;
using Cartridge.Core.Models;
using Cartridge.Infrastructure.Services;
using Cartridge.Infrastructure.Connectors;
using Cartridge.Infrastructure.Configuration;
using Cartridge.Infrastructure.Steam;
using Cartridge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add cascade authentication state
builder.Services.AddCascadingAuthenticationState();

// Add HttpContextAccessor for accessing HttpContext in components
builder.Services.AddHttpContextAccessor();

// Add antiforgery services for form protection
builder.Services.AddAntiforgery();

// Configure PostgreSQL Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    
    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;
});

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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["AppUrl"] ?? "https://localhost:7294") });

// Register Steam API client
builder.Services.AddScoped<SteamApiClient>();

// Register GOG services
builder.Services.AddScoped<Cartridge.Infrastructure.Gog.GogGalaxyDatabaseReader>();

// Register authentication service
builder.Services.AddScoped<IAuthService, AuthService>();

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

app.UseStaticFiles();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Map sign out endpoint
app.MapPost("/signout", async (HttpContext context, SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
