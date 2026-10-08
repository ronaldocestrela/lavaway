namespace CarWashSaaS.Billing.Application;

public interface ITenantPixGatewayResolver
{
    Task<IPixGatewayProvider> ResolveForTenantAsync(Guid tenantId, CancellationToken ct = default);
}
