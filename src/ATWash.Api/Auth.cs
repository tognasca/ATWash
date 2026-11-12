using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ATWash.Api;

public enum UserRole { Owner, Manager, Attendant, Washer }
public sealed class AppUser : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record LoginRequest(Guid TenantId, string Email, string Password);
public sealed class TokenIssuer(IConfiguration configuration)
{
    public string Issue(AppUser user)
    {
        var secret = configuration["JWT_SECRET_KEY"] ?? throw new InvalidOperationException("JWT_SECRET_KEY missing");
        if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT_SECRET_KEY must have at least 32 bytes");
        var issuer = configuration["JWT_ISSUER"] ?? "ATWash";
        var audience = configuration["JWT_AUDIENCE"] ?? "ATWashClients";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddMinutes(20), signingCredentials: credentials));
    }
}
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, AtWashDbContext db,
            IPasswordHasher<AppUser> hasher, TokenIssuer issuer, CancellationToken ct) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
                return Results.Unauthorized();
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .SingleOrDefaultAsync(db.Users.Where(x => x.TenantId == request.TenantId && x.Email == email && x.IsActive), ct);
            // Verify a dummy hash on missing accounts to reduce timing differences.
            var candidate = user ?? new AppUser { TenantId = request.TenantId, Email = email,
                PasswordHash = DummyHash };
            var valid = hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, request.Password);
            if (user is null || valid == PasswordVerificationResult.Failed) return Results.Unauthorized();
            var tenant = await db.Tenants.FindAsync([user.TenantId], ct);
            if (tenant is null || !tenant.IsActive) return Results.Unauthorized();
            return Results.Ok(new { accessToken = issuer.Issue(user), tokenType = "Bearer", expiresIn = 1200 });
        }).RequireRateLimiting("login");

        app.MapGet("/api/auth/me", (ClaimsPrincipal principal) => Results.Ok(new
        {
            userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier),
            tenantId = principal.FindFirstValue("tenant_id"),
            role = principal.FindFirstValue(ClaimTypes.Role)
        })).RequireAuthorization();
    }

    // Generated using ASP.NET Identity PBKDF2 format; replace with a freshly generated dummy on deployment.
    private static readonly string DummyHash = new PasswordHasher<AppUser>()
        .HashPassword(new AppUser { Email = "dummy@invalid", PasswordHash = "" }, Guid.NewGuid().ToString());
}
