﻿﻿namespace Cartridge.Mobile;

public class App : Application
{
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// Log app startup for debugging
		System.Diagnostics.Debug.WriteLine("=== App Starting ===");
		System.Diagnostics.Debug.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

		return window;
	}

	protected override void OnStart()
	{
		base.OnStart();
		System.Diagnostics.Debug.WriteLine("=== App OnStart called ===");
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		System.Diagnostics.Debug.WriteLine("=== App OnSleep called ===");

		// Flush cookies when app goes to sleep
#if ANDROID
		try
		{
			var cookieManager = Android.Webkit.CookieManager.Instance;
			if (cookieManager != null && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
			{
				cookieManager.Flush();
				System.Diagnostics.Debug.WriteLine("✓ Cookies flushed during OnSleep");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error flushing cookies in OnSleep: {ex.Message}");
		}
#endif
	}

	protected override void OnResume()
	{
		base.OnResume();
		System.Diagnostics.Debug.WriteLine("=== App OnResume called ===");

		// Log cookie status when app resumes
#if ANDROID
		try
		{
			var cookieManager = Android.Webkit.CookieManager.Instance;
			if (cookieManager != null)
			{
				var url = "https://cartridge.step0fail.com";
				var cookies = cookieManager.GetCookie(url);
				System.Diagnostics.Debug.WriteLine($"Cookies on resume: {(!string.IsNullOrEmpty(cookies) ? cookies.Split(';').Length.ToString() + " cookies" : "No cookies")}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error checking cookies in OnResume: {ex.Message}");
		}
#endif
	}
}