using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

/// <summary>Initial service catalog for a newly provisioned company. Existing catalogs are preserved.</summary>
public static class DefaultServiceCatalog
{
    public static async Task SeedMissingCatalogsAsync(AtWashDbContext db, CancellationToken ct = default)
    {
        var tenantIds = await db.Tenants.AsNoTracking()
            .Where(x => !x.IsDemo && x.IsActive)
            .Select(x => x.Id).ToListAsync(ct);

        var specs = new (string Name, decimal Price)[]
        {
            ("Lavagem simples", 45m),
            ("Lavagem completa", 65m),
            ("Lavagem premium", 90m),
            ("Lavagem de motor", 50m),
            ("Aspiração interna", 25m),
            ("Enceramento", 80m),
            ("Higienização interna", 150m),
            ("Polimento", 250m)
        };

        foreach (var tenantId in tenantIds)
        {
            // Only initialize a completely empty catalog. Never overwrite custom pricing or
            // restore services intentionally removed from a company that has its own catalog.
            if (await db.Services.AnyAsync(x => x.TenantId == tenantId, ct)) continue;
            foreach (var (name, price) in specs)
                db.Services.Add(new WashService { TenantId = tenantId, Name = name, Price = price });
            await db.SaveChangesAsync(ct);
        }
    }
}
