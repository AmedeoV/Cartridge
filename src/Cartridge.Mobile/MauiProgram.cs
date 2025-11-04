using Microsoft.Extensions.Logging;
using Cartridge.Mobile.Services;

namespace Cartridge.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		// Configure custom handlers for cookie persistence
		builder.ConfigureMauiHandlers(handlers =>
		{
#if ANDROID
			handlers.AddHandler<WebView, Platforms.Android.CustomWebViewHandler>();
#endif
		});

		// Register services
		builder.Services.AddSingleton<AuthenticationService>();

		// Register main page
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
