using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using Cartridge.Mobile.Platforms.Android;

namespace Cartridge.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		try
		{
			System.Diagnostics.Debug.WriteLine("=== MainActivity.OnCreate START ===");
			System.Diagnostics.Debug.WriteLine($"Build Configuration: {(System.Diagnostics.Debugger.IsAttached ? "DEBUG" : "RELEASE")}");
			System.Diagnostics.Debug.WriteLine($"Package Name: {ApplicationContext?.PackageName}");
			
			if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
			{
#pragma warning disable CA1416
				System.Diagnostics.Debug.WriteLine($"Data Directory: {ApplicationContext?.DataDir?.AbsolutePath}");
#pragma warning restore CA1416
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"Files Directory: {ApplicationContext?.FilesDir?.AbsolutePath}");
			}
			
			// CRITICAL: Set WebView data directory BEFORE any WebView is created
			// This ensures cookies are stored in the app's private directory and persist
			if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
			{
				var context = ApplicationContext;
				if (context != null)
				{
					var webViewDataDir = System.IO.Path.Combine(context.FilesDir?.AbsolutePath ?? "", "webview");
					
					if (!System.IO.Directory.Exists(webViewDataDir))
					{
						System.IO.Directory.CreateDirectory(webViewDataDir);
						System.Diagnostics.Debug.WriteLine($"✓ Created WebView data directory: {webViewDataDir}");
					}
					
					System.Diagnostics.Debug.WriteLine($"WebView data directory configured: {webViewDataDir}");
				}
			}

			// Enable WebView debugging for troubleshooting
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
			{
				Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
				System.Diagnostics.Debug.WriteLine("✓ WebView debugging enabled");
			}

			// Enable WebView cookie persistence - MUST BE DONE BEFORE WebView IS CREATED
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				cookieManager.SetAcceptCookie(true);
				
				// IMPORTANT: Don't remove existing cookies on startup
				// This ensures cookies persist across app restarts
				// cookieManager.RemoveSessionCookies(null);
				// cookieManager.RemoveAllCookies(null);

				System.Diagnostics.Debug.WriteLine("=== WebView cookies enabled and will persist ===");
				System.Diagnostics.Debug.WriteLine($"Accept cookies: {cookieManager.AcceptCookie()}");
				
				// Use debug helper to log all cookies
				CookieDebugHelper.LogAllCookies("MainActivity.OnCreate");
				
				// Try to restore cookies from backup if WebView cookies are empty
				if (string.IsNullOrEmpty(cookieManager.GetCookie("https://cartridge.step0fail.com")))
				{
					System.Diagnostics.Debug.WriteLine("No WebView cookies found, attempting to restore from backup...");
					PersistentCookieStore.RestoreCookies(ApplicationContext!);
					CookieDebugHelper.LogAllCookies("MainActivity.OnCreate - After Restore");
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("!!! CookieManager is NULL in OnCreate");
			}
			
			System.Diagnostics.Debug.WriteLine("=== MainActivity.OnCreate END ===");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error in OnCreate: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
		}
	}

	protected override void OnPause()
	{
		base.OnPause();

		// Backup cookies and flush to disk when app is paused
		CookieDebugHelper.LogAllCookies("MainActivity.OnPause - BEFORE flush");
		PersistentCookieStore.BackupCookies(ApplicationContext!);
		CookieDebugHelper.ForceCookieFlush("MainActivity.OnPause");
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		// Backup cookies and flush to disk when app is destroyed
		CookieDebugHelper.LogAllCookies("MainActivity.OnDestroy - BEFORE flush");
		PersistentCookieStore.BackupCookies(ApplicationContext!);
		CookieDebugHelper.ForceCookieFlush("MainActivity.OnDestroy");
	}

	private void FlushCookies()
	{
		CookieDebugHelper.ForceCookieFlush("MainActivity.FlushCookies");
	}
}
