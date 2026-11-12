namespace ATWash.Api;

public sealed class CashSession : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OpenedBy { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal OpeningFloat { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public string? ClosingNote { get; set; }
}
public enum CashMovementKind { Supply, Withdrawal }
public sealed class CashMovement : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public CashMovementKind Kind { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class TenantPixSettings : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string PixKey { get; set; }
    public required string MerchantName { get; set; }
    public required string MerchantCity { get; set; }
}
public sealed class PaymentRefund : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public required string ExternalReference { get; set; }
    public Guid ConfirmedBy { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class BankReconciliation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public required string BankReference { get; set; }
    public DateTimeOffset ReconciledAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ReconciledBy { get; set; }
}
