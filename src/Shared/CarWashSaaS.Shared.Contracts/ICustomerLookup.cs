namespace CarWashSaaS.Shared.Contracts;

public interface ICustomerLookup
{
    Task<Result<CustomerVehicleMatchDto?>> GetByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default);

    Task<Result<CustomerVehicleMatchDto>> RegisterCustomerAsync(
        Guid tenantId,
        string name,
        string phone,
        CancellationToken ct = default);

    Task<Result<VehicleSummaryDto>> RegisterVehicleForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        string plate,
        string size,
        CancellationToken ct = default);
}
