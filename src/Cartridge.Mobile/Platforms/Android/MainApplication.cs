﻿using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Webkit;

namespace Cartridge.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void OnCreate()
	{
		base.OnCreate();

		// Enable persistent cookies at application level
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null)
			{
				cookieManager.SetAcceptCookie(true);
				System.Diagnostics.Debug.WriteLine("=== MainApplication: Cookies enabled ===");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! MainApplication: Error enabling cookies: {ex.Message}");
		}
	}

	public override void OnLowMemory()
	{
		base.OnLowMemory();

		// Flush cookies when low memory
		FlushCookies();
	}

	public override void OnTrimMemory([GeneratedEnum] TrimMemory level)
	{
		base.OnTrimMemory(level);

		// Flush cookies when trimming memory
		if (level >= TrimMemory.RunningLow)
		{
			FlushCookies();
		}
	}

	private void FlushCookies()
	{
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
			{
				cookieManager.Flush();
				System.Diagnostics.Debug.WriteLine("✓ MainApplication: Cookies flushed");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! MainApplication: Error flushing cookies: {ex.Message}");
		}
	}
}
