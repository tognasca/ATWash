namespace ATWash.Api;
public sealed class Supplier : ITenantEntity {
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid TenantId {get;set;}
 public required string Name {get;set;} public string? Phone {get;set;} public string? TaxId {get;set;}
 public bool IsActive {get;set;}=true;
}
public sealed class ServiceIngredient : ITenantEntity {
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid TenantId {get;set;}
 public Guid ServiceId {get;set;} public Guid ProductId {get;set;}
 public decimal Quantity {get;set;}
}
public sealed class ServiceExecution : ITenantEntity {
 public Guid Id {get;set;}=Guid.NewGuid(); public Guid TenantId {get;set;}
 public Guid OrderId {get;set;} public Guid ServiceId {get;set;}
 public Guid? EmployeeId {get;set;} public DateTimeOffset ExecutedAt {get;set;}=DateTimeOffset.UtcNow;
}
