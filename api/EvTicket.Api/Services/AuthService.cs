using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService,
    AppDbContext db,
    IEmailService email,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthService> logger)
{
    private string? Ip => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw ApiException.Conflict("An account with this email already exists.", "email_taken");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber,
            IsOrganizer = request.AsOrganizer,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw ApiException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, AppConstants.UserRole);
        if (request.AsOrganizer)
            await userManager.AddToRoleAsync(user, AppConstants.OrganizerRole);

        logger.LogInformation("New user registered {Email} (organizer: {Organizer})", email, request.AsOrganizer);
        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null)
            throw ApiException.Unauthorized("Invalid email or password.");

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            throw new ApiException(423, "Account temporarily locked after too many attempts. Try again in a few minutes.", "locked_out");
        if (!result.Succeeded)
            throw ApiException.Unauthorized("Invalid email or password.");

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = TokenService.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
            throw ApiException.Unauthorized("Invalid refresh token.");

        // Reuse of a revoked token is a strong signal the token was stolen.
        if (token.RevokedAt is not null)
        {
            await db.RefreshTokens
                .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
            logger.LogWarning("Refresh token reuse detected for user {UserId}", token.UserId);
            throw ApiException.Unauthorized("Refresh token has already been used.");
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
            throw ApiException.Unauthorized("Refresh token expired.");

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = Ip;

        var response = await BuildAuthResponseAsync(token.User, ct);
        token.ReplacedByTokenHash = TokenService.Hash(response.RefreshToken);
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;
        var hash = TokenService.Hash(request.RefreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null) return; // do not leak account existence

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = configuration["App:PublicUrl"] ?? "http://localhost:4200";
        var link = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
        await email.SendPasswordResetAsync(user.Email!, user.FullName, link, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null) throw ApiException.BadRequest("Invalid or expired reset link.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            throw ApiException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw ApiException.NotFound("User not found.");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            throw ApiException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw ApiException.NotFound("User not found.");
        return await ToDtoAsync(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw ApiException.NotFound("User not found.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw ApiException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

        return await ToDtoAsync(user);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw ApiException.BadRequest("Google sign-in is not configured on this server.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            throw ApiException.Unauthorized("Google token could not be verified.");
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = payload.Name ?? email.Split('@')[0],
                AvatarUrl = payload.Picture,
                EmailConfirmed = true
            };
            var create = await userManager.CreateAsync(user, Guid.NewGuid().ToString("A") + "!aA1");
            if (!create.Succeeded)
                throw ApiException.BadRequest(string.Join(" ", create.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, AppConstants.UserRole);
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user, roles);
        var (refresh, _) = await tokenService.IssueRefreshTokenAsync(user, Ip, ct);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refresh,
            ExpiresAt = expiresAt.AsUtc(),
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                Roles = roles.ToList(),
                IsOrganizer = user.IsOrganizer || roles.Contains(AppConstants.OrganizerRole),
                CreatedAt = user.CreatedAt.AsUtc()
            }
        };
    }

    private async Task<UserDto> ToDtoAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Roles = roles.ToList(),
            IsOrganizer = user.IsOrganizer || roles.Contains(AppConstants.OrganizerRole),
            CreatedAt = user.CreatedAt.AsUtc()
        };
    }
}
