using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
public sealed record LoginRequest(string Email, string Password, Guid? DeviceId = null, string? DeviceName = null);
public sealed class TokenIssuer(IConfiguration configuration)
{
    public string Issue(AppUser user, Guid deviceId)
    {
        var secret = configuration["JWT_SECRET_KEY"] ?? throw new InvalidOperationException("JWT_SECRET_KEY missing");
        if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT_SECRET_KEY must have at least 32 bytes");
        var issuer = configuration["JWT_ISSUER"] ?? "ATWash";
        var audience = configuration["JWT_AUDIENCE"] ?? "ATWashClients";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("device_id",deviceId.ToString()),
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
            var matches = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Users.Where(x => x.Email == email && x.IsActive).Take(2), ct);
            // An email must resolve to exactly one account; never choose a tenant arbitrarily.
            var user = matches.Count == 1 ? matches[0] : null;
            // Verify a dummy hash on missing accounts to reduce timing differences.
            var candidate = user ?? new AppUser { TenantId = Guid.Empty, Email = email,
                PasswordHash = DummyHash };
            var valid = hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, request.Password);
            if (user is null || valid == PasswordVerificationResult.Failed) return Results.Unauthorized();
            var tenant = await db.Tenants.FindAsync([user.TenantId], ct);
            if (tenant is null || !tenant.IsActive) return Results.Unauthorized();
            var subscription=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==user.TenantId,ct);
            if(subscription!=null&&(subscription.Status is not ("active" or "trial")||
               (subscription.ExpiresAt.HasValue&&subscription.ExpiresAt.Value<=DateTimeOffset.UtcNow)))
                return Results.Unauthorized();
            if (request.DeviceId == Guid.Empty) return Results.BadRequest("Identificador de dispositivo inválido.");
            var deviceId=request.DeviceId ?? Guid.NewGuid();
            await using var deviceTx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({user.TenantId.ToString()}, 0))",ct);
            var device=await db.RegisteredDevices.SingleOrDefaultAsync(x=>x.Id==deviceId,ct);
            if(device!=null && (device.RevokedAt!=null || device.UserId!=user.Id || device.TenantId!=user.TenantId))
                return Results.Unauthorized();
            if(device==null){
                if(subscription!=null){
                    var plan=await db.SubscriptionPlans.AsNoTracking().SingleAsync(x=>x.Id==subscription.PlanId,ct);
                    var activeDevices=await db.RegisteredDevices.CountAsync(x=>x.TenantId==user.TenantId&&x.RevokedAt==null,ct);
                    if(activeDevices>=plan.MaxDevices)return Results.Conflict("Limite de dispositivos do plano atingido. Revogue um dispositivo antes de cadastrar outro.");
                }
                device=new RegisteredDevice{Id=deviceId,TenantId=user.TenantId,UserId=user.Id,
                    Name=string.IsNullOrWhiteSpace(request.DeviceName)?"Navegador":request.DeviceName.Trim()[..Math.Min(request.DeviceName.Trim().Length,100)]};
                db.RegisteredDevices.Add(device);
                await db.SaveChangesAsync(ct);
            }
            await deviceTx.CommitAsync(ct);
            return Results.Ok(new { accessToken = issuer.Issue(user,deviceId), deviceId, tokenType = "Bearer", expiresIn = 1200 });
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
