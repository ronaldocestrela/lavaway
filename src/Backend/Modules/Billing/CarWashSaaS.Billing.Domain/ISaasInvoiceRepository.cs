namespace CarWashSaaS.Billing.Domain;

public interface ISaasInvoiceRepository
{
    Task<SaasInvoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SaasInvoice?> GetByGatewayInvoiceIdAsync(string gatewayInvoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<SaasInvoice>> ListByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<SaasInvoice?> GetPendingByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(SaasInvoice invoice, CancellationToken ct = default);
    void Update(SaasInvoice invoice);
    Task SaveChangesAsync(CancellationToken ct = default);
}
