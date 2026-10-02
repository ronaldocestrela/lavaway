using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public interface ICustomerVehicleSearchRepository
{
    Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
        Guid tenantId,
        string? normalizedPlate,
        string? normalizedPhone,
        int limit,
        CancellationToken ct = default);
}
