using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public static class OperationalEndpoints
{
    public static void MapOperations(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/customers", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Customers.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderBy(x => x.Name).Take(200).ToListAsync(ct));
        });

        api.MapPost("/customers", async (CustomerInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 160) return Results.BadRequest("Invalid customer name");
            var customer = new Customer { TenantId = tenant, Name = input.Name.Trim(), Phone = input.Phone?.Trim() };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{customer.Id}", new { customer.Id });
        });

        api.MapGet("/vehicles", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Vehicles.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderBy(x => x.Plate).Take(200).ToListAsync(ct));
        });

        api.MapPost("/vehicles", async (VehicleInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var plate = input.Plate?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(plate) || plate.Length > 10 || string.IsNullOrWhiteSpace(input.Model))
                return Results.BadRequest("Invalid vehicle");
            if (!await db.Customers.AnyAsync(x => x.TenantId == tenant && x.Id == input.CustomerId, ct))
                return Results.BadRequest("Customer does not belong to tenant");
            if (await db.Vehicles.AnyAsync(x => x.TenantId == tenant && x.Plate == plate, ct))
                return Results.Conflict("Plate already registered");
            var vehicle = new Vehicle { TenantId = tenant, CustomerId = input.CustomerId,
                Plate = plate, Model = input.Model.Trim(), Brand = input.Brand?.Trim(), Color = input.Color?.Trim() };
            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/vehicles/{vehicle.Id}", new { vehicle.Id });
        });

        api.MapGet("/services", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Services.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderBy(x => x.Name).ToListAsync(ct));
        });

        api.MapGet("/orders", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Orders.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
        });

        api.MapPost("/orders", async (OrderInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == input.VehicleId, ct);
            var service = await db.Services.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == input.ServiceId, ct);
            if (vehicle is null || service is null) return Results.BadRequest("Invalid vehicle or service");
            if (string.IsNullOrWhiteSpace(input.Number) || input.Number.Length > 30) return Results.BadRequest("Invalid order number");
            if (await db.Orders.AnyAsync(x => x.TenantId == tenant && x.Number == input.Number, ct))
                return Results.Conflict("Order number already exists");
            var order = new WashOrder { TenantId = tenant, Number = input.Number.Trim(),
                VehicleId = vehicle.Id, ServiceId = service.Id, Total = service.Price,
                Status = OrderStatus.Waiting, PaymentStatus = PaymentStatus.Pending };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", new { order.Id, order.Total });
        });
    }

    private static bool TryTenant(ClaimsPrincipal principal, out Guid tenant) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out tenant) && tenant != Guid.Empty;

    private static bool CanWrite(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Owner)) ||
        principal.IsInRole(nameof(UserRole.Manager)) ||
        principal.IsInRole(nameof(UserRole.Attendant));
}

public sealed record CustomerInput(string Name, string? Phone);
public sealed record VehicleInput(Guid CustomerId, string Plate, string Model, string? Brand, string? Color);
public sealed record OrderInput(string Number, Guid VehicleId, Guid ServiceId);
