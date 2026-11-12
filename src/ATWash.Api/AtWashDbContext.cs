using Microsoft.EntityFrameworkCore;
namespace ATWash.Api;
public sealed class AtWashDbContext(DbContextOptions<AtWashDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WashService> Services => Set<WashService>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WashOrder> Orders => Set<WashOrder>();
    public DbSet<WashOrderItem> OrderItems => Set<WashOrderItem>();
    public DbSet<OrderPayment> Payments => Set<OrderPayment>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<TenantPixSettings> PixSettings => Set<TenantPixSettings>();
    public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AppUser>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        model.Entity<AppUser>().Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        model.Entity<Customer>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<Vehicle>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<WashService>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<Vehicle>().HasIndex(x => new { x.TenantId, x.Plate }).IsUnique();
        model.Entity<Vehicle>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.TenantId, x.VehicleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasOne<WashService>().WithMany().HasForeignKey(x => new { x.TenantId, x.ServiceId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrder>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<WashOrderItem>().HasOne<WashOrder>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrderItem>().HasOne<WashService>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ServiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WashOrderItem>().HasIndex(x => new { x.TenantId, x.OrderId, x.ServiceId }).IsUnique();
        model.Entity<WashOrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        model.Entity<WashOrderItem>().Property(x => x.Total).HasPrecision(18, 2);
        model.Entity<OrderPayment>().HasOne<WashOrder>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderPayment>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        model.Entity<OrderPayment>().HasIndex(x => new { x.TenantId, x.OrderId });
        model.Entity<OrderPayment>().Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        model.Entity<OrderPayment>().Property(x => x.Amount).HasPrecision(18, 2);
        model.Entity<OrderPayment>().Property(x => x.Tendered).HasPrecision(18, 2);
        model.Entity<CashSession>().HasIndex(x => x.TenantId).IsUnique().HasFilter("\\"ClosedAt\\" IS NULL");
        model.Entity<CashSession>().Property(x => x.OpeningFloat).HasPrecision(18,2);
        model.Entity<CashSession>().Property(x => x.CountedCash).HasPrecision(18,2);
        model.Entity<CashSession>().Property(x => x.ExpectedCash).HasPrecision(18,2);
        model.Entity<CashMovement>().HasOne<CashSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.SessionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CashSession>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<CashMovement>().Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        model.Entity<CashMovement>().Property(x => x.Amount).HasPrecision(18,2);
        model.Entity<TenantPixSettings>().HasIndex(x => x.TenantId).IsUnique();
        model.Entity<PaymentRefund>().HasOne<OrderPayment>().WithMany().HasForeignKey(x => new { x.TenantId, x.PaymentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderPayment>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<PaymentRefund>().Property(x => x.Amount).HasPrecision(18,2);
        model.Entity<BankReconciliation>().HasOne<OrderPayment>().WithMany().HasForeignKey(x => new { x.TenantId, x.PaymentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BankReconciliation>().HasIndex(x => new { x.TenantId, x.PaymentId }).IsUnique();
        model.Entity<BankReconciliation>().HasIndex(x => new { x.TenantId, x.BankReference }).IsUnique();
        model.Entity<WashOrder>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        foreach (var entity in model.Model.GetEntityTypes().Where(x => typeof(ITenantEntity).IsAssignableFrom(x.ClrType)))
            model.Entity(entity.ClrType).HasOne(typeof(Tenant)).WithMany().HasForeignKey(nameof(ITenantEntity.TenantId)).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<Product>().Property(x => x.Stock).HasPrecision(18, 3);
        model.Entity<WashService>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<WashOrder>().Property(x => x.Total).HasPrecision(18, 2);
    }
}
