using Cartridge.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Cartridge.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> LoginWith2faAsync(LoginWith2faRequest request);
    Task LogoutAsync();
    Task<ApplicationUser?> GetCurrentUserAsync();
    Task<Enable2faResponse> EnableTwoFactorAsync();
    Task<AuthResponse> VerifyAndEnableTwoFactorAsync(Verify2faRequest request);
    Task<AuthResponse> DisableTwoFactorAsync();
    Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync();
    Task<IEnumerable<string>> GenerateRecoveryCodesAsync();
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Passwords do not match"
            };
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Email already registered"
            };
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName ?? request.Email.Split('@')[0],
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        // Don't auto sign-in - let the user sign in on the next page load
        // This avoids "Headers are read-only" errors in Blazor Server
        return new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            return new AuthResponse
            {
                Success = false,
                RequiresTwoFactor = true,
                Message = "Two-factor authentication required"
            };
        }

        if (!result.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        };
    }

    public async Task<AuthResponse> LoginWith2faAsync(LoginWith2faRequest request)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Unable to load two-factor authentication user"
            };
        }

        var authenticatorCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            authenticatorCode,
            request.RememberMe,
            request.RememberMachine);

        if (!result.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Invalid authenticator code"
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        };
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        return await _userManager.GetUserAsync(_signInManager.Context.User);
    }

    public async Task<Enable2faResponse> EnableTwoFactorAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new Enable2faResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        // Generate the authenticator key
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(sharedKey))
        {
            return new Enable2faResponse
            {
                Success = false,
                Message = "Failed to generate authenticator key"
            };
        }

        // Generate the QR code URI
        var email = await _userManager.GetEmailAsync(user);
        var authenticatorUri = GenerateQrCodeUri(email!, sharedKey);

        return new Enable2faResponse
        {
            Success = true,
            SharedKey = FormatKey(sharedKey),
            AuthenticatorUri = authenticatorUri
        };
    }

    public async Task<AuthResponse> VerifyAndEnableTwoFactorAsync(Verify2faRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!is2faTokenValid)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Verification code is invalid"
            };
        }

        // Enable 2FA
        var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!result.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Failed to enable two-factor authentication: " + string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        // Verify it was actually enabled
        var verifyEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        Console.WriteLine($"2FA enabled status after SetTwoFactorEnabledAsync: {verifyEnabled}");

        return new AuthResponse
        {
            Success = true,
            Message = "Two-factor authentication has been enabled"
        };
    }

    public async Task<AuthResponse> DisableTwoFactorAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disable2faResult.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Failed to disable two-factor authentication"
            };
        }

        return new AuthResponse
        {
            Success = true,
            Message = "Two-factor authentication has been disabled"
        };
    }

    public async Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new TwoFactorStatusResponse
            {
                IsEnabled = false,
                IsMachineRemembered = false,
                RecoveryCodesLeft = 0
            };
        }

        var hasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user);
        var is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var isMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

        return new TwoFactorStatusResponse
        {
            IsEnabled = is2faEnabled && !string.IsNullOrEmpty(hasAuthenticator),
            IsMachineRemembered = isMachineRemembered,
            RecoveryCodesLeft = recoveryCodesLeft
        };
    }

    public async Task<IEnumerable<string>> GenerateRecoveryCodesAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Enumerable.Empty<string>();
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes ?? Enumerable.Empty<string>();
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new System.Text.StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        return string.Format(
            AuthenticatorUriFormat,
            System.Web.HttpUtility.UrlEncode("Cartridge"),
            System.Web.HttpUtility.UrlEncode(email),
            unformattedKey);
    }
}

