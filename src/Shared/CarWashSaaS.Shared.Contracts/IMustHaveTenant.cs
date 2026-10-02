namespace CarWashSaaS.Shared.Contracts;

public interface IMustHaveTenant
{
    Guid TenantId { get; set; }
}
