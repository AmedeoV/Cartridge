using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;

namespace Cartridge.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		try
		{
			// Configure WebView data directory to ensure persistence
			var dataDir = System.IO.Path.Combine(ApplicationContext.DataDir.AbsolutePath, "webview_data");
			if (!System.IO.Directory.Exists(dataDir))
			{
				System.IO.Directory.CreateDirectory(dataDir);
			}
			System.Diagnostics.Debug.WriteLine($"WebView data directory: {dataDir}");

			// Enable WebView cookie persistence
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				cookieManager.SetAcceptCookie(true);
				cookieManager.RemoveSessionCookies(null); // Don't remove session cookies

				System.Diagnostics.Debug.WriteLine("=== WebView cookies enabled and will persist ===");
				System.Diagnostics.Debug.WriteLine($"Accept cookies: {cookieManager.AcceptCookie()}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error enabling cookies: {ex.Message}");
		}
	}

	protected override void OnPause()
	{
		base.OnPause();

		// Flush cookies to disk when app is paused
		FlushCookies();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		// Flush cookies to disk when app is destroyed
		FlushCookies();
	}

	private void FlushCookies()
	{
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
				{
					cookieManager.Flush();
					System.Diagnostics.Debug.WriteLine("Cookies flushed to disk");
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error flushing cookies: {ex.Message}");
		}
	}
}
