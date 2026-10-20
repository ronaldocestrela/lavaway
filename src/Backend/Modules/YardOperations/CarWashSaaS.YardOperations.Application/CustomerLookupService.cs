using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class CustomerLookupService(
    ICustomerVehicleSearchRepository searchRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : ICustomerLookup
{
    public async Task<Result<CustomerVehicleMatchDto?>> GetByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerVehicleMatchDto?>.Failure(new Error("customer.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedPhone = Customer.NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return Result<CustomerVehicleMatchDto?>.Success(null);
        }

        var matches = await searchRepository.SearchAsync(tenantId, null, normalizedPhone, 1, null, ct);
        var match = matches.FirstOrDefault();
        return Result<CustomerVehicleMatchDto?>.Success(match);
    }

    public async Task<Result<CustomerVehicleMatchDto>> RegisterCustomerAsync(
        Guid tenantId,
        string name,
        string phone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerVehicleMatchDto>.Failure(new Error("customer.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedPhone = Customer.NormalizePhone(phone);
        var existingMatches = await searchRepository.SearchAsync(tenantId, null, normalizedPhone, 1, null, ct);
        var existing = existingMatches.FirstOrDefault();
        if (existing is not null)
        {
            return Result<CustomerVehicleMatchDto>.Success(existing);
        }

        var customerResult = Customer.Create(tenantId, name, phone);
        if (!customerResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(customerResult.Error!);
        }

        var customer = customerResult.Value!;
        await customerRepository.AddAsync(customer, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        return Result<CustomerVehicleMatchDto>.Success(new CustomerVehicleMatchDto(
            customer.Id,
            customer.Name,
            customer.Phone,
            []));
    }

    public async Task<Result<VehicleSummaryDto>> RegisterVehicleForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        string plate,
        string size,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty)
        {
            return Result<VehicleSummaryDto>.Failure(new Error("vehicle.tenant_customer.required", "Tenant and customer are required.", ErrorType.Validation));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, customerId, ct);
        if (customer is null)
        {
            return Result<VehicleSummaryDto>.Failure(new Error("customer.not_found", "Customer was not found.", ErrorType.NotFound));
        }

        var normalizedPlate = Vehicle.NormalizePlate(plate);
        if (await vehicleRepository.IsPlateRegisteredAsync(tenantId, normalizedPlate, ct))
        {
            var customerWithVehicles = await searchRepository.GetByCustomerIdAsync(tenantId, customerId, ct);
            var existingVehicle = customerWithVehicles?.Vehicles.FirstOrDefault(v => v.Plate.Equals(normalizedPlate, StringComparison.OrdinalIgnoreCase));
            if (existingVehicle is not null)
            {
                return Result<VehicleSummaryDto>.Success(existingVehicle);
            }

            return Result<VehicleSummaryDto>.Failure(new Error("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict));
        }

        var parsedSize = ParseVehicleSize(size);
        var vehicleResult = Vehicle.Create(tenantId, customerId, normalizedPlate, parsedSize);
        if (!vehicleResult.IsSuccess)
        {
            return Result<VehicleSummaryDto>.Failure(vehicleResult.Error!);
        }

        var vehicle = vehicleResult.Value!;
        await vehicleRepository.AddAsync(vehicle, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<VehicleSummaryDto>.Failure(saveResult.Error!);
        }

        return Result<VehicleSummaryDto>.Success(new VehicleSummaryDto(
            vehicle.Id,
            vehicle.Plate,
            vehicle.Size.ToString()));
    }

    private static VehicleSize ParseVehicleSize(string? size) => size?.Trim() switch
    {
        VehicleSizeConstants.Suv or "Suv" => VehicleSize.Suv,
        VehicleSizeConstants.PickupVan or "PickupVan" => VehicleSize.PickupVan,
        VehicleSizeConstants.Motorcycle or "Motorcycle" => VehicleSize.Motorcycle,
        _ => VehicleSize.HatchSedan
    };
}
