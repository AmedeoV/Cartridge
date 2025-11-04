using Android.Content;
using Android.Webkit;
using System.Diagnostics;
using System.Text.Json;

namespace Cartridge.Mobile.Platforms.Android;

/// <summary>
/// Backup cookie persistence mechanism using SharedPreferences
/// This provides an additional layer of cookie storage in case WebView's
/// built-in persistence fails.
/// </summary>
public static class PersistentCookieStore
{
	private const string PREFS_NAME = "CartridgeCookieStore";
	private const string COOKIE_KEY_PREFIX = "cookie_";

	public static void BackupCookies(Context context)
	{
		try
		{
			var cookieManager = CookieManager.Instance;
			if (cookieManager == null)
			{
				Debug.WriteLine("[PersistentCookieStore] CookieManager is null, cannot backup");
				return;
			}

			var url = "https://cartridge.step0fail.com";
			var cookieString = cookieManager.GetCookie(url);

			if (string.IsNullOrEmpty(cookieString))
			{
				Debug.WriteLine("[PersistentCookieStore] No cookies to backup");
				return;
			}

			var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
			var editor = prefs?.Edit();

			if (editor == null)
			{
				Debug.WriteLine("[PersistentCookieStore] Could not get preferences editor");
				return;
			}

			// Parse and store individual cookies
			var cookies = cookieString.Split(';');
			var savedCount = 0;

			foreach (var cookie in cookies)
			{
				var trimmedCookie = cookie.Trim();
				if (string.IsNullOrEmpty(trimmedCookie)) continue;

				var parts = trimmedCookie.Split(new[] { '=' }, 2);
				if (parts.Length != 2) continue;

				var cookieName = parts[0].Trim();
				var cookieValue = parts[1].Trim();

				// Only backup auth-related cookies
				if (cookieName.Contains("Cartridge") || 
				    cookieName.Contains("AspNet") || 
				    cookieName.Contains("Identity") ||
				    cookieName.Contains("Auth"))
				{
					editor.PutString($"{COOKIE_KEY_PREFIX}{cookieName}", trimmedCookie);
					savedCount++;
					Debug.WriteLine($"[PersistentCookieStore] Backed up cookie: {cookieName}");
				}
			}

			// Store timestamp
			editor.PutLong("backup_timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			editor.Apply();

			Debug.WriteLine($"[PersistentCookieStore] ✓✓✓ Backed up {savedCount} auth cookies to SharedPreferences");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PersistentCookieStore] Error backing up cookies: {ex.Message}");
		}
	}

	public static void RestoreCookies(Context context)
	{
		try
		{
			var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
			if (prefs == null)
			{
				Debug.WriteLine("[PersistentCookieStore] No preferences found");
				return;
			}

			var backupTimestamp = prefs.GetLong("backup_timestamp", 0);
			if (backupTimestamp == 0)
			{
				Debug.WriteLine("[PersistentCookieStore] No backup timestamp found");
				return;
			}

			// Check if backup is too old (more than 30 days)
			var backupAge = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - backupTimestamp;
			if (backupAge > 30 * 24 * 60 * 60) // 30 days
			{
				Debug.WriteLine("[PersistentCookieStore] Backup is too old, not restoring");
				ClearBackup(context);
				return;
			}

			var cookieManager = CookieManager.Instance;
			if (cookieManager == null)
			{
				Debug.WriteLine("[PersistentCookieStore] CookieManager is null, cannot restore");
				return;
			}

			var url = "https://cartridge.step0fail.com";
			var allPrefs = prefs.All;
			
			if (allPrefs == null)
			{
				Debug.WriteLine("[PersistentCookieStore] No preferences to restore");
				return;
			}
			
			var restoredCount = 0;

			foreach (var entry in allPrefs)
			{
				if (entry.Key?.StartsWith(COOKIE_KEY_PREFIX) == true)
				{
					var cookieValue = entry.Value?.ToString();
					if (!string.IsNullOrEmpty(cookieValue))
					{
						// Set cookie in WebView
						cookieManager.SetCookie(url, cookieValue);
						restoredCount++;
						
						var cookieName = entry.Key.Substring(COOKIE_KEY_PREFIX.Length);
						Debug.WriteLine($"[PersistentCookieStore] Restored cookie: {cookieName}");
					}
				}
			}

			if (restoredCount > 0)
			{
				// Flush to ensure cookies are saved
				if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
				{
					cookieManager.Flush();
				}

				Debug.WriteLine($"[PersistentCookieStore] ✓✓✓ Restored {restoredCount} cookies from backup");
			}
			else
			{
				Debug.WriteLine("[PersistentCookieStore] No cookies to restore from backup");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PersistentCookieStore] Error restoring cookies: {ex.Message}");
		}
	}

	public static void ClearBackup(Context context)
	{
		try
		{
			var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
			prefs?.Edit()?.Clear()?.Apply();
			Debug.WriteLine("[PersistentCookieStore] Backup cleared");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PersistentCookieStore] Error clearing backup: {ex.Message}");
		}
	}
}
