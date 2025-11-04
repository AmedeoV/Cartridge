namespace Cartridge.Mobile;

public partial class MainPage : ContentPage
{
	private System.Threading.Timer? _cookieFlushTimer;

	public MainPage()
	{
		InitializeComponent();

		// Handle WebView navigation events
		CartridgeWebView.Navigating += OnWebViewNavigating;
		CartridgeWebView.Navigated += OnWebViewNavigated;

		// Start periodic cookie flushing every 30 seconds
		StartCookieFlushTimer();
	}

	private void StartCookieFlushTimer()
	{
		_cookieFlushTimer = new System.Threading.Timer(
			callback: _ => FlushCookies(),
			state: null,
			dueTime: TimeSpan.FromSeconds(30),
			period: TimeSpan.FromSeconds(30)
		);
	}

	private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
	{
		// Show loading indicator when navigation starts
		LoadingIndicator.IsVisible = true;
		LoadingIndicator.IsRunning = true;

		System.Diagnostics.Debug.WriteLine($">>> WebView navigating to: {e.Url}");
	}

	private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
	{
		// Hide loading indicator when navigation completes
		LoadingIndicator.IsVisible = false;
		LoadingIndicator.IsRunning = false;

		// Handle navigation result
		if (e.Result != WebNavigationResult.Success)
		{
			await DisplayAlert("Error", $"Failed to load page: {e.Result}", "OK");
			System.Diagnostics.Debug.WriteLine($"!!! Navigation failed: {e.Result}");
			return;
		}

		System.Diagnostics.Debug.WriteLine($"<<< WebView navigated successfully to: {e.Url}");

		// Flush cookies after navigation to save authentication state
		FlushCookies();

		// Check authentication status for debugging
		await CheckAuthenticationStatusAsync();
	}

	private async Task CheckAuthenticationStatusAsync()
	{
		try
		{
			// Inject script to check if user is authenticated and what cookies exist
			var checkAuthScript = @"
				(function() {
					try {
						var info = {
							authenticated: false,
							hasCookies: document.cookie.length > 0,
							cookieCount: document.cookie ? document.cookie.split(';').length : 0,
							cookiePreview: document.cookie ? document.cookie.substring(0, 50) : 'none'
						};

						// Check if there's an authentication indicator on the page
						var userMenuExists = document.querySelector('[data-user-menu]') !== null ||
						                     document.querySelector('.user-profile') !== null ||
						                     document.querySelector('[href*=""signout""]') !== null ||
						                     document.querySelector('[href*=""logout""]') !== null;

						info.authenticated = userMenuExists;

						return JSON.stringify(info);
					} catch (e) {
						return JSON.stringify({error: e.message});
					}
				})();
			";

			var result = await CartridgeWebView.EvaluateJavaScriptAsync(checkAuthScript);
			System.Diagnostics.Debug.WriteLine($"=== Auth Status Check ===");
			System.Diagnostics.Debug.WriteLine($"Result: {result}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error checking authentication status: {ex.Message}");
		}
	}

	private void FlushCookies()
	{
#if ANDROID
		try
		{
			var cookieManager = Android.Webkit.CookieManager.Instance;
			if (cookieManager != null)
			{
				// Get current cookies for the site
				var url = "https://cartridge.step0fail.com";
				var cookies = cookieManager.GetCookie(url);

				System.Diagnostics.Debug.WriteLine($"=== [{DateTime.Now:HH:mm:ss}] Cookie Flush ===");
				System.Diagnostics.Debug.WriteLine($"Has cookies: {!string.IsNullOrEmpty(cookies)}");
				if (!string.IsNullOrEmpty(cookies))
				{
					System.Diagnostics.Debug.WriteLine($"Cookie count: {cookies.Split(';').Length}");
					System.Diagnostics.Debug.WriteLine($"Cookies: {cookies.Substring(0, Math.Min(100, cookies.Length))}...");
				}

				if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
				{
					cookieManager.Flush();
					System.Diagnostics.Debug.WriteLine($"✓ Cookies flushed to disk");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"! Old Android version, cannot flush");
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"! CookieManager is null");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!! Error flushing cookies: {ex.Message}");
		}
#endif
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		// Flush cookies when page disappears
		FlushCookies();
		System.Diagnostics.Debug.WriteLine("Page disappearing, cookies flushed");

		// Stop timer
		_cookieFlushTimer?.Dispose();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Restart timer when page appears
		StartCookieFlushTimer();
		System.Diagnostics.Debug.WriteLine("Page appearing, cookie flush timer started");
	}
}
