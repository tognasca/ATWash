using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        if (!config.GetValue<bool>("ATWASH_BOOTSTRAP_ADMIN")) return;
        var email = config["ATWASH_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = config["ATWASH_ADMIN_PASSWORD"];
        var company = config["ATWASH_COMPANY_NAME"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(company)
            || string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Configure ATWASH_ADMIN_EMAIL, ATWASH_COMPANY_NAME and ATWASH_ADMIN_PASSWORD (12+ characters).");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AtWashDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Apply all migrations before admin bootstrap.");

        // Only allow bootstrap on an empty set of non-demo tenants.
        if (await db.Tenants.AnyAsync(x => !x.IsDemo, ct))
            return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Tenants.AnyAsync(x => !x.IsDemo, ct)) return;
        var tenant = new Tenant { Name = company, IsDemo = false, IsActive = true };
        var user = new AppUser { TenantId = tenant.Id, Email = email, PasswordHash = "", Role = UserRole.Owner };
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Console.WriteLine($"ATWash bootstrap completed. Tenant ID: {tenant.Id}. Admin email: {email}. Disable ATWASH_BOOTSTRAP_ADMIN now.");
    }
}
