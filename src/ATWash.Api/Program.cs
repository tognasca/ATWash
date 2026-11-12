using Microsoft.AspNetCore.RateLimiting;
using ATWash.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AtWashDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("mercadopago",client=>client.Timeout=TimeSpan.FromSeconds(15));
builder.Services.AddHealthChecks();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("platform", rule => { rule.PermitLimit=20; rule.Window=TimeSpan.FromMinutes(1); rule.QueueLimit=0; });
    options.AddFixedWindowLimiter("login", rule =>
    {
        rule.PermitLimit = builder.Configuration.GetValue<int>("ATWASH_LOGIN_RATE_LIMIT",5);
        rule.Window = TimeSpan.FromMinutes(1);
        rule.QueueLimit = 0;
    });
});
var jwtSecret = builder.Configuration["JWT_SECRET_KEY"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("JWT_SECRET_KEY must contain at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["JWT_ISSUER"] ?? "ATWash",
        ValidateAudience = true, ValidAudience = builder.Configuration["JWT_AUDIENCE"] ?? "ATWashClients",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents {
        OnTokenValidated = async context => {
            var principal=context.Principal;
            if(!Guid.TryParse(principal?.FindFirstValue("device_id"),out var deviceId) ||
               !Guid.TryParse(principal?.FindFirstValue("tenant_id"),out var tenantId) ||
               !Guid.TryParse(principal?.FindFirstValue("sub"),out var userId)){
                context.Fail("Dispositivo não identificado.");return;
            }
            var db=context.HttpContext.RequestServices.GetRequiredService<AtWashDbContext>();
            var valid=await db.RegisteredDevices.AsNoTracking().AnyAsync(x=>x.Id==deviceId&&x.TenantId==tenantId&&x.UserId==userId&&x.RevokedAt==null);
            var currentUser=await db.Users.AsNoTracking().Where(x=>x.Id==userId&&x.TenantId==tenantId&&x.IsActive)
                .Select(x=>new{x.Role}).SingleOrDefaultAsync();
            var tokenRole=principal?.FindFirstValue(ClaimTypes.Role);
            // Reject existing JWTs immediately after a role change. The user must log in
            // again to receive a token with their current permissions.
            var userValid=currentUser!=null&&string.Equals(currentUser.Role.ToString(),tokenRole,StringComparison.Ordinal);
            var tenantValid=await db.Tenants.AsNoTracking().AnyAsync(x=>x.Id==tenantId&&x.IsActive);
            var sub=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenantId);
            var subscriptionValid=sub==null || (sub.Status is "active" or "trial" &&
                (!sub.ExpiresAt.HasValue||sub.ExpiresAt.Value>DateTimeOffset.UtcNow));
            if(!valid||!userValid||!tenantValid||!subscriptionValid)
                context.Fail("Dispositivo, conta ou assinatura inativos.");
        }
    };
});
builder.Services.AddAuthorization();
var allowedOrigins = (builder.Configuration["ATWASH_CORS_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("AtWashClients", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
var app = builder.Build();
if (app.Configuration.GetValue<bool>("ATWASH_APPLY_MIGRATIONS") || app.Configuration.GetValue<bool>("ATWASH_SEED_DEMO"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AtWashDbContext>();
    if (app.Configuration.GetValue<bool>("ATWASH_APPLY_MIGRATIONS")) await db.Database.MigrateAsync();
    if (app.Configuration.GetValue<bool>("ATWASH_SEED_DEMO"))
    {
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Demo seed requires all EF Core migrations to be applied first.");
        await DemoSeeder.SeedAsync(db);
    }
}
app.UseCors("AtWashClients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuth();
app.MapOperations();
app.MapInventory();
app.MapOperationsManagement();
app.MapSaas();
app.MapSaasAdministration();
app.MapMercadoPagoBilling();
app.MapPayments();
app.MapFinance();
await AdminBootstrap.RunAsync(app.Services, app.Configuration);
// Initialize the default catalog for real companies that have no services yet.
await using (var catalogScope = app.Services.CreateAsyncScope())
{
    var catalogDb = catalogScope.ServiceProvider.GetRequiredService<AtWashDbContext>();
    await DefaultServiceCatalog.SeedMissingCatalogsAsync(catalogDb);
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapHealthChecks("/health");
// Public API is intentionally read-only and limited to the demo tenant until authentication is implemented.
if (app.Environment.IsDevelopment()) app.MapGet("/api/demo", async (AtWashDbContext db, CancellationToken ct) =>
{
    var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.IsDemo, ct);
    if (tenant is null) return Results.NotFound();
    var services = await db.Services.AsNoTracking().Where(x => x.TenantId == tenant.Id).Select(x => new { x.Name, x.Price }).ToListAsync(ct);
    var orders = await db.Orders.AsNoTracking().Where(x => x.TenantId == tenant.Id).Select(x => new { x.Number, x.Status, x.PaymentStatus, x.Total }).ToListAsync(ct);
    return Results.Ok(new { tenant.Name, Services = services, Orders = orders });
});
app.Run();
public partial class Program { }
