using ATWash.Api;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AtWashDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
var app = builder.Build();
if (app.Configuration.GetValue<bool>("ATWASH_SEED_DEMO"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AtWashDbContext>();
    if (!(await db.Database.GetPendingMigrationsAsync()).Any() && (await db.Database.GetAppliedMigrationsAsync()).Any())
        await DemoSeeder.SeedAsync(db);
    else
        throw new InvalidOperationException("Demo seed requires all EF Core migrations to be applied first.");
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapHealthChecks("/health");
// Public API is intentionally read-only and limited to the demo tenant until authentication is implemented.
app.MapGet("/api/demo", async (AtWashDbContext db, CancellationToken ct) =>
{
    var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.IsDemo, ct);
    if (tenant is null) return Results.NotFound();
    var services = await db.Services.AsNoTracking().Where(x => x.TenantId == tenant.Id).Select(x => new { x.Name, x.Price }).ToListAsync(ct);
    var orders = await db.Orders.AsNoTracking().Where(x => x.TenantId == tenant.Id).Select(x => new { x.Number, x.Status, x.PaymentStatus, x.Total }).ToListAsync(ct);
    return Results.Ok(new { tenant.Name, Services = services, Orders = orders });
});
app.Run();
public partial class Program { }
