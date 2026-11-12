using Microsoft.EntityFrameworkCore;
namespace ATWash.Api;
public sealed class AtWashDbContext(DbContextOptions<AtWashDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredDevice> RegisteredDevices => Set<RegisteredDevice>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WashService> Services => Set<WashService>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<OrderProductItem> OrderProductItems => Set<OrderProductItem>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ServiceIngredient> ServiceIngredients => Set<ServiceIngredient>();
    public DbSet<ServiceExecution> ServiceExecutions => Set<ServiceExecution>();
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
        model.Entity<RegisteredDevice>().HasIndex(x=>new{x.TenantId,x.UserId});
        model.Entity<RegisteredDevice>().HasOne<AppUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SubscriptionPlan>().Property(x=>x.MonthlyPrice).HasPrecision(18,2);
        model.Entity<TenantSubscription>().HasIndex(x=>x.TenantId).IsUnique();
        model.Entity<TenantSubscription>().HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x=>x.PlanId).OnDelete(DeleteBehavior.Restrict);
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
        model.Entity<CashSession>().HasIndex(x => x.TenantId).IsUnique().HasFilter("\u0022ClosedAt\u0022 IS NULL");
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
        model.Entity<Employee>().HasAlternateKey(x=>new{x.TenantId,x.Id});
        model.Entity<Employee>().HasIndex(x=>new{x.TenantId,x.UserId}).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        model.Entity<Employee>().Property(x=>x.CommissionPercent).HasPrecision(5,2);
        model.Entity<WashOrder>().HasOne<Employee>().WithMany().HasForeignKey(x=>new{x.TenantId,x.EmployeeId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Supplier>().HasAlternateKey(x=>new{x.TenantId,x.Id});
        model.Entity<Product>().HasOne<Supplier>().WithMany().HasForeignKey(x=>new{x.TenantId,x.SupplierId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Product>().Property(x=>x.MinimumStock).HasPrecision(18,3);
        model.Entity<Product>().Property(x=>x.AverageCost).HasPrecision(18,2);
        model.Entity<ServiceIngredient>().HasOne<WashService>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ServiceId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceIngredient>().HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceIngredient>().HasIndex(x=>new{x.TenantId,x.ServiceId,x.ProductId}).IsUnique();
        model.Entity<ServiceIngredient>().Property(x=>x.Quantity).HasPrecision(18,3);
        model.Entity<ServiceExecution>().HasOne<WashOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.OrderId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceExecution>().HasOne<WashService>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ServiceId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceExecution>().HasOne<Employee>().WithMany().HasForeignKey(x=>new{x.TenantId,x.EmployeeId})
            .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceExecution>().HasIndex(x=>new{x.TenantId,x.OrderId,x.ServiceId}).IsUnique();
        model.Entity<Product>().HasAlternateKey(x => new { x.TenantId, x.Id });
        model.Entity<StockMovement>().HasOne<Product>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockMovement>().HasOne<WashOrder>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockMovement>().HasIndex(x => new { x.TenantId, x.ProductId, x.CreatedAt });
        model.Entity<StockMovement>().Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
        model.Entity<StockMovement>().Property(x => x.Quantity).HasPrecision(18, 3);
        model.Entity<StockMovement>().Property(x => x.BalanceAfter).HasPrecision(18, 3);
        model.Entity<OrderProductItem>().HasOne<Product>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderProductItem>().HasOne<WashOrder>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderProductItem>().HasIndex(x => new { x.TenantId, x.OrderId });
        model.Entity<OrderProductItem>().Property(x => x.Quantity).HasPrecision(18, 3);
        model.Entity<OrderProductItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        model.Entity<OrderProductItem>().Property(x => x.Total).HasPrecision(18, 2);
        model.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<Product>().Property(x => x.Stock).HasPrecision(18, 3);
        model.Entity<WashService>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<WashOrder>().Property(x => x.Total).HasPrecision(18, 2);
    }
}
