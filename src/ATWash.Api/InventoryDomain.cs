namespace ATWash.Api;
public enum StockMovementKind { Opening, Purchase, Sale, Consumption, Adjustment, Reversal }
public sealed class StockMovement : ITenantEntity
{
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid TenantId { get; set; }
 public Guid ProductId { get; set; }
 public Guid? OrderId { get; set; }
 public StockMovementKind Kind { get; set; }
 public decimal Quantity { get; set; } // Signed quantity: inbound positive, outbound negative.
 public decimal BalanceAfter { get; set; }
 public required string Reason { get; set; }
 public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class OrderProductItem : ITenantEntity
{
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid TenantId { get; set; }
 public Guid OrderId { get; set; }
 public Guid ProductId { get; set; }
 public required string ProductName { get; set; }
 public decimal Quantity { get; set; }
 public decimal UnitPrice { get; set; }
 public decimal Total { get; set; }
 public bool IsConsumption { get; set; }
}
