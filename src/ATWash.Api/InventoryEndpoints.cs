using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public sealed record StockAdjustmentInput(decimal Quantity, string Reason);
public sealed record AddOrderProductInput(Guid ProductId, decimal Quantity, bool IsConsumption = false);

public static class InventoryEndpoints
{
 private static bool Tenant(ClaimsPrincipal p,out Guid tenant) =>
  Guid.TryParse(p.FindFirstValue("tenant_id"),out tenant) && tenant!=Guid.Empty;
 private static bool Manager(ClaimsPrincipal p) => p.IsInRole(nameof(UserRole.Owner)) || p.IsInRole(nameof(UserRole.Manager));
 private static bool Operator(ClaimsPrincipal p) => Manager(p) || p.IsInRole(nameof(UserRole.Attendant));
 private static bool ValidQty(decimal q) => q>0 && q<=1000000 && decimal.Round(q,3)==q;

 public static void MapInventory(this WebApplication app)
 {
  var api=app.MapGroup("/api").RequireAuthorization();
  api.MapGet("/inventory/movements",async (Guid? productId,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!Tenant(p,out var tenant))return Results.Unauthorized();
   var query=db.StockMovements.AsNoTracking().Where(x=>x.TenantId==tenant);
   if(productId.HasValue)query=query.Where(x=>x.ProductId==productId.Value);
   return Results.Ok(await query.OrderByDescending(x=>x.CreatedAt).Take(250).ToListAsync(ct));
  });
  api.MapPost("/products/{id:guid}/stock",async(Guid id,StockAdjustmentInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!Manager(p)||!Tenant(p,out var tenant))return Results.Forbid();
   if(input.Quantity==0 || Math.Abs(input.Quantity)>1000000 || decimal.Round(input.Quantity,3)!=input.Quantity ||
      string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Trim().Length>300)
    return Results.BadRequest("Informe quantidade com até 3 casas e justificativa (máximo 300 caracteres).");
   await using var tx=await db.Database.BeginTransactionAsync(ct);
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))",ct);
   var product=await db.Products.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
   if(product is null)return Results.NotFound();
   if(product.Stock+input.Quantity<0)return Results.Conflict("Estoque insuficiente.");
   product.Stock+=input.Quantity;
   var movement=new StockMovement{TenantId=tenant,ProductId=id,
    Kind=input.Quantity>0?StockMovementKind.Purchase:StockMovementKind.Adjustment,
    Quantity=input.Quantity,BalanceAfter=product.Stock,Reason=input.Reason.Trim()};
   db.StockMovements.Add(movement);
   await db.SaveChangesAsync(ct);
   await tx.CommitAsync(ct);
   return Results.Created($"/api/inventory/movements?productId={id}",new{movement.Id,product.Stock});
  });
  api.MapGet("/orders/{id:guid}/products",async(Guid id,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!Tenant(p,out var tenant))return Results.Unauthorized();
   if(!await db.Orders.AnyAsync(x=>x.TenantId==tenant&&x.Id==id,ct))return Results.NotFound();
   return Results.Ok(await db.OrderProductItems.AsNoTracking().Where(x=>x.TenantId==tenant&&x.OrderId==id).ToListAsync(ct));
  });
  api.MapPost("/orders/{id:guid}/products",async(Guid id,AddOrderProductInput input,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
   if(!Operator(p)||!Tenant(p,out var tenant))return Results.Forbid();
   if(input.ProductId==Guid.Empty||!ValidQty(input.Quantity))return Results.BadRequest("Produto ou quantidade inválida.");
   if(input.IsConsumption&&!Manager(p))return Results.Forbid();
   await using var tx=await db.Database.BeginTransactionAsync(ct);
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()}, 0))",ct);
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()+id.ToString()}, 0))",ct);
   var order=await db.Orders.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==id,ct);
   if(order is null)return Results.NotFound();
   if(order.Status is OrderStatus.Delivered or OrderStatus.Cancelled || order.PaymentStatus!=PaymentStatus.Pending)
    return Results.Conflict("Não é possível adicionar produtos a uma comanda paga ou encerrada.");
   var product=await db.Products.SingleOrDefaultAsync(x=>x.TenantId==tenant&&x.Id==input.ProductId,ct);
   if(product is null)return Results.NotFound();
   if(product.Stock<input.Quantity)return Results.Conflict("Estoque insuficiente.");
   var total=input.IsConsumption?0m:decimal.Round(product.Price*input.Quantity,2);
   product.Stock-=input.Quantity;
   order.Total+=total;
   var line=new OrderProductItem{TenantId=tenant,OrderId=id,ProductId=product.Id,
    ProductName=product.Name,Quantity=input.Quantity,UnitPrice=input.IsConsumption?0m:product.Price,
    Total=total,IsConsumption=input.IsConsumption};
   db.OrderProductItems.Add(line);
   db.StockMovements.Add(new StockMovement{TenantId=tenant,ProductId=product.Id,OrderId=id,
    Kind=input.IsConsumption?StockMovementKind.Consumption:StockMovementKind.Sale,
    Quantity=-input.Quantity,BalanceAfter=product.Stock,
    Reason=input.IsConsumption?"Consumo de insumo na comanda":"Venda na comanda"});
   await db.SaveChangesAsync(ct);
   await tx.CommitAsync(ct);
   return Results.Created($"/api/orders/{id}/products",new{line.Id,line.Total,order.Total,product.Stock});
  });
 }
}
