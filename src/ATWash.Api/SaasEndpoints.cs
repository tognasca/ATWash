using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace ATWash.Api;
public static class SaasEndpoints {
 static bool IsManager(ClaimsPrincipal p)=>p.IsInRole("Owner")||p.IsInRole("Manager");
 static bool GetTenant(ClaimsPrincipal p,out Guid t)=>Guid.TryParse(p.FindFirstValue("tenant_id"),out t)&&t!=Guid.Empty;
 public static void MapSaas(this WebApplication app){
  var api=app.MapGroup("/api").RequireAuthorization();
  api.MapGet("/devices",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!IsManager(p)||!GetTenant(p,out var t))return Results.Forbid();
   return Results.Ok(await db.RegisteredDevices.AsNoTracking().Where(x=>x.TenantId==t)
    .OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.Name,x.UserId,x.CreatedAt,x.RevokedAt}).ToListAsync(ct));
  });
  api.MapPost("/devices/{id:guid}/revoke",async(Guid id,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!IsManager(p)||!GetTenant(p,out var t))return Results.Forbid();
   var device=await db.RegisteredDevices.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);
   if(device==null)return Results.NotFound();
   device.RevokedAt??=DateTimeOffset.UtcNow;
   await db.SaveChangesAsync(ct);return Results.Ok(new{device.Id,device.RevokedAt});
  });
  api.MapGet("/dashboard",async(ClaimsPrincipal p,AtWashDbContext db,DateTimeOffset? from,DateTimeOffset? to,CancellationToken ct)=>{
   if(!IsManager(p)||!GetTenant(p,out var t))return Results.Forbid();
   var start=from??DateTimeOffset.UtcNow.AddDays(-30);var end=to??DateTimeOffset.UtcNow;
   if(start>=end||end-start>TimeSpan.FromDays(366))return Results.BadRequest("Período inválido.");
   var orders=await db.Orders.AsNoTracking().Where(x=>x.TenantId==t&&x.CreatedAt>=start&&x.CreatedAt<end).ToListAsync(ct);
   var ids=orders.Select(x=>x.Id).ToArray();
   var payments=await db.Payments.AsNoTracking().Where(x=>x.TenantId==t&&ids.Contains(x.OrderId)).ToListAsync(ct);
   var refunds=await db.PaymentRefunds.AsNoTracking().Where(x=>x.TenantId==t&&payments.Select(p=>p.Id).Contains(x.PaymentId)).ToListAsync(ct);
   var supplies=await db.StockMovements.AsNoTracking().Where(x=>x.TenantId==t&&x.Kind==StockMovementKind.Consumption&&x.CreatedAt>=start&&x.CreatedAt<end).ToListAsync(ct);
   var costs=await db.Products.AsNoTracking().Where(x=>x.TenantId==t).Select(x=>new{x.Id,x.AverageCost}).ToListAsync(ct);
   var costMap=costs.ToDictionary(x=>x.Id,x=>x.AverageCost);
   var gross=payments.Sum(x=>x.Amount);
   var refunded=refunds.Sum(x=>x.Amount);
   return Results.Ok(new{From=start,To=end,Orders=orders.Count,Completed=orders.Count(x=>x.Status==OrderStatus.Delivered),
    Open=orders.Count(x=>x.Status is not OrderStatus.Delivered and not OrderStatus.Cancelled),
    Cancelled=orders.Count(x=>x.Status==OrderStatus.Cancelled),
    GrossReceived=gross,Refunded=refunded,NetReceived=gross-refunded,
    AverageTicket=orders.Count==0?0:decimal.Round(orders.Sum(x=>x.Total)/orders.Count,2),
    EstimatedSupplyCost=supplies.Sum(x=>-x.Quantity*costMap.GetValueOrDefault(x.ProductId)),
    ByDay=orders.GroupBy(x=>x.CreatedAt.UtcDateTime.Date).OrderBy(x=>x.Key)
     .Select(g=>new{Date=g.Key,Orders=g.Count(),Billed=g.Sum(x=>x.Total)}).ToArray(),
    ByStatus=orders.GroupBy(x=>x.Status).Select(g=>new{Status=g.Key,Count=g.Count()}).ToArray()});
  });
  api.MapGet("/saas/plans",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!GetTenant(p,out _))return Results.Forbid();
   return Results.Ok(await db.SubscriptionPlans.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.MonthlyPrice).ToListAsync(ct));
  });
  api.MapGet("/saas/subscription",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!IsManager(p)||!GetTenant(p,out var t))return Results.Forbid();
   var subscription=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==t,ct);
   return Results.Ok(new{Subscription=subscription,Provisioning="manual",BillingIntegration=false});
  });
 }
}
