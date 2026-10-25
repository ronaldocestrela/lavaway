using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class CustomerVehicleApplicationService(
    ICustomerVehicleSearchRepository searchRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    ICustomerCommunicationPreferenceLookup? preferenceLookup = null,
    IAuditTrailService? auditTrailService = null)
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

        if (query.Limit is < 1 or > MaximumSearchLimit)
        {
            return Failure<IReadOnlyCollection<CustomerVehicleMatchDto>>("customer.search.limit.invalid", $"Search limit must be between 1 and {MaximumSearchLimit}.");
        }

        var hasPlate = !string.IsNullOrWhiteSpace(query.Plate);
        var hasPhone = !string.IsNullOrWhiteSpace(query.Phone);

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

        var matches = await searchRepository.SearchAsync(tenantId, normalizedPlate, normalizedPhone, query.Limit, query.Query, ct);
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

        var normalizedPlate = Vehicle.NormalizePlate(command.Plate);
        var existingVehicle = await vehicleRepository.GetByPlateAsync(tenantId, normalizedPlate, ct);
        Vehicle vehicleToPersist;

        if (existingVehicle is not null)
        {
            if (existingVehicle.CustomerId.HasValue)
            {
                return Failure<CustomerVehicleMatchDto>("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict);
            }

            existingVehicle.ReassignCustomer(customerResult.Value!.Id);
            existingVehicle.Update(command.Plate, command.Size);
            vehicleToPersist = existingVehicle;
        }
        else
        {
            var vehicleResult = Vehicle.Create(tenantId, customerResult.Value!.Id, command.Plate, command.Size);
            if (!vehicleResult.IsSuccess)
            {
                return Result<CustomerVehicleMatchDto>.Failure(vehicleResult.Error!);
            }

            vehicleToPersist = vehicleResult.Value!;
            await vehicleRepository.AddAsync(vehicleToPersist, ct);
        }

        await customerRepository.AddAsync(customerResult.Value, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        return Result<CustomerVehicleMatchDto>.Success(ToMatch(customerResult.Value, vehicleToPersist));
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

        var normalizedPlate = Vehicle.NormalizePlate(command.Plate);
        var existingVehicle = await vehicleRepository.GetByPlateAsync(tenantId, normalizedPlate, ct);
        Vehicle vehicleToPersist;

        if (existingVehicle is not null)
        {
            if (existingVehicle.CustomerId.HasValue)
            {
                return Failure<CustomerVehicleMatchDto>("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict);
            }

            existingVehicle.ReassignCustomer(customer.Id);
            existingVehicle.Update(command.Plate, command.Size);
            vehicleToPersist = existingVehicle;
        }
        else
        {
            var vehicleResult = Vehicle.Create(tenantId, customer.Id, command.Plate, command.Size);
            if (!vehicleResult.IsSuccess)
            {
                return Result<CustomerVehicleMatchDto>.Failure(vehicleResult.Error!);
            }

            vehicleToPersist = vehicleResult.Value!;
            await vehicleRepository.AddAsync(vehicleToPersist, ct);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        var updatedCustomer = await searchRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);
        return updatedCustomer is null
            ? Result<CustomerVehicleMatchDto>.Success(ToMatch(customer, vehicleToPersist))
            : Result<CustomerVehicleMatchDto>.Success(updatedCustomer);
    }

    public async Task<Result<CustomerVehicleMatchDto>> UpdateCustomerAsync(
        Guid tenantId,
        Guid customerId,
        UpdateCustomerCommand command,
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

        var updateResult = customer.Update(command.Name, command.Phone);
        if (!updateResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(updateResult.Error!);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        var updatedCustomer = await searchRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);
        return updatedCustomer is null
            ? Failure<CustomerVehicleMatchDto>("customer.not_found", "Customer was not found.", ErrorType.NotFound)
            : Result<CustomerVehicleMatchDto>.Success(updatedCustomer);
    }

    public async Task<Result<CustomerVehicleMatchDto>> UpdateVehicleAsync(
        Guid tenantId,
        Guid customerId,
        Guid vehicleId,
        UpdateVehicleCommand command,
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

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, vehicleId, ct);
        if (vehicle is null || vehicle.CustomerId != customerId)
        {
            return Failure<CustomerVehicleMatchDto>("vehicle.not_found", "Vehicle was not found.", ErrorType.NotFound);
        }

        var normalizedPlate = Vehicle.NormalizePlate(command.Plate);
        if (await vehicleRepository.IsPlateRegisteredAsync(tenantId, normalizedPlate, vehicleId, ct))
        {
            return Failure<CustomerVehicleMatchDto>("vehicle.plate.duplicate", "A vehicle with this plate already exists for this tenant.", ErrorType.Conflict);
        }

        var updateResult = vehicle.Update(command.Plate, command.Size);
        if (!updateResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(updateResult.Error!);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerVehicleMatchDto>.Failure(saveResult.Error!);
        }

        var updatedCustomer = await searchRepository.GetByCustomerIdAsync(tenantId, customer.Id, ct);
        return updatedCustomer is null
            ? Failure<CustomerVehicleMatchDto>("customer.not_found", "Customer was not found.", ErrorType.NotFound)
            : Result<CustomerVehicleMatchDto>.Success(updatedCustomer);
    }

    public async Task<Result<CustomerAnonymizedDto>> AnonymizeCustomerAsync(
        Guid tenantId,
        AnonymizeCustomerCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<CustomerAnonymizedDto>("customer.tenant.required", "Tenant is required.");
        }

        if (command.CustomerId == Guid.Empty)
        {
            return Failure<CustomerAnonymizedDto>("customer.id.required", "Customer ID is required.");
        }

        if (command.OperatorId == Guid.Empty)
        {
            return Failure<CustomerAnonymizedDto>("customer.anonymize.operator_required", "Operator ID is required for audit.");
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Failure<CustomerAnonymizedDto>("customer.anonymize.reason_required", "A formal reason is required for data anonymization.");
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, command.CustomerId, ct);
        if (customer is null)
        {
            return Failure<CustomerAnonymizedDto>("customer.not_found", "Customer was not found.", ErrorType.NotFound);
        }

        if (customer.IsAnonymized)
        {
            return Failure<CustomerAnonymizedDto>("customer.already_anonymized", "Customer has already been anonymized.", ErrorType.Conflict);
        }

        var originalPhone = customer.Phone;
        var originalNormalizedPhone = customer.NormalizedPhone;

        var salt = Guid.NewGuid().ToString("N");
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{originalNormalizedPhone}:{salt}"));
        var phoneHash = Convert.ToHexString(hashBytes)[..32];

        var anonymizeResult = customer.Anonymize(phoneHash);
        if (!anonymizeResult.IsSuccess)
        {
            return Result<CustomerAnonymizedDto>.Failure(anonymizeResult.Error!);
        }

        var vehicles = await vehicleRepository.GetVehiclesByCustomerIdAsync(tenantId, customer.Id, ct);
        foreach (var vehicle in vehicles)
        {
            vehicle.DisassociateCustomer();
        }

        if (preferenceLookup is not null && !string.IsNullOrWhiteSpace(originalPhone))
        {
            await preferenceLookup.ClearPreferencesForPhoneAsync(
                tenantId,
                originalPhone,
                $"Anonimização LGPD solicitada por operador {command.OperatorEmail}. Motivo: {command.Reason.Trim()}",
                ct);
        }

        if (auditTrailService is not null)
        {
            var auditDetails = JsonSerializer.Serialize(new
            {
                Reason = command.Reason.Trim(),
                VehiclesDisassociated = vehicles.Count,
                AnonymizedAtUtc = customer.AnonymizedAtUtc
            });

            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                command.OperatorId,
                string.IsNullOrWhiteSpace(command.OperatorEmail) ? "system@lavaway.com.br" : command.OperatorEmail,
                "Administrator",
                "Shop",
                PlatformActionConstants.CustomerAnonymized,
                PlatformTargetTypeConstants.Customer,
                customer.Id.ToString(),
                tenantId,
                command.IpAddress,
                command.UserAgent,
                auditDetails),
                ct);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CustomerAnonymizedDto>.Failure(saveResult.Error!);
        }

        return Result<CustomerAnonymizedDto>.Success(new CustomerAnonymizedDto(
            customer.Id,
            customer.Name,
            customer.AnonymizedAtUtc ?? DateTimeOffset.UtcNow,
            vehicles.Count));
    }

    private static CustomerVehicleMatchDto ToMatch(Customer customer, Vehicle vehicle) => new(
        customer.Id,
        customer.Name,
        customer.Phone,
        [new VehicleSummaryDto(vehicle.Id, vehicle.Plate, vehicle.Size.ToString())],
        customer.IsAnonymized,
        customer.AnonymizedAtUtc);

    private static Result<T> Failure<T>(string code, string description, ErrorType type = ErrorType.Validation) =>
        Result<T>.Failure(new Error(code, description, type));
}
