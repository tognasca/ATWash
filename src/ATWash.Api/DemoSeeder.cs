using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

/// <summary>Demo-only, idempotent seed. Never runs for a real tenant.</summary>
public static class DemoSeeder
{
    public static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static async Task SeedAsync(AtWashDbContext db, CancellationToken ct = default)
    {
        // A transaction prevents partial demo initialization.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == DemoTenantId, ct);
        if (tenant is null)
        {
            tenant = new Tenant { Id = DemoTenantId, Name = "ATWash Demo", IsDemo = true };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
        }
        if (!tenant.IsDemo) throw new InvalidOperationException("Demo tenant ID belongs to a real company.");

        var serviceSpecs = new (string Name, decimal Price)[]
        {
            ("Lavagem simples", 45m), ("Lavagem completa", 65m),
            ("Lavagem premium", 90m), ("Lavagem de motor", 50m),
            ("Aspiração interna", 25m), ("Enceramento", 80m),
            ("Higienização interna", 150m), ("Polimento", 250m)
        };
        foreach (var (name, price) in serviceSpecs)
        {
            if (!await db.Services.AnyAsync(x => x.TenantId == DemoTenantId && x.Name == name, ct))
                db.Services.Add(new WashService { TenantId = DemoTenantId, Name = name, Price = price });
        }

        var productSpecs = new (string Name, decimal Price, decimal Stock)[]
        {
            ("Água mineral", 4m, 50m), ("Refrigerante", 6m, 40m),
            ("Café", 3m, 30m), ("Energético", 12m, 20m),
            ("Aromatizante automotivo", 15m, 25m), ("Pano de microfibra", 12m, 30m)
        };
        foreach (var (name, price, stock) in productSpecs)
        {
            if (!await db.Products.AnyAsync(x => x.TenantId == DemoTenantId && x.Name == name, ct))
                db.Products.Add(new Product { TenantId = DemoTenantId, Name = name, Price = price, Stock = stock });
        }

        var employees = new (string Name, string Role)[]
        {
            ("João Silva", "Lavador"), ("Carlos Oliveira", "Lavador"), ("Mariana Santos", "Atendente")
        };
        foreach (var (name, role) in employees)
        {
            if (!await db.Employees.AnyAsync(x => x.TenantId == DemoTenantId && x.Name == name, ct))
                db.Employees.Add(new Employee { TenantId = DemoTenantId, Name = name, Role = role });
        }
        await db.SaveChangesAsync(ct);

        var specs = new (string Number, string Plate, string Brand, string Model, string Service, OrderStatus Status)[]
        {
            ("001", "ABC-1D23", "Honda", "Civic", "Lavagem completa", OrderStatus.Washing),
            ("002", "FJK-4G56", "Jeep", "Renegade", "Lavagem premium", OrderStatus.Ready),
            ("003", "GHT-7H89", "Fiat", "Argo", "Lavagem simples", OrderStatus.Waiting)
        };
        foreach (var spec in specs)
        {
            var customerId = StableId("customer:" + spec.Number);
            var vehicleId = StableId("vehicle:" + spec.Plate);
            if (!await db.Customers.AnyAsync(x => x.Id == customerId && x.TenantId == DemoTenantId, ct))
                db.Customers.Add(new Customer { Id = customerId, TenantId = DemoTenantId, Name = "Cliente Demo " + spec.Number });
            if (!await db.Vehicles.AnyAsync(x => x.Id == vehicleId && x.TenantId == DemoTenantId, ct))
                db.Vehicles.Add(new Vehicle { Id = vehicleId, TenantId = DemoTenantId, CustomerId = customerId, Plate = spec.Plate, Brand = spec.Brand, Model = spec.Model });
        }
        await db.SaveChangesAsync(ct);

        foreach (var spec in specs)
        {
            if (await db.Orders.AnyAsync(x => x.TenantId == DemoTenantId && x.Number == spec.Number, ct)) continue;
            var service = await db.Services.SingleAsync(x => x.TenantId == DemoTenantId && x.Name == spec.Service, ct);
            db.Orders.Add(new WashOrder
            {
                Id = StableId("order:" + spec.Number), TenantId = DemoTenantId,
                Number = spec.Number, VehicleId = StableId("vehicle:" + spec.Plate),
                ServiceId = service.Id, Status = spec.Status, PaymentStatus = PaymentStatus.Pending,
                Total = service.Price
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static Guid StableId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("ATWash-demo-v1:" + value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
