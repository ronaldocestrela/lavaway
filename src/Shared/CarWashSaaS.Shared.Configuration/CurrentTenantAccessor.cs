using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Shared.Configuration;

public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}