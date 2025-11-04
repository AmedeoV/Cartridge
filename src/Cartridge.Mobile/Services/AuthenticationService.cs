namespace Cartridge.Mobile.Services;

public class AuthenticationService
{
	private const string AuthTokenKey = "auth_token";
	private const string RefreshTokenKey = "refresh_token";
	private const string TokenExpiryKey = "token_expiry";
	private const string UserIdKey = "user_id";

	public async Task<bool> IsAuthenticatedAsync()
	{
		try
		{
			var token = await SecureStorage.GetAsync(AuthTokenKey);
			if (string.IsNullOrEmpty(token))
			{
				return false;
			}

			var expiryString = await SecureStorage.GetAsync(TokenExpiryKey);
			if (string.IsNullOrEmpty(expiryString))
			{
				return true; // Token exists but no expiry set, assume valid
			}

			if (DateTime.TryParse(expiryString, out var expiry))
			{
				return expiry > DateTime.UtcNow;
			}

			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error checking authentication: {ex.Message}");
			return false;
		}
	}

	public async Task SaveAuthenticationAsync(string token, string? refreshToken = null, DateTime? expiry = null)
	{
		try
		{
			await SecureStorage.SetAsync(AuthTokenKey, token);

			if (!string.IsNullOrEmpty(refreshToken))
			{
				await SecureStorage.SetAsync(RefreshTokenKey, refreshToken);
			}

			if (expiry.HasValue)
			{
				await SecureStorage.SetAsync(TokenExpiryKey, expiry.Value.ToString("o"));
			}

			System.Diagnostics.Debug.WriteLine("Authentication saved successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error saving authentication: {ex.Message}");
			throw;
		}
	}

	public async Task<(string? token, string? refreshToken, DateTime? expiry)> GetAuthenticationAsync()
	{
		try
		{
			var token = await SecureStorage.GetAsync(AuthTokenKey);
			var refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
			var expiryString = await SecureStorage.GetAsync(TokenExpiryKey);

			DateTime? expiry = null;
			if (!string.IsNullOrEmpty(expiryString) && DateTime.TryParse(expiryString, out var expiryDate))
			{
				expiry = expiryDate;
			}

			return (token, refreshToken, expiry);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting authentication: {ex.Message}");
			return (null, null, null);
		}
	}

	public void ClearAuthentication()
	{
		try
		{
			SecureStorage.Remove(AuthTokenKey);
			SecureStorage.Remove(RefreshTokenKey);
			SecureStorage.Remove(TokenExpiryKey);
			SecureStorage.Remove(UserIdKey);

			System.Diagnostics.Debug.WriteLine("Authentication cleared");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error clearing authentication: {ex.Message}");
		}
	}

	public async Task SaveUserIdAsync(string userId)
	{
		try
		{
			await SecureStorage.SetAsync(UserIdKey, userId);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error saving user ID: {ex.Message}");
		}
	}

	public async Task<string?> GetUserIdAsync()
	{
		try
		{
			return await SecureStorage.GetAsync(UserIdKey);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting user ID: {ex.Message}");
			return null;
		}
	}
}

