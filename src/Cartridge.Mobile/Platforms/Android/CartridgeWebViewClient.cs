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

		// Ensure cookies are flushed after each page load
		try
		{
			var cookieManager = global::Android.Webkit.CookieManager.Instance;
			if (cookieManager != null && global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
			{
				cookieManager.Flush();
				System.Diagnostics.Debug.WriteLine($"✓ WebViewClient: Cookies flushed after loading {url}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! WebViewClient: Error flushing cookies: {ex.Message}");
		}
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

