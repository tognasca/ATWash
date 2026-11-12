using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public sealed record ConfirmPaymentInput(Guid OperationId, PaymentMethod Method, decimal Amount, decimal Tendered, string? Reference);
public static class PaymentEndpoints
{
    private static bool Tenant(ClaimsPrincipal principal, out Guid id) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out id) && id != Guid.Empty;
    private static bool CanReceive(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Owner)) || principal.IsInRole(nameof(UserRole.Manager)) ||
        principal.IsInRole(nameof(UserRole.Attendant));

    public static void MapPayments(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").RequireAuthorization();

        group.MapGet("/{id:guid}/payments", async (Guid id, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!Tenant(principal, out var tenant)) return Results.Unauthorized();
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.TenantId == tenant && o.Id == id, ct);
            if (order is null) return Results.NotFound();
            var payments = await db.Payments.AsNoTracking().Where(p => p.TenantId == tenant && p.OrderId == id)
                .OrderBy(p => p.ConfirmedAt).ToListAsync(ct);
            var paid = payments.Sum(p => p.Amount);
            return Results.Ok(new { order.Id, order.Number, order.Total, Paid = paid,
                Remaining = Math.Max(0, order.Total - paid), order.PaymentStatus, Payments = payments });
        });

        group.MapPost("/{id:guid}/payments", async (Guid id, ConfirmPaymentInput input, ClaimsPrincipal principal,
            AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanReceive(principal) || !Tenant(principal, out var tenant)) return Results.Forbid();
            if (input.OperationId == Guid.Empty || !Enum.IsDefined(input.Method) ||
                input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount ||
                input.Tendered < input.Amount || decimal.Round(input.Tendered, 2) != input.Tendered ||
                input.Tendered > 10000000 || input.Reference?.Length > 150)
                return Results.BadRequest("Valor ou identificador de pagamento inválido.");
            if (input.Method != PaymentMethod.Cash && input.Tendered != input.Amount)
                return Results.BadRequest("Troco é permitido somente em dinheiro.");
            if (input.Method == PaymentMethod.Pix &&
                !principal.IsInRole(nameof(UserRole.Owner)) && !principal.IsInRole(nameof(UserRole.Manager)))
                return Results.Forbid();
            if (input.Method == PaymentMethod.Card && string.IsNullOrWhiteSpace(input.Reference))
                return Results.BadRequest("Informe o NSU ou código de autorização da maquininha.");
            if (input.Method == PaymentMethod.Pix && string.IsNullOrWhiteSpace(input.Reference))
                return Results.BadRequest("Informe a referência da transferência Pix confirmada.");
            if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
                return Results.Unauthorized();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString() + id.ToString()}, 0))", ct);
            var existing = await db.Payments.AsNoTracking().SingleOrDefaultAsync(
                p => p.TenantId == tenant && p.OperationId == input.OperationId, ct);
            if (existing is not null)
            {
                if (existing.OrderId != id || existing.Amount != input.Amount ||
                    existing.Method != input.Method || existing.Tendered != input.Tendered ||
                    existing.Reference != input.Reference?.Trim())
                    return Results.Conflict("Identificador de operação já utilizado com outros dados.");
                return Results.Ok(new { existing.Id, existing.Amount, existing.Method, existing.ConfirmedAt, AlreadyProcessed = true });
            }
            var order = await db.Orders.SingleOrDefaultAsync(o => o.TenantId == tenant && o.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
                return Results.Conflict("A comanda está cancelada ou já foi entregue.");
            var paid = await db.Payments.Where(p => p.TenantId == tenant && p.OrderId == id).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
            var remaining = order.Total - paid;
            if (remaining <= 0 || input.Amount > remaining)
                return Results.Conflict($"Valor superior ao saldo pendente de R$ {Math.Max(0, remaining):N2}.");
            if (input.Method == PaymentMethod.Cash)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))", ct);
                if (!await db.CashSessions.AnyAsync(x => x.TenantId == tenant && x.ClosedAt == null, ct))
                    return Results.Conflict("Abra o caixa antes de receber em dinheiro.");
            }
            var payment = new OrderPayment { TenantId = tenant, OrderId = id, OperationId = input.OperationId,
                Method = input.Method, Amount = input.Amount, Tendered = input.Tendered,
                Reference = input.Reference?.Trim(), ConfirmedByUserId = userId };
            db.Payments.Add(payment);
            order.PaymentStatus = input.Amount == remaining ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/orders/{id}/payments", new { payment.Id, payment.Amount, payment.Method,
                Change = payment.Tendered - payment.Amount, order.PaymentStatus, Remaining = remaining - input.Amount });
        });

        group.MapPost("/{id:guid}/deliver", async (Guid id, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!CanReceive(principal) || !Tenant(principal, out var tenant)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString() + id.ToString()}, 0))", ct);
            var order = await db.Orders.SingleOrDefaultAsync(o => o.TenantId == tenant && o.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (order.Status == OrderStatus.Delivered) return Results.Ok(new { order.Id, order.Status, AlreadyDelivered = true });
            if (order.Status != OrderStatus.Ready) return Results.Conflict("A lavagem precisa estar marcada como pronta.");
            var paid = await db.Payments.Where(p => p.TenantId == tenant && p.OrderId == id)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
            if (order.PaymentStatus != PaymentStatus.Paid || paid != order.Total)
                return Results.Conflict("Confirme o pagamento integral antes de entregar o veículo.");
            order.Status = OrderStatus.Delivered;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { order.Id, order.Status });
        });
    }
}
