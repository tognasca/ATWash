using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public sealed record SaasPlanInput(string Name, decimal MonthlyPrice, int MaxDevices, bool IsActive = true);
public sealed record SaasTenantInput(string Name, string OwnerEmail, string OwnerPassword, Guid PlanId, DateTimeOffset? ExpiresAt);
public sealed record SaasSubscriptionInput(Guid PlanId, string Status, DateTimeOffset? ExpiresAt);

public static class SaasAdministrationEndpoints
{
    // Separate platform credential; never grant global access to a tenant Owner JWT.
    static bool Authorized(HttpContext http, IConfiguration config)
    {
        var expected = config["ATWASH_PLATFORM_ADMIN_KEY"];
        if (string.IsNullOrWhiteSpace(expected) || Encoding.UTF8.GetByteCount(expected) < 32) return false;
        var actual = http.Request.Headers["X-ATWash-Platform-Key"].ToString();
        if (string.IsNullOrWhiteSpace(actual)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a,b);
    }

    public static void MapSaasAdministration(this WebApplication app)
    {
        var admin = app.MapGroup("/api/platform").RequireRateLimiting("platform");
        admin.AddEndpointFilter(async (ctx,next) =>
        {
            var http = ctx.HttpContext;
            if (!Authorized(http,http.RequestServices.GetRequiredService<IConfiguration>()))
                return Results.Unauthorized();
            if (!http.Request.IsHttps && !http.Request.Host.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)
                && http.Request.Host.Host!="127.0.0.1")
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await next(ctx);
        });
        admin.MapGet("/tenants",async(AtWashDbContext db,CancellationToken ct) =>
        {
            var tenants=await db.Tenants.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
            var subscriptions=await db.TenantSubscriptions.AsNoTracking().ToListAsync(ct);
            return Results.Ok(tenants.Select(t=>new {t.Id,t.Name,t.IsActive,t.IsDemo,
                Subscription=subscriptions.SingleOrDefault(x=>x.TenantId==t.Id)}));
        });
        admin.MapGet("/plans",async(AtWashDbContext db,CancellationToken ct) =>
            Results.Ok(await db.SubscriptionPlans.AsNoTracking().OrderBy(x=>x.MonthlyPrice).ToListAsync(ct)));
        admin.MapPost("/plans",async(SaasPlanInput input,AtWashDbContext db,CancellationToken ct) =>
        {
            if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>120||input.MonthlyPrice<0||
               input.MaxDevices<1||input.MaxDevices>10000)return Results.BadRequest("Dados do plano inválidos.");
            var plan=new SubscriptionPlan{Name=input.Name.Trim(),MonthlyPrice=input.MonthlyPrice,
                MaxDevices=input.MaxDevices,IsActive=input.IsActive};
            db.SubscriptionPlans.Add(plan);await db.SaveChangesAsync(ct);
            return Results.Created($"/api/platform/plans/{plan.Id}",plan);
        });
        admin.MapPut("/plans/{id:guid}",async(Guid id,SaasPlanInput input,AtWashDbContext db,CancellationToken ct) =>
        {
            if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>120||input.MonthlyPrice<0||
               input.MaxDevices<1||input.MaxDevices>10000)return Results.BadRequest("Dados do plano inválidos.");
            var plan=await db.SubscriptionPlans.FindAsync([id],ct);if(plan==null)return Results.NotFound();
            plan.Name=input.Name.Trim();plan.MonthlyPrice=input.MonthlyPrice;plan.MaxDevices=input.MaxDevices;
            plan.IsActive=input.IsActive;await db.SaveChangesAsync(ct);return Results.Ok(plan);
        });
        admin.MapPost("/tenants",async(SaasTenantInput input,AtWashDbContext db,
            IPasswordHasher<AppUser> hasher,CancellationToken ct) =>
        {
            var email=input.OwnerEmail?.Trim().ToLowerInvariant();
            if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>160||
               string.IsNullOrWhiteSpace(email)||!email.Contains('@')||
               string.IsNullOrWhiteSpace(input.OwnerPassword)||input.OwnerPassword.Length<12||
               input.OwnerPassword.Length>128)return Results.BadRequest("Dados da empresa inválidos.");
            if(!await db.SubscriptionPlans.AnyAsync(x=>x.Id==input.PlanId&&x.IsActive,ct))
                return Results.BadRequest("Plano inexistente ou inativo.");
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({email}, 0))",ct);
            if(await db.Users.AnyAsync(x=>x.Email==email,ct))return Results.Conflict("E-mail já cadastrado.");
            var tenant=new Tenant{Name=input.Name.Trim(),IsActive=true,IsDemo=false};
            var owner=new AppUser{TenantId=tenant.Id,Email=email,PasswordHash="",Role=UserRole.Owner};
            owner.PasswordHash=hasher.HashPassword(owner,input.OwnerPassword);
            var sub=new TenantSubscription{TenantId=tenant.Id,PlanId=input.PlanId,
                Status="active",ExpiresAt=input.ExpiresAt};
            db.Tenants.Add(tenant);db.Users.Add(owner);db.TenantSubscriptions.Add(sub);
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return Results.Created($"/api/platform/tenants/{tenant.Id}",new{tenant.Id,tenant.Name,OwnerEmail=email});
        });
        admin.MapPut("/tenants/{id:guid}/status",async(Guid id,bool active,AtWashDbContext db,CancellationToken ct) =>
        {
            var tenant=await db.Tenants.FindAsync([id],ct);if(tenant==null)return Results.NotFound();
            tenant.IsActive=active;await db.SaveChangesAsync(ct);return Results.Ok(new{tenant.Id,tenant.IsActive});
        });
        admin.MapPut("/tenants/{id:guid}/subscription",async(Guid id,SaasSubscriptionInput input,
            AtWashDbContext db,CancellationToken ct) =>
        {
            if(input.Status is not ("active" or "trial" or "past_due" or "cancelled" or "suspended"))
                return Results.BadRequest("Status inválido.");
            if(!await db.Tenants.AnyAsync(x=>x.Id==id,ct)||!await db.SubscriptionPlans.AnyAsync(x=>x.Id==input.PlanId,ct))
                return Results.NotFound();
            var sub=await db.TenantSubscriptions.SingleOrDefaultAsync(x=>x.TenantId==id,ct);
            if(sub==null){sub=new TenantSubscription{TenantId=id,PlanId=input.PlanId,Status=input.Status};db.TenantSubscriptions.Add(sub);}
            sub.PlanId=input.PlanId;sub.Status=input.Status;sub.ExpiresAt=input.ExpiresAt;
            await db.SaveChangesAsync(ct);return Results.Ok(sub);
        });
    }
}
