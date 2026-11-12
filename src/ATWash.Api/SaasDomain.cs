namespace ATWash.Api;
public sealed class RegisteredDevice : ITenantEntity {
 public Guid Id {get;set;}=Guid.NewGuid();
 public Guid TenantId {get;set;}
 public Guid UserId {get;set;}
 public required string Name {get;set;}
 public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
 public DateTimeOffset? RevokedAt {get;set;}
}
public sealed class SubscriptionPlan {
 public Guid Id {get;set;}=Guid.NewGuid();
 public required string Name {get;set;}
 public decimal MonthlyPrice {get;set;}
 public int MaxDevices {get;set;}
 public bool IsActive {get;set;}=true;
}
public sealed class TenantSubscription : ITenantEntity {
 public Guid Id {get;set;}=Guid.NewGuid();
 public Guid TenantId {get;set;}
 public Guid PlanId {get;set;}
 public required string Status {get;set;}
 public DateTimeOffset StartsAt {get;set;}=DateTimeOffset.UtcNow;
 public DateTimeOffset? ExpiresAt {get;set;}
}
