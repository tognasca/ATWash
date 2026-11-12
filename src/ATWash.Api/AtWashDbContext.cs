using Microsoft.EntityFrameworkCore;
namespace ATWash.Api;
public sealed class AtWashDbContext(DbContextOptions<AtWashDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WashService> Services => Set<WashService>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WashOrder> Orders => Set<WashOrder>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Customer>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<Vehicle>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<WashService>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<Vehicle>().HasIndex(x => new { x.TenantId, x.Plate }).IsUnique();
        model.Entity<Vehicle>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.TenantId, x.VehicleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasOne<WashService>().WithMany().HasForeignKey(x => new { x.TenantId, x.ServiceId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        foreach (var entity in model.Model.GetEntityTypes().Where(x => typeof(ITenantEntity).IsAssignableFrom(x.ClrType)))
            model.Entity(entity.ClrType).HasOne(typeof(Tenant)).WithMany().HasForeignKey(nameof(ITenantEntity.TenantId)).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<Product>().Property(x => x.Stock).HasPrecision(18, 3);
        model.Entity<WashService>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<WashOrder>().Property(x => x.Total).HasPrecision(18, 2);
    }
}
