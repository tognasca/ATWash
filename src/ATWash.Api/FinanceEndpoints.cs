using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;
public sealed record OpenCashInput(decimal OpeningFloat);
public sealed record CloseCashInput(decimal CountedCash, string? Note);
public sealed record CashMovementInput(CashMovementKind Kind, decimal Amount, string Reason);
public sealed record PixSettingsInput(string PixKey, string MerchantName, string MerchantCity);
public sealed record RefundInput(decimal Amount, string Reason, string ExternalReference);
public sealed record ReconcileInput(string BankReference);

public static class FinanceEndpoints
{
    private static bool Context(ClaimsPrincipal p, out Guid tenant, out Guid user)
    {
        var hasTenant = Guid.TryParse(p.FindFirstValue("tenant_id"), out tenant);
        var hasUser = Guid.TryParse(p.FindFirstValue(JwtRegisteredClaimNames.Sub), out user);
        return hasTenant && tenant != Guid.Empty && hasUser && user != Guid.Empty;
    }
    private static bool Manager(ClaimsPrincipal p) => p.IsInRole(nameof(UserRole.Owner)) || p.IsInRole(nameof(UserRole.Manager));
    private static bool Cashier(ClaimsPrincipal p) => Manager(p) || p.IsInRole(nameof(UserRole.Attendant));
    private static bool Money(decimal n) => n>=0 && n<=10000000 && decimal.Round(n,2)==n;

