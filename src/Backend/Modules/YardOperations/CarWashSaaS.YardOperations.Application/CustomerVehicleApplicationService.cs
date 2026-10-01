using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class CustomerVehicleApplicationService(
    ICustomerVehicleSearchRepository searchRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork)
{
    private const int MaximumSearchLimit = 50;

    public async Task<Result<CustomerVehicleMatchDto>> GetAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<CustomerVehicleMatchDto>("customer.tenant.required", "Tenant is required.");
        }

        var customer = await searchRepository.GetByCustomerIdAsync(tenantId, customerId, ct);
        return customer is null
            ? Failure<CustomerVehicleMatchDto>("customer.not_found", "Customer was not found.", ErrorType.NotFound)
            : Result<CustomerVehicleMatchDto>.Success(customer);
    }

    public async Task<Result<IReadOnlyCollection<CustomerVehicleMatchDto>>> SearchAsync(
        Guid tenantId,
        SearchCustomerVehiclesQuery query,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("customer.tenant.required", "Tenant is required.");
        }

        var hasPlate = !string.IsNullOrWhiteSpace(query.Plate);
        var hasPhone = !string.IsNullOrWhiteSpace(query.Phone);
        if (!hasPlate && !hasPhone)
        {
            return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("customer.search.required", "Plate or phone is required.");
        }

        if (query.Limit is < 1 or > MaximumSearchLimit)
        {
            return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("customer.search.limit.invalid", $"Search limit must be between 1 and {MaximumSearchLimit}.");
        }

        string? normalizedPlate = null;
        if (hasPlate)
        {
            normalizedPlate = Vehicle.NormalizePlate(query.Plate);
            if (normalizedPlate.Length != 7)
            {
                return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("vehicle.plate.invalid", "Vehicle plate must contain seven letters or digits.");
            }
        }

        string? normalizedPhone = null;
        if (hasPhone)
        {
            normalizedPhone = Customer.NormalizePhone(query.Phone);
            if (normalizedPhone.Length == 0)
            {
                return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("customer.phone.invalid", "A customer phone number must contain digits.");
            }
        }

        var matches = await searchRepository.SearchAsync(tenantId, normalizedPlate, normalizedPhone, query.Limit, ct);
        return Result<IReadOnlyCollection<CustomerVehicleMatchDto>>.Success(matches);
    }

    public async Task<Result<CustomerVehicleMatchDto>> CreateAsync(
        Guid tenantId,
        CreateCustomerWithVehicleCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<CustomerVehicleMatchDto>("customer.tenant.required", "Tenant is required.");
        }

        var customerResult = Customer.Create(tenantId, command.Name, command.Phone);
        if (!customerResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(customerResult.Error!);
        }

        var vehicleResult = Vehicle.Create(tenantId, customerResult.Value!.Id, command.Plate, command.Size);
        if (!vehicleResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(vehicleResult.Error!);
        }

        if (await vehicleRepository.IsPlateRegisteredAsync(tenantId, vehicleResult.Value!.Plate, ct))
        {
            return Failure<CustomerVehicleMatchDto>("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict);
        }

        await customerRepository.AddAsync(customerResult.Value, ct);
        await vehicleRepository.AddAsync(vehicleResult.Value, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        return Result<CustomerVehicleMatchDto>.Success(ToMatch(customerResult.Value, vehicleResult.Value));
    }

    public async Task<Result<CustomerVehicleMatchDto>> AddVehicleAsync(
        Guid tenantId,
        Guid customerId,
        AddVehicleToCustomerCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<CustomerVehicleMatchDto>("customer.tenant.required", "Tenant is required.");
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, customerId, ct);
        if (customer is null)
        {
            return Failure<CustomerVehicleMatchDto>("customer.not_found", "Customer was not found.", ErrorType.NotFound);
        }

        var vehicleResult = Vehicle.Create(tenantId, customer.Id, command.Plate, command.Size);
        if (!vehicleResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(vehicleResult.Error!);
        }

        if (await vehicleRepository.IsPlateRegisteredAsync(tenantId, vehicleResult.Value!.Plate, ct))
        {
            return Failure<CustomerVehicleMatchDto>("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict);
        }

        await vehicleRepository.AddAsync(vehicleResult.Value, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        var updatedCustomer = await searchRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);
        return updatedCustomer is null
            ? Result<CustomerVehicleMatchDto>.Success(ToMatch(customer, vehicleResult.Value))
            : Result<CustomerVehicleMatchDto>.Success(updatedCustomer);
    }

    private static CustomerVehicleMatchDto ToMatch(Customer customer, Vehicle vehicle) => new(
        customer.Id,
        customer.Name,
        customer.Phone,
        [new VehicleSummaryDto(vehicle.Id, vehicle.Plate, vehicle.Size.ToString())]);

    private static Result<T> Failure<T>(string code, string description, ErrorType type = ErrorType.Validation) =>
        Result<T>.Failure(new Error(code, description, type));
}