using Android.Webkit;
using System.Diagnostics;

namespace Cartridge.Mobile.Platforms.Android;

public static class CookieDebugHelper
{
	public static void LogAllCookies(string context)
	{
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager == null)
			{
				Debug.WriteLine($"[{context}] CookieManager is NULL");
				return;
			}

			var url = "https://cartridge.step0fail.com";
			var cookieString = cookieManager.GetCookie(url);

			Debug.WriteLine($"======= Cookie Debug [{context}] =======");
			Debug.WriteLine($"URL: {url}");
			Debug.WriteLine($"Accept Cookies: {cookieManager.AcceptCookie()}");
			Debug.WriteLine($"Has Cookies: {!string.IsNullOrEmpty(cookieString)}");

			if (!string.IsNullOrEmpty(cookieString))
			{
				var cookies = cookieString.Split(';');
				Debug.WriteLine($"Total Cookies: {cookies.Length}");
				Debug.WriteLine("Cookies:");

				foreach (var cookie in cookies)
				{
					var trimmedCookie = cookie.Trim();
					var parts = trimmedCookie.Split('=');
					var cookieName = parts.Length > 0 ? parts[0] : "unknown";
					var cookieValuePreview = parts.Length > 1 ? parts[1].Substring(0, Math.Min(20, parts[1].Length)) : "empty";

					Debug.WriteLine($"  [{cookieName}] = {cookieValuePreview}...");

					// Flag authentication-related cookies
					if (cookieName.Contains("Cartridge") || 
					    cookieName.Contains("AspNet") || 
					    cookieName.Contains("Identity") ||
					    cookieName.Contains("Auth") ||
					    cookieName.Contains(".AspNetCore"))
					{
						Debug.WriteLine($"    *** AUTH-RELATED COOKIE ***");
					}
				}
			}
			else
			{
				Debug.WriteLine("NO COOKIES FOUND - User is NOT authenticated");
			}

			Debug.WriteLine($"======= End Cookie Debug =======");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{context}] Error logging cookies: {ex.Message}");
		}
	}

	public static void ForceCookieFlush(string context)
	{
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager != null && global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
			{
				cookieManager.Flush();
				Debug.WriteLine($"[{context}] ✓✓✓ Cookies FLUSHED to persistent storage");
			}
			else
			{
				Debug.WriteLine($"[{context}] Cannot flush cookies (old Android or null manager)");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{context}] Error flushing cookies: {ex.Message}");
		}
	}
}
