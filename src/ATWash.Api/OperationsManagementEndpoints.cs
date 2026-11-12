using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
namespace ATWash.Api;
public sealed record EmployeeInput(string Name,string Role,string? Phone,bool IsActive=true,decimal CommissionPercent=0,Guid? UserId=null);
public sealed record SupplierInput(string Name,string? Phone,string? TaxId,bool IsActive=true);
public sealed record ProductDetailsInput(string Name,decimal Price,decimal MinimumStock,decimal AverageCost,string Unit,bool IsSupply,Guid? SupplierId);
public sealed record RecipeInput(Guid ProductId,decimal Quantity);
public sealed record AssignEmployeeInput(Guid? EmployeeId);
public sealed record EmployeeAccessInput(string Email,string Password,UserRole Role);
public static class OperationsManagementEndpoints {
 static bool Tenant(ClaimsPrincipal p,out Guid t)=>Guid.TryParse(p.FindFirstValue("tenant_id"),out t)&&t!=Guid.Empty;
 static bool Manage(ClaimsPrincipal p)=>p.IsInRole("Owner")||p.IsInRole("Manager");
 static bool Staff(ClaimsPrincipal p)=>Manage(p)||p.IsInRole("Attendant");
 static bool Allowed(ClaimsPrincipal p)=>Staff(p)||p.IsInRole("Washer");
 public static void MapOperationsManagement(this WebApplication app) {
 var api=app.MapGroup("/api").RequireAuthorization();
 api.MapGet("/employees",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Allowed(p)||!Tenant(p,out var t))return Results.Forbid();
  return Results.Ok(await db.Employees.AsNoTracking().Where(x=>x.TenantId==t).OrderBy(x=>x.Name).ToListAsync(ct));
 });
 api.MapPost("/employees",async(EmployeeInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  var error=await ValidateEmployee(i,t,db,ct);if(error!=null)return Results.BadRequest(error);
  var e=new Employee{TenantId=t,Name=i.Name.Trim(),Role=i.Role.Trim(),Phone=i.Phone?.Trim(),IsActive=i.IsActive,CommissionPercent=i.CommissionPercent,UserId=i.UserId};
  db.Employees.Add(e);await db.SaveChangesAsync(ct);return Results.Created("/api/employees/"+e.Id,e);
 });
 api.MapPut("/employees/{id:guid}",async(Guid id,EmployeeInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  var e=await db.Employees.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);if(e==null)return Results.NotFound();
  var error=await ValidateEmployee(i,t,db,ct);if(error!=null)return Results.BadRequest(error);
  if(i.UserId.HasValue&&await db.Employees.AnyAsync(x=>x.TenantId==t&&x.Id!=id&&x.UserId==i.UserId,ct))return Results.Conflict("Usuário já vinculado a outro funcionário.");
  e.Name=i.Name.Trim();e.Role=i.Role.Trim();e.Phone=i.Phone?.Trim();e.IsActive=i.IsActive;e.CommissionPercent=i.CommissionPercent;e.UserId=i.UserId;
  if(e.UserId.HasValue){var account=await db.Users.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==e.UserId,ct);if(account!=null)account.IsActive=e.IsActive;}
  await db.SaveChangesAsync(ct);return Results.Ok(e);
 });
 api.MapPost("/employees/{id:guid}/access",async(Guid id,EmployeeAccessInput i,ClaimsPrincipal p,AtWashDbContext db,IPasswordHasher<AppUser> hasher,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  if(i.Role==UserRole.Owner||(!p.IsInRole("Owner")&&i.Role==UserRole.Manager))return Results.Forbid();
  var email=i.Email?.Trim().ToLowerInvariant();
  if(string.IsNullOrWhiteSpace(email)||!email.Contains('@')||i.Password?.Length<12||i.Password.Length>128)return Results.BadRequest("E-mail ou senha inválidos. A senha deve ter pelo menos 12 caracteres.");
  var e=await db.Employees.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);
  if(e==null)return Results.NotFound();
  if(await db.Users.AnyAsync(x=>x.Email==email&&x.Id!=e.UserId,ct))return Results.Conflict("E-mail já utilizado.");
  var user=e.UserId.HasValue?await db.Users.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==e.UserId,ct):null;
  if(user==null){user=new AppUser{TenantId=t,Email=email,PasswordHash="",Role=i.Role};db.Users.Add(user);e.UserId=user.Id;}
  user.Email=email;user.Role=i.Role;user.IsActive=e.IsActive;
  user.PasswordHash=hasher.HashPassword(user,i.Password);
  await db.SaveChangesAsync(ct);return Results.Ok(new{user.Id,user.Email,user.Role,user.IsActive});
 });
 api.MapGet("/employees/performance",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  var staff=await db.Employees.AsNoTracking().Where(x=>x.TenantId==t).ToListAsync(ct);
  var orders=await db.Orders.AsNoTracking().Where(x=>x.TenantId==t&&x.EmployeeId!=null&&x.Status==OrderStatus.Delivered).ToListAsync(ct);
  return Results.Ok(staff.Select(e=>new{e.Id,e.Name,Orders=orders.Count(o=>o.EmployeeId==e.Id),Revenue=orders.Where(o=>o.EmployeeId==e.Id).Sum(o=>o.Total),EstimatedCommission=decimal.Round(orders.Where(o=>o.EmployeeId==e.Id).Sum(o=>o.Total)*e.CommissionPercent/100m,2)}));
 });
 api.MapPut("/orders/{id:guid}/employee",async(Guid id,AssignEmployeeInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Staff(p)||!Tenant(p,out var t))return Results.Forbid();
  var order=await db.Orders.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);
  if(order==null)return Results.NotFound();
  if(order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)return Results.Conflict("Comanda encerrada.");
  if(i.EmployeeId.HasValue&&!await db.Employees.AnyAsync(x=>x.TenantId==t&&x.Id==i.EmployeeId&&x.IsActive,ct))
   return Results.BadRequest("Funcionário não encontrado ou inativo.");
  order.EmployeeId=i.EmployeeId;await db.SaveChangesAsync(ct);return Results.Ok(order);
 });
 api.MapGet("/suppliers",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  return Results.Ok(await db.Suppliers.AsNoTracking().Where(x=>x.TenantId==t).OrderBy(x=>x.Name).ToListAsync(ct));
 });
 api.MapPost("/suppliers",async(SupplierInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  if(string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>160)return Results.BadRequest("Nome do fornecedor inválido.");
  var s=new Supplier{TenantId=t,Name=i.Name.Trim(),Phone=i.Phone?.Trim(),TaxId=i.TaxId?.Trim(),IsActive=i.IsActive};
  db.Suppliers.Add(s);await db.SaveChangesAsync(ct);return Results.Created("/api/suppliers/"+s.Id,s);
 });
 api.MapPut("/suppliers/{id:guid}",async(Guid id,SupplierInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  if(string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>160)return Results.BadRequest("Nome do fornecedor inválido.");
  var s=await db.Suppliers.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);if(s==null)return Results.NotFound();
  s.Name=i.Name.Trim();s.Phone=i.Phone?.Trim();s.TaxId=i.TaxId?.Trim();s.IsActive=i.IsActive;
  await db.SaveChangesAsync(ct);return Results.Ok(s);
 });
 api.MapPut("/products/{id:guid}/details",async(Guid id,ProductDetailsInput i,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  if(string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>160||i.Price<0||i.AverageCost<0||i.MinimumStock<0||
     i.Unit is null||i.Unit.Trim().Length is <1 or >12)return Results.BadRequest("Dados do produto inválidos.");
  var product=await db.Products.SingleOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct);if(product==null)return Results.NotFound();
  if(i.SupplierId.HasValue&&!await db.Suppliers.AnyAsync(x=>x.TenantId==t&&x.Id==i.SupplierId,ct))return Results.BadRequest("Fornecedor inválido.");
  product.Name=i.Name.Trim();product.Price=i.Price;product.MinimumStock=i.MinimumStock;product.AverageCost=i.AverageCost;product.Unit=i.Unit.Trim();product.IsSupply=i.IsSupply;product.SupplierId=i.SupplierId;
  await db.SaveChangesAsync(ct);return Results.Ok(product);
 });
 api.MapGet("/inventory/low-stock",async(ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  return Results.Ok(await db.Products.AsNoTracking().Where(x=>x.TenantId==t&&x.Stock<=x.MinimumStock).ToListAsync(ct));
 });
 api.MapGet("/services/{id:guid}/recipe",async(Guid id,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Allowed(p)||!Tenant(p,out var t))return Results.Forbid();
  if(!await db.Services.AnyAsync(x=>x.TenantId==t&&x.Id==id,ct))return Results.NotFound();
  return Results.Ok(await db.ServiceIngredients.AsNoTracking().Where(x=>x.TenantId==t&&x.ServiceId==id).ToListAsync(ct));
 });
 api.MapPut("/services/{id:guid}/recipe",async(Guid id,List<RecipeInput> items,ClaimsPrincipal p,AtWashDbContext db,CancellationToken ct)=>{
  if(!Manage(p)||!Tenant(p,out var t))return Results.Forbid();
  if(items.Count>50||items.Any(x=>x.ProductId==Guid.Empty||x.Quantity<=0||x.Quantity>1000000||decimal.Round(x.Quantity,3)!=x.Quantity)||
   items.Select(x=>x.ProductId).Distinct().Count()!=items.Count)return Results.BadRequest("Ficha técnica inválida.");
  if(!await db.Services.AnyAsync(x=>x.TenantId==t&&x.Id==id,ct))return Results.NotFound();
  var ids=items.Select(x=>x.ProductId).ToArray();
  if(await db.Products.CountAsync(x=>x.TenantId==t&&ids.Contains(x.Id)&&x.IsSupply,ct)!=items.Count)
   return Results.BadRequest("A ficha técnica deve conter apenas insumos cadastrados na empresa.");
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({t.ToString()}, 0))",ct);
  var existing=await db.ServiceIngredients.Where(x=>x.TenantId==t&&x.ServiceId==id).ToListAsync(ct);
  db.ServiceIngredients.RemoveRange(existing);
  await db.SaveChangesAsync(ct);
  db.ServiceIngredients.AddRange(items.Select(x=>new ServiceIngredient{TenantId=t,ServiceId=id,ProductId=x.ProductId,Quantity=x.Quantity}));
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  return Results.Ok(new{Count=items.Count});
 });
 }
 static async Task<string?> ValidateEmployee(EmployeeInput i,Guid t,AtWashDbContext db,CancellationToken ct) {
  if(string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>160||string.IsNullOrWhiteSpace(i.Role)||i.Role.Length>50||
   i.CommissionPercent<0||i.CommissionPercent>100)return "Dados do funcionário inválidos.";
  if(i.UserId.HasValue&&!await db.Users.AnyAsync(x=>x.TenantId==t&&x.Id==i.UserId,ct))return "Usuário de acesso inválido para esta empresa.";
  return null;
 }
}
