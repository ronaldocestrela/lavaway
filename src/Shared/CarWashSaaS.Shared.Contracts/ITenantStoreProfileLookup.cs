namespace CarWashSaaS.Shared.Contracts;

public interface ITenantStoreProfileLookup
{
    Task<Result<StoreProfileDto>> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
}

