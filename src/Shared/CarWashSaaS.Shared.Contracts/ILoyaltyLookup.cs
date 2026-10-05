namespace CarWashSaaS.Shared.Contracts;

public interface ILoyaltyLookup
{
    Task<Result<CustomerLoyaltySummaryDto?>> GetLoyaltySummaryByPhoneAsync(Guid tenantId, string phone, CancellationToken ct = default);
    Task<Result<CustomerLoyaltySummaryDto?>> GetLoyaltySummaryByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
}
