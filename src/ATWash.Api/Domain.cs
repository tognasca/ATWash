namespace ATWash.Api;

public interface ITenantEntity { Guid TenantId { get; set; } }
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsDemo { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class Branch : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
}
public sealed class Customer : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Phone { get; set; }
}
public sealed class Vehicle : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public required string Plate { get; set; }
    public required string Model { get; set; }
    public string? Brand { get; set; }
    public string? Color { get; set; }
}
public sealed class WashService : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
public sealed class Product : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal AverageCost { get; set; }
    public string Unit { get; set; } = "un";
    public bool IsSupply { get; set; }
    public Guid? SupplierId { get; set; }
}
public sealed class Employee : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Role { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal CommissionPercent { get; set; }
    public Guid? UserId { get; set; }
}
public enum OrderStatus { Waiting, Washing, Finishing, Ready, Delivered, Cancelled }
public enum PaymentStatus { Pending, PartiallyPaid, Paid, Refunded }
public sealed class WashOrder : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Number { get; set; }
    public Guid VehicleId { get; set; }
    public Guid ServiceId { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Total { get; set; }
    public Guid? EmployeeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WashOrderItem : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ServiceId { get; set; }
    public required string ServiceName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Total { get; set; }
}

public enum PaymentMethod { Cash, Card, Pix }
public sealed class OrderPayment : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OperationId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public decimal Tendered { get; set; }
    public string? Reference { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
}
