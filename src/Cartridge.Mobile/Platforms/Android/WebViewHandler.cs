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

				// Allow mixed content (HTTP in HTTPS pages)
				if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
				{
					platformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
				}

				System.Diagnostics.Debug.WriteLine("✓ WebView configured: DOM Storage, Database, Cache enabled");
			}

			// Enable cookies
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				cookieManager.SetAcceptCookie(true);
				cookieManager.SetAcceptThirdPartyCookies(platformView, true);

				System.Diagnostics.Debug.WriteLine($"✓ WebView cookies enabled - Accept: {cookieManager.AcceptCookie()}");
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
}

