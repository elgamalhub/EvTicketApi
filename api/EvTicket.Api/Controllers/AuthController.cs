using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, CurrentUser currentUser) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        auth.LoginAsync(request, ct);

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Google(GoogleLoginRequest request, CancellationToken ct) =>
        auth.GoogleLoginAsync(request, ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) =>
        auth.RefreshAsync(request, ct);

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Accepted(new { message = "If that account exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(currentUser.RequireId(), request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetMeAsync(currentUser.RequireId(), ct);

    [HttpPut("me")]
    [Authorize]
    public Task<UserDto> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        auth.UpdateProfileAsync(currentUser.RequireId(), request, ct);
}
