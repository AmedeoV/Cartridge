using Android.Webkit;
using Java.Net;

namespace Cartridge.Mobile.Platforms.Android;

public class CartridgeWebViewClient : WebViewClient
{
	public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		// Allow all cartridge.step0fail.com URLs to load normally
		if (request?.Url?.Host?.Contains("cartridge.step0fail.com") == true)
		{
			return false; // Let WebView handle it
		}

		// For external URLs, you could open in external browser
		// For now, let WebView handle all URLs
		return false;
	}

	public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
	{
		base.OnPageFinished(view, url);

		System.Diagnostics.Debug.WriteLine($">>> WebViewClient.OnPageFinished: {url}");
		
		// Log and flush cookies after each page load
		CookieDebugHelper.LogAllCookies($"OnPageFinished: {url}");
		
		// Backup cookies after page load (especially after login)
		var context = view?.Context;
		if (context != null)
		{
			PersistentCookieStore.BackupCookies(context);
		}
		
		CookieDebugHelper.ForceCookieFlush("OnPageFinished");
	}

	public override WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		// Log cookie headers for debugging
		if (request?.Url?.Host?.Contains("cartridge.step0fail.com") == true)
		{
			var cookieManager = global::Android.Webkit.CookieManager.Instance;
			var cookies = cookieManager?.GetCookie(request.Url.ToString());
			if (!string.IsNullOrEmpty(cookies))
			{
				System.Diagnostics.Debug.WriteLine($"✓ WebViewClient: Request to {request.Url.Path} has cookies");
			}
		}

		return base.ShouldInterceptRequest(view, request);
	}
}