    public static void MapFinance(this WebApplication app)
    {
        var api=app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/cash/current",async (ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Context(p,out var tenant,out _))return Results.Unauthorized();
            var s=await db.CashSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant && x.ClosedAt==null,ct);
            if(s==null)return Results.Ok(new { Open=false, Session=(object?)null });
            var movements=await db.CashMovements.AsNoTracking().Where(x=>x.TenantId==tenant && x.SessionId==s.Id).ToListAsync(ct);
            var cash=await db.Payments.AsNoTracking().Where(x=>x.TenantId==tenant && x.Method==PaymentMethod.Cash &&
                x.ConfirmedAt>=s.OpenedAt).SumAsync(x=>(decimal?)x.Amount,ct)??0m;
            var refunds=await (from refund in db.PaymentRefunds join payment in db.Payments on new {refund.TenantId,Id=refund.PaymentId} equals new {payment.TenantId,payment.Id}
                where refund.TenantId==tenant && payment.Method==PaymentMethod.Cash && refund.ConfirmedAt>=s.OpenedAt
                select (decimal?)refund.Amount).SumAsync(ct)??0m;
            var expected=s.OpeningFloat+cash-refunds+movements.Sum(x=>x.Kind==CashMovementKind.Supply?x.Amount:-x.Amount);
            return Results.Ok(new { Open=true,Session=s,ExpectedCash=expected,CashSales=cash,CashRefunds=refunds,Movements=movements });
        });
        api.MapPost("/cash/open",async (OpenCashInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Cashier(p)||!Context(p,out var tenant,out var user))return Results.Forbid();
            if(!Money(input.OpeningFloat))return Results.BadRequest("Fundo de troco inválido.");
            if(await db.CashSessions.AnyAsync(x=>x.TenantId==tenant && x.ClosedAt==null,ct))return Results.Conflict("Já existe caixa aberto.");
            var s=new CashSession{TenantId=tenant,OpenedBy=user,OpeningFloat=input.OpeningFloat};
            db.CashSessions.Add(s);
            try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return Results.Conflict("Já existe caixa aberto.");}
            return Results.Created("/api/cash/current",s);
        });
        api.MapPost("/cash/movements",async (CashMovementInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Cashier(p)||!Context(p,out var tenant,out var user))return Results.Forbid();
            if(!Enum.IsDefined(input.Kind)||!Money(input.Amount)||input.Amount<=0||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>250)
                return Results.BadRequest("Movimentação inválida.");
            var s=await db.CashSessions.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.ClosedAt==null,ct);
            if(s==null)return Results.Conflict("Abra o caixa primeiro.");
            var m=new CashMovement{TenantId=tenant,SessionId=s.Id,Kind=input.Kind,Amount=input.Amount,Reason=input.Reason.Trim(),CreatedBy=user};
            db.CashMovements.Add(m);await db.SaveChangesAsync(ct);return Results.Created("/api/cash/current",m);
        });
        api.MapPost("/cash/close",async (CloseCashInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Manager(p)||!Context(p,out var tenant,out var user))return Results.Forbid();
            if(!Money(input.CountedCash)||input.Note?.Length>500)return Results.BadRequest("Conferência inválida.");
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))",ct);
            var s=await db.CashSessions.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.ClosedAt==null,ct);
            if(s==null)return Results.Conflict("Nenhum caixa aberto.");
            var cash=await db.Payments.Where(x=>x.TenantId==tenant&&x.Method==PaymentMethod.Cash&&x.ConfirmedAt>=s.OpenedAt).SumAsync(x=>(decimal?)x.Amount,ct)??0m;
            var refunds=await (from refund in db.PaymentRefunds join payment in db.Payments on new {refund.TenantId,Id=refund.PaymentId} equals new {payment.TenantId,payment.Id}
                where refund.TenantId==tenant&&payment.Method==PaymentMethod.Cash&&refund.ConfirmedAt>=s.OpenedAt select (decimal?)refund.Amount).SumAsync(ct)??0m;
            var moves=await db.CashMovements.Where(x=>x.TenantId==tenant&&x.SessionId==s.Id).ToListAsync(ct);
            var expected=s.OpeningFloat+cash-refunds+moves.Sum(x=>x.Kind==CashMovementKind.Supply?x.Amount:-x.Amount);
            s.ExpectedCash=expected;s.CountedCash=input.CountedCash;s.ClosingNote=input.Note?.Trim();s.ClosedBy=user;s.ClosedAt=DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return Results.Ok(new {s.Id,Expected=expected,Counted=input.CountedCash,Difference=input.CountedCash-expected});
        });
        api.MapGet("/settings/pix",async (ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Context(p,out var tenant,out _))return Results.Unauthorized();
            var settings=await db.PixSettings.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant,ct);
            return settings==null?Results.NotFound():Results.Ok(settings);
        });
        api.MapPut("/settings/pix",async (PixSettingsInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Manager(p)||!Context(p,out var tenant,out _))return Results.Forbid();
            if(string.IsNullOrWhiteSpace(input.PixKey)||input.PixKey.Length>77||string.IsNullOrWhiteSpace(input.MerchantName)||
                input.MerchantName.Length>25||string.IsNullOrWhiteSpace(input.MerchantCity)||input.MerchantCity.Length>15)
                return Results.BadRequest("Dados Pix inválidos.");
            var s=await db.PixSettings.SingleOrDefaultAsync(x=>x.TenantId==tenant,ct);
            if(s==null){s=new TenantPixSettings{TenantId=tenant,PixKey=input.PixKey.Trim(),MerchantName=input.MerchantName.Trim(),MerchantCity=input.MerchantCity.Trim()};db.PixSettings.Add(s);}
            else{s.PixKey=input.PixKey.Trim();s.MerchantName=input.MerchantName.Trim();s.MerchantCity=input.MerchantCity.Trim();}
            await db.SaveChangesAsync(ct);return Results.Ok(new {Configured=true});
        });
        api.MapGet("/orders/{id:guid}/pix",async (Guid id,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Context(p,out var tenant,out _))return Results.Unauthorized();
            var s=await db.PixSettings.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant,ct);
            if(s==null)return Results.Conflict("Configure a chave Pix no painel administrativo.");
            var o=await db.Orders.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
            if(o==null)return Results.NotFound();
            var paid=await db.Payments.Where(x=>x.TenantId==tenant&&x.OrderId==id).SumAsync(x=>(decimal?)x.Amount,ct)??0m;
            var remaining=o.Total-paid;
            if(remaining<=0)return Results.Conflict("Comanda já quitada.");
            return Results.Ok(new {Amount=remaining,Payload=PixPayload.Build(s.PixKey,s.MerchantName,s.MerchantCity,remaining,"***"),
                AutomaticConfirmation=false,Warning="Confirme o crédito na conta antes de registrar o pagamento."});
        });
        api.MapGet("/orders/{id:guid}/receipt",async (Guid id,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Context(p,out var tenant,out _))return Results.Unauthorized();
            var order=await db.Orders.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
            if(order==null)return Results.NotFound();
            var customer=await (from v in db.Vehicles join c in db.Customers on new {v.TenantId,Id=v.CustomerId} equals new {c.TenantId,c.Id}
                where v.TenantId==tenant&&v.Id==order.VehicleId select new {c.Name,v.Plate,v.Model}).SingleOrDefaultAsync(ct);
            var items=await db.OrderItems.AsNoTracking().Where(x=>x.TenantId==tenant&&x.OrderId==id).Select(x=>new{x.ServiceName,x.Quantity,x.UnitPrice,x.Total}).ToListAsync(ct);
            var payments=await db.Payments.AsNoTracking().Where(x=>x.TenantId==tenant&&x.OrderId==id).Select(x=>new{x.Method,x.Amount,x.ConfirmedAt,x.Reference}).ToListAsync(ct);
            var refunds=await (from refund in db.PaymentRefunds join pay in db.Payments on new {refund.TenantId,Id=refund.PaymentId} equals new {pay.TenantId,pay.Id}
                where refund.TenantId==tenant&&pay.OrderId==id select refund.Amount).SumAsync(ct);
            var company=await db.Tenants.AsNoTracking().Where(x=>x.Id==tenant).Select(x=>x.Name).SingleAsync(ct);
            return Results.Ok(new {Company=company,order.Number,order.CreatedAt,Customer=customer,Items=items,Payments=payments,
                order.Total,Refunded=refunds,Paid=payments.Sum(x=>x.Amount)-refunds,order.Status,
                Notice="Comprovante comercial, não é documento fiscal."});
        });
        api.MapGet("/finance/payments",async (ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Manager(p)||!Context(p,out var tenant,out _))return Results.Forbid();
            var payments=await (from pay in db.Payments.AsNoTracking()
                join order in db.Orders.AsNoTracking() on new {pay.TenantId,Id=pay.OrderId} equals new {order.TenantId,order.Id}
                where pay.TenantId==tenant
                orderby pay.ConfirmedAt descending
                select new {pay.Id,pay.Method,pay.Amount,pay.Reference,pay.ConfirmedAt,OrderNumber=order.Number,
                    Reconciled=db.BankReconciliations.Any(r=>r.TenantId==tenant&&r.PaymentId==pay.Id),
                    Refunded=db.PaymentRefunds.Where(r=>r.TenantId==tenant&&r.PaymentId==pay.Id).Sum(r=>(decimal?)r.Amount)??0m})
                .Take(200).ToListAsync(ct);
            return Results.Ok(payments);
        });
        api.MapPost("/payments/{id:guid}/reconcile",async (Guid id,ReconcileInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Manager(p)||!Context(p,out var tenant,out var user))return Results.Forbid();
            if(string.IsNullOrWhiteSpace(input.BankReference)||input.BankReference.Length>150)return Results.BadRequest("Referência inválida.");
            var pay=await db.Payments.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
            if(pay==null)return Results.NotFound();
            if(pay.Method==PaymentMethod.Cash)return Results.Conflict("Dinheiro não é conciliado por extrato bancário.");
            if(await db.BankReconciliations.AnyAsync(x=>x.TenantId==tenant&&(x.PaymentId==id||x.BankReference==input.BankReference.Trim()),ct))
                return Results.Conflict("Pagamento ou referência já conciliado.");
            db.BankReconciliations.Add(new BankReconciliation{TenantId=tenant,PaymentId=id,BankReference=input.BankReference.Trim(),ReconciledBy=user});
            await db.SaveChangesAsync(ct);return Results.Ok(new{Reconciled=true});
        });
        api.MapPost("/payments/{id:guid}/refund",async (Guid id,RefundInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
            if(!Manager(p)||!Context(p,out var tenant,out var user))return Results.Forbid();
            if(!Money(input.Amount)||input.Amount<=0||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>250||
                string.IsNullOrWhiteSpace(input.ExternalReference)||input.ExternalReference.Length>150)
                return Results.BadRequest("Dados de estorno inválidos.");
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()+id.ToString()}, 0))",ct);
            var payment=await db.Payments.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
            if(payment==null)return Results.NotFound();
            var already=await db.PaymentRefunds.Where(x=>x.TenantId==tenant&&x.PaymentId==id).SumAsync(x=>(decimal?)x.Amount,ct)??0m;
            if(input.Amount>payment.Amount-already)return Results.Conflict("Estorno maior que o valor disponível.");
            if(await db.PaymentRefunds.AnyAsync(x=>x.TenantId==tenant&&x.PaymentId==id&&x.ExternalReference==input.ExternalReference.Trim(),ct))
                return Results.Conflict("Referência de estorno já registrada.");
            var refund=new PaymentRefund{TenantId=tenant,PaymentId=id,Amount=input.Amount,Reason=input.Reason.Trim(),ExternalReference=input.ExternalReference.Trim(),ConfirmedBy=user};
            db.PaymentRefunds.Add(refund);
            var order=await db.Orders.SingleAsync(x=>x.TenantId==tenant&&x.Id==payment.OrderId,ct);
            var received=await db.Payments.Where(x=>x.TenantId==tenant&&x.OrderId==order.Id).SumAsync(x=>(decimal?)x.Amount,ct)??0m;
            var returned=await (from r in db.PaymentRefunds join pay in db.Payments on new {r.TenantId,Id=r.PaymentId} equals new {pay.TenantId,pay.Id}
                where r.TenantId==tenant&&pay.OrderId==order.Id select (decimal?)r.Amount).SumAsync(ct)??0m;
            var net=received-returned-input.Amount;
            order.PaymentStatus=net<=0?PaymentStatus.Refunded:net<order.Total?PaymentStatus.PartiallyPaid:PaymentStatus.Paid;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return Results.Ok(new{refund.Id,refund.Amount,order.PaymentStatus,ExternalRefundConfirmedManually=true});
        });
    }
}
