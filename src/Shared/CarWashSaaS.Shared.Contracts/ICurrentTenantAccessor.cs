namespace CarWashSaaS.Shared.Contracts;

public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; }
}