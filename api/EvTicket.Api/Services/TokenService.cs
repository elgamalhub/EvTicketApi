using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EvTicket.Api.Services;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "EvTicket.Api";
    public string Audience { get; set; } = "EvTicket.Web";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class TokenService(IConfiguration configuration, AppDbContext db)
{
    public JwtOptions Options { get; } = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

    public (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(Options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("organizer", user.IsOrganizer ? "true" : "false")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: Options.Issuer,
            audience: Options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public async Task<(string Raw, RefreshToken Entity)> IssueRefreshTokenAsync(AppUser user, string? ip, CancellationToken ct = default)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(Options.RefreshTokenDays),
            CreatedByIp = ip
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return (raw, entity);
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
