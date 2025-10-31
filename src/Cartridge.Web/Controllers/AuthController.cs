using Cartridge.Core.Models;
using Cartridge.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cartridge.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }

            return Unauthorized(result);
        }
        
        [HttpPost("form-login")]
        public async Task<IActionResult> FormLogin([FromForm] string email, [FromForm] string password, [FromForm] bool rememberMe = false)
        {
            var request = new LoginRequest
            {
                Email = email,
                Password = password,
                RememberMe = rememberMe
            };

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                return Redirect("/library");
            }

            if (result.RequiresTwoFactor)
            {
                return Redirect("/signin-2fa");
            }

            return Redirect($"/signin?error={Uri.EscapeDataString(result.Message ?? "Invalid email or password")}");
        }

        [HttpPost("form-login-2fa")]
        public async Task<IActionResult> FormLogin2fa(
            [FromForm] string code, 
            [FromForm] bool rememberMe = false, 
            [FromForm] bool rememberMachine = false)
        {
            var request = new LoginWith2faRequest
            {
                Code = code,
                RememberMe = rememberMe,
                RememberMachine = rememberMachine
            };

            var result = await _authService.LoginWith2faAsync(request);

            if (result.Success)
            {
                return Redirect("/library");
            }

            return Redirect($"/signin-2fa?error={Uri.EscapeDataString(result.Message ?? "Invalid authenticator code")}");
        }
    }
}
