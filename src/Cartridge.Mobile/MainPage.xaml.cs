using System.Text.Json;

namespace Cartridge.Mobile;

public partial class MainPage : ContentPage
{
	private const string AuthTokenKey = "auth_token";
	private const string RefreshTokenKey = "refresh_token";
	private const string TokenExpiryKey = "token_expiry";

	public MainPage()
	{
		InitializeComponent();

		// Handle WebView navigation events
		CartridgeWebView.Navigating += OnWebViewNavigating;
		CartridgeWebView.Navigated += OnWebViewNavigated;
	}

	private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
	{
		// Show loading indicator when navigation starts
		LoadingIndicator.IsVisible = true;
		LoadingIndicator.IsRunning = true;
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
			return;
		}

		// Inject JavaScript to capture and restore authentication tokens
		await InjectAuthenticationScriptAsync();
	}

	private async Task InjectAuthenticationScriptAsync()
	{
		try
		{
			// First, try to restore saved tokens
			var savedToken = await SecureStorage.GetAsync(AuthTokenKey);
			if (!string.IsNullOrEmpty(savedToken))
			{
				var expiryString = await SecureStorage.GetAsync(TokenExpiryKey);
				if (!string.IsNullOrEmpty(expiryString))
				{
					if (DateTime.TryParse(expiryString, out var expiry) && expiry > DateTime.UtcNow)
					{
						// Token is still valid, restore it to localStorage
						var refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
						await RestoreTokensToWebViewAsync(savedToken, refreshToken);
					}
				}
			}

			// Inject script to monitor and save authentication changes
			var monitorScript = @"
				(function() {
					// Monitor localStorage changes for authentication tokens
					const originalSetItem = localStorage.setItem;
					localStorage.setItem = function(key, value) {
						originalSetItem.apply(this, arguments);
						if (key === 'token' || key === 'auth_token' || key === 'access_token') {
							window.chrome.webview.postMessage({
								type: 'auth_token',
								token: value,
								refresh_token: localStorage.getItem('refresh_token'),
								expiry: localStorage.getItem('token_expiry')
							});
						}
					};

					// Also monitor sessionStorage
					const originalSessionSetItem = sessionStorage.setItem;
					sessionStorage.setItem = function(key, value) {
						originalSessionSetItem.apply(this, arguments);
						if (key === 'token' || key === 'auth_token' || key === 'access_token') {
							window.chrome.webview.postMessage({
								type: 'auth_token',
								token: value,
								refresh_token: sessionStorage.getItem('refresh_token'),
								expiry: sessionStorage.getItem('token_expiry')
							});
						}
					};
				})();
			";

			await CartridgeWebView.EvaluateJavaScriptAsync(monitorScript);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error injecting authentication script: {ex.Message}");
		}
	}

	private async Task RestoreTokensToWebViewAsync(string token, string? refreshToken)
	{
		try
		{
			var restoreScript = $@"
				(function() {{
					localStorage.setItem('token', '{token}');
					localStorage.setItem('auth_token', '{token}');
					localStorage.setItem('access_token', '{token}');
					if ('{refreshToken}' !== '') {{
						localStorage.setItem('refresh_token', '{refreshToken}');
					}}
				}})();
			";

			await CartridgeWebView.EvaluateJavaScriptAsync(restoreScript);
			System.Diagnostics.Debug.WriteLine("Authentication tokens restored to WebView");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error restoring tokens: {ex.Message}");
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Check and restore authentication on page appearing
		await CheckAndRestoreAuthenticationAsync();
	}

	private async Task CheckAndRestoreAuthenticationAsync()
	{
		try
		{
			var savedToken = await SecureStorage.GetAsync(AuthTokenKey);
			if (!string.IsNullOrEmpty(savedToken))
			{
				var expiryString = await SecureStorage.GetAsync(TokenExpiryKey);
				if (!string.IsNullOrEmpty(expiryString))
				{
					if (DateTime.TryParse(expiryString, out var expiry) && expiry > DateTime.UtcNow)
					{
						System.Diagnostics.Debug.WriteLine("Valid authentication token found");
						// Token will be restored after navigation completes
					}
					else
					{
						// Token expired, clear it
						System.Diagnostics.Debug.WriteLine("Authentication token expired, clearing");
						ClearAuthentication();
					}
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error checking authentication: {ex.Message}");
		}
	}

	private void ClearAuthentication()
	{
		try
		{
			SecureStorage.Remove(AuthTokenKey);
			SecureStorage.Remove(RefreshTokenKey);
			SecureStorage.Remove(TokenExpiryKey);

			// Clear localStorage via JavaScript
			var clearScript = @"
				localStorage.removeItem('token');
				localStorage.removeItem('auth_token');
				localStorage.removeItem('access_token');
				localStorage.removeItem('refresh_token');
				localStorage.removeItem('token_expiry');
			";

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					await CartridgeWebView.EvaluateJavaScriptAsync(clearScript);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error clearing localStorage: {ex.Message}");
				}
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error clearing authentication: {ex.Message}");
		}
	}
}
