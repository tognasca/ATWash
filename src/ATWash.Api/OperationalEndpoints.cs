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

        api.MapGet("/customers/search", async (string q, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            q = q?.Trim() ?? "";
            if (q.Length < 2 || q.Length > 80) return Results.BadRequest("Informe ao menos 2 caracteres");
            var customers = await db.Customers.AsNoTracking().Where(x => x.TenantId == tenant &&
                (EF.Functions.ILike(x.Name, "%" + q + "%") || (x.Phone != null && x.Phone.Contains(q)) ||
                 db.Vehicles.Any(v => v.TenantId == tenant && v.CustomerId == x.Id && EF.Functions.ILike(v.Plate, "%" + q + "%"))))
                .OrderBy(x => x.Name).Take(30).ToListAsync(ct);
            var ids = customers.Select(x => x.Id).ToArray();
            var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.TenantId == tenant && ids.Contains(x.CustomerId))
                .OrderBy(x => x.Plate).ToListAsync(ct);
            return Results.Ok(customers.Select(x => new { x.Id, x.Name, x.Phone,
                Vehicles = vehicles.Where(v => v.CustomerId == x.Id).ToArray() }));
        });

        api.MapPost("/customers", async (CustomerInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 160) return Results.BadRequest("Invalid customer name");
            if (input.ClientId == Guid.Empty) return Results.BadRequest("Identificador inválido.");
            if (input.ClientId is Guid customerId)
            {
                var existing = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == customerId, ct);
                if (existing != null)
                    return existing.Name == input.Name.Trim() && existing.Phone == input.Phone?.Trim()
                        ? Results.Ok(new { existing.Id }) : Results.Conflict("Identificador já utilizado.");
            }
            var customer = new Customer { Id = input.ClientId ?? Guid.NewGuid(), TenantId = tenant, Name = input.Name.Trim(), Phone = input.Phone?.Trim() };
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
            if (await db.Vehicles.AnyAsync(x => x.TenantId == tenant && x.Plate == plate && x.Id != input.ClientId, ct))
                return Results.Conflict("Esta placa já está cadastrada. Pesquise a placa e reutilize o veículo.");
            if (input.ClientId == Guid.Empty) return Results.BadRequest("Identificador inválido.");
            if (input.ClientId is Guid vehicleId)
            {
                var existing = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == vehicleId, ct);
                if (existing != null)
                    return existing.CustomerId == input.CustomerId && existing.Plate == plate && existing.Model == input.Model.Trim()
                        ? Results.Ok(new { existing.Id }) : Results.Conflict("Identificador já utilizado.");
            }
            var vehicle = new Vehicle { Id = input.ClientId ?? Guid.NewGuid(), TenantId = tenant, CustomerId = input.CustomerId,
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


        api.MapPost("/services", async (ServiceInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanManageServices(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var name = input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || input.Price < 0 || input.Price > 1000000)
                return Results.BadRequest("Invalid service name or price");
            if (await db.Services.AnyAsync(x => x.TenantId == tenant && x.Name.ToLower() == name.ToLower(), ct))
                return Results.Conflict("Service already exists");
            var service = new WashService { TenantId = tenant, Name = name, Price = input.Price };
            db.Services.Add(service);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/services/{service.Id}", service);
        });

        api.MapPut("/services/{id:guid}", async (Guid id, ServiceInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanManageServices(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var name = input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || input.Price < 0 || input.Price > 1000000)
                return Results.BadRequest("Invalid service name or price");
            var service = await db.Services.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant, ct);
            if (service is null) return Results.NotFound();
            if (await db.Services.AnyAsync(x => x.TenantId == tenant && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
                return Results.Conflict("Service already exists");
            service.Name = name;
            service.Price = input.Price;
            await db.SaveChangesAsync(ct);
            return Results.Ok(service);
        });

        api.MapDelete("/services/{id:guid}", async (Guid id, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanManageServices(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var service = await db.Services.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant, ct);
            if (service is null) return Results.NotFound();
            if (await db.Orders.AnyAsync(x => x.TenantId == tenant && x.ServiceId == id, ct))
                return Results.Conflict("Este serviço já foi utilizado em comandas e não pode ser excluído, pois faz parte do histórico de atendimentos.");
            db.Services.Remove(service);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });


        api.MapGet("/products", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Products.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderBy(x => x.Name).ToListAsync(ct));
        });

        api.MapPost("/products", async (ProductInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanManageServices(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var name = input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || input.Price < 0 || input.Price > 1000000 ||
                input.Stock < 0 || input.Stock > 1000000) return Results.BadRequest("Invalid product");
            if (await db.Products.AnyAsync(x => x.TenantId == tenant && x.Name.ToLower() == name.ToLower(), ct))
                return Results.Conflict("Product already exists");
            var product = new Product { TenantId = tenant, Name = name, Price = input.Price, Stock = input.Stock };
            db.Products.Add(product);
            if(product.Stock>0) db.StockMovements.Add(new StockMovement { TenantId=tenant,ProductId=product.Id,
                Kind=StockMovementKind.Opening,Quantity=product.Stock,BalanceAfter=product.Stock,Reason="Saldo inicial do cadastro" });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{product.Id}", product);
        });

        api.MapPut("/products/{id:guid}", async (Guid id, ProductInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanManageServices(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            var name = input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || input.Price < 0 || input.Price > 1000000 ||
                input.Stock < 0 || input.Stock > 1000000) return Results.BadRequest("Invalid product");
            var product = await db.Products.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
            if (product is null) return Results.NotFound();
            if (await db.Products.AnyAsync(x => x.TenantId == tenant && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
                return Results.Conflict("Product already exists");
            if (input.Stock != product.Stock) return Results.Conflict("Use a movimentação de estoque para alterar o saldo.");
            product.Name = name; product.Price = input.Price;
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        });

        api.MapPatch("/orders/{id:guid}/status", async (Guid id, OrderStatusInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            if (!Enum.IsDefined(input.Status)) return Results.BadRequest("Invalid status");
            if (input.Status == OrderStatus.Delivered)
                return Results.Conflict("Use o fechamento da comanda para entregar o veículo após validar o saldo.");
            await using var statusTx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))", ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString() + id.ToString()}, 0))", ct);
            var order = await db.Orders.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
                return Results.Conflict("Esta comanda já foi finalizada ou cancelada.");
            if (input.Status == OrderStatus.Cancelled)
            {
                if (!CanManageServices(principal)) return Results.Forbid();
            }
            else if ((int)input.Status != (int)order.Status + 1)
                return Results.Conflict("A comanda mudou de etapa. Atualize a lista antes de continuar.");
            if (input.Status == OrderStatus.Delivered && order.PaymentStatus != PaymentStatus.Paid)
                return Results.Conflict("Confirme o pagamento antes de entregar o veículo.");
            if (input.Status == OrderStatus.Ready)
            {
                // Consume each service recipe exactly once when the wash is marked ready.
                var serviceIds = await db.OrderItems.AsNoTracking()
                    .Where(x => x.TenantId == tenant && x.OrderId == id).Select(x => x.ServiceId).ToListAsync(ct);
                // Legacy demo orders may not have order item rows.
                if (serviceIds.Count == 0) serviceIds.Add(order.ServiceId);
                foreach (var serviceId in serviceIds.Distinct())
                {
                    if (await db.ServiceExecutions.AnyAsync(x => x.TenantId == tenant && x.OrderId == id && x.ServiceId == serviceId, ct))
                        continue;
                    var ingredients = await db.ServiceIngredients.AsNoTracking()
                        .Where(x => x.TenantId == tenant && x.ServiceId == serviceId).ToListAsync(ct);
                    foreach (var ingredient in ingredients)
                    {
                        var product = await db.Products.SingleAsync(x => x.TenantId == tenant && x.Id == ingredient.ProductId, ct);
                        if (product.Stock < ingredient.Quantity)
                            return Results.Conflict($"Estoque insuficiente do insumo {product.Name}. Reponha antes de concluir a lavagem.");
                        product.Stock -= ingredient.Quantity;
                        db.StockMovements.Add(new StockMovement { TenantId = tenant, ProductId = product.Id,
                            OrderId = id, Kind = StockMovementKind.Consumption, Quantity = -ingredient.Quantity,
                            BalanceAfter = product.Stock, Reason = "Consumo automático da ficha técnica do serviço" });
                    }
                    db.ServiceExecutions.Add(new ServiceExecution { TenantId = tenant, OrderId = id,
                        ServiceId = serviceId, EmployeeId = order.EmployeeId });
                }
            }
            order.Status = input.Status;
            await db.SaveChangesAsync(ct);
            await statusTx.CommitAsync(ct);
            return Results.Ok(order);
        });

        api.MapGet("/orders", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            return Results.Ok(await db.Orders.AsNoTracking().Where(x => x.TenantId == tenant)
                .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
        });

        api.MapGet("/orders/{id:guid}/items", async (Guid id, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!TryTenant(principal, out var tenant)) return Results.Unauthorized();
            if (!await db.Orders.AnyAsync(x => x.Id == id && x.TenantId == tenant, ct)) return Results.NotFound();
            return Results.Ok(await db.OrderItems.AsNoTracking().Where(x => x.TenantId == tenant && x.OrderId == id)
                .OrderBy(x => x.ServiceName).ToListAsync(ct));
        });

        api.MapPost("/orders", async (OrderInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            if (!await db.Vehicles.AnyAsync(x => x.TenantId == tenant && x.Id == input.VehicleId, ct))
                return Results.BadRequest("Invalid vehicle");
            if (input.ClientId == Guid.Empty) return Results.BadRequest("Identificador inválido.");
            var serviceIds = input.ServiceIds?.Distinct().ToArray() ?? [];
            if (serviceIds.Length is < 1 or > 30) return Results.BadRequest("Select 1 to 30 services");
            var services = await db.Services.AsNoTracking().Where(x => x.TenantId == tenant && serviceIds.Contains(x.Id)).ToListAsync(ct);
            if (services.Count != serviceIds.Length) return Results.BadRequest("Invalid service selection");
            await using var numberTx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))", ct);
            if (input.ClientId is Guid clientOrderId)
            {
                var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == clientOrderId, ct);
                if (existing != null)
                {
                    var existingServices = await db.OrderItems.AsNoTracking().Where(x => x.TenantId == tenant && x.OrderId == clientOrderId)
                        .Select(x => x.ServiceId).ToArrayAsync(ct);
                    if (existing.VehicleId != input.VehicleId || input.ServiceIds == null ||
                        !existingServices.OrderBy(x => x).SequenceEqual(input.ServiceIds.Distinct().OrderBy(x => x)))
                        return Results.Conflict("Identificador de comanda já utilizado.");
                    return Results.Ok(new { existing.Id, existing.Number, existing.Total, AlreadyProcessed = true });
                }
            }

            var numbers = await db.Orders.AsNoTracking().Where(x => x.TenantId == tenant).Select(x => x.Number).ToListAsync(ct);
            var next = numbers.Select(x => long.TryParse(x, out var n) && n > 0 ? n : 0).DefaultIfEmpty(0).Max() + 1;
            var order = new WashOrder { Id = input.ClientId ?? Guid.NewGuid(), TenantId = tenant, Number = next.ToString("D6"),
                VehicleId = input.VehicleId, ServiceId = services[0].Id,
                Total = services.Sum(x => x.Price), Status = OrderStatus.Waiting, PaymentStatus = PaymentStatus.Pending };
            db.Orders.Add(order);
            foreach (var service in services)
                db.OrderItems.Add(new WashOrderItem { TenantId = tenant, OrderId = order.Id,
                    ServiceId = service.Id, ServiceName = service.Name, UnitPrice = service.Price,
                    Quantity = 1, Total = service.Price });
            await db.SaveChangesAsync(ct);
            await numberTx.CommitAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", new { order.Id, order.Number, order.Total });
        });

        api.MapPost("/orders/{id:guid}/services", async (Guid id, AddOrderServiceInput input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanWrite(principal) || !TryTenant(principal, out var tenant)) return Results.Forbid();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant, ct);
            if (order is null) return Results.NotFound();
            if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled || order.PaymentStatus != PaymentStatus.Pending)
                return Results.Conflict("Cannot change a closed or paid order");
            var service = await db.Services.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ServiceId && x.TenantId == tenant, ct);
            if (service is null) return Results.BadRequest("Invalid service");
            if (await db.OrderItems.AnyAsync(x => x.TenantId == tenant && x.OrderId == id && x.ServiceId == input.ServiceId, ct))
                return Results.Conflict("Service already added");
            db.OrderItems.Add(new WashOrderItem { TenantId = tenant, OrderId = id, ServiceId = service.Id,
                ServiceName = service.Name, UnitPrice = service.Price, Quantity = 1, Total = service.Price });
            order.Total += service.Price;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { order.Id, order.Total });
        });

    }

    private static bool TryTenant(ClaimsPrincipal principal, out Guid tenant) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out tenant) && tenant != Guid.Empty;

    private static bool CanManageServices(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Owner)) || principal.IsInRole(nameof(UserRole.Manager));

    private static bool CanWrite(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Owner)) ||
        principal.IsInRole(nameof(UserRole.Manager)) ||
        principal.IsInRole(nameof(UserRole.Attendant));
}

public sealed record CustomerInput(string Name, string? Phone, Guid? ClientId = null);
public sealed record VehicleInput(Guid CustomerId, string Plate, string Model, string? Brand, string? Color, Guid? ClientId = null);
public sealed record ProductInput(string Name, decimal Price, decimal Stock);
public sealed record OrderStatusInput(OrderStatus Status);
public sealed record ServiceInput(string Name, decimal Price);
public sealed record OrderInput(Guid VehicleId, Guid[] ServiceIds, Guid? ClientId = null);
public sealed record AddOrderServiceInput(Guid ServiceId);
