using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Cartridge.Mobile.Platforms.Android;

public class CustomWebViewHandler : WebViewHandler
{
	protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
	{
		base.ConnectHandler(platformView);

		try
		{
			System.Diagnostics.Debug.WriteLine("=== CustomWebViewHandler.ConnectHandler called ===");

			// Set custom WebViewClient for better cookie management
			platformView.SetWebViewClient(new CartridgeWebViewClient());

			// Configure WebView settings for cookie persistence
			if (platformView.Settings != null)
			{
				platformView.Settings.DomStorageEnabled = true;
				platformView.Settings.DatabaseEnabled = true;
				platformView.Settings.JavaScriptEnabled = true;

				// Set cache mode to use cache when available
				platformView.Settings.CacheMode = CacheModes.Default;

				// Enable storage APIs
				platformView.Settings.SetGeolocationEnabled(false);

				// Set a proper User-Agent to ensure server treats this as a persistent browser
				var defaultUserAgent = platformView.Settings.UserAgentString ?? "";
				if (!defaultUserAgent.Contains("Cartridge"))
				{
					platformView.Settings.UserAgentString = $"{defaultUserAgent} CartridgeMobile/1.0";
				}

				// Enable app cache (deprecated but still works on older Android versions)
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CA1422 // Obsolete on Android 30+
				platformView.Settings.SetAppCacheEnabled(true);
#pragma warning restore CA1422
#pragma warning restore CS0618

				// Allow mixed content (HTTP in HTTPS pages)
				if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
				{
					platformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
				}

				System.Diagnostics.Debug.WriteLine("✓ WebView configured: DOM Storage, Database, Cache enabled");
			}

			// Enable cookies with persistence - CRITICAL for persistent login
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				// Enable cookie acceptance
				cookieManager.SetAcceptCookie(true);
				
				// Allow third-party cookies (may be needed for some auth flows)
				cookieManager.SetAcceptThirdPartyCookies(platformView, true);

				System.Diagnostics.Debug.WriteLine($"✓ WebView cookies enabled - Accept: {cookieManager.AcceptCookie()}");

				// Check for existing cookies on WebView creation
				var url = "https://cartridge.step0fail.com";
				var cookies = cookieManager.GetCookie(url);
				if (!string.IsNullOrEmpty(cookies))
				{
					var cookieCount = cookies.Split(';').Length;
					System.Diagnostics.Debug.WriteLine($"✓ Found existing cookies in WebView: {cookieCount} cookie(s)");
					
					// Check specifically for auth cookies
					if (cookies.Contains(".Cartridge.Auth") || cookies.Contains("AspNet"))
					{
						System.Diagnostics.Debug.WriteLine("✓ Authentication cookie present in WebView - user should be logged in");
					}
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("No existing cookies found in WebView (user needs to log in)");
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("! CookieManager is null in handler");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error configuring WebView: {ex.Message}\n{ex.StackTrace}");
		}
	}

	protected override void DisconnectHandler(global::Android.Webkit.WebView platformView)
	{
		try
		{
			// Flush cookies before disconnecting
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null && global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
			{
				cookieManager.Flush();
				System.Diagnostics.Debug.WriteLine("✓ Cookies flushed before WebView disconnect");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error flushing cookies in DisconnectHandler: {ex.Message}");
		}

		base.DisconnectHandler(platformView);
	}
}

