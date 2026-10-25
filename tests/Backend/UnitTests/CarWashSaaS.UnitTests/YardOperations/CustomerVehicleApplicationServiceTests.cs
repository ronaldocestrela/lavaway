using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerVehicleApplicationServiceTests
{
    [Fact]
    public async Task SearchAsync_ShouldNormalizePlateAndPhoneBeforeSearching()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.SearchAsync(
            Guid.CreateVersion7(),
            new SearchCustomerVehiclesQuery("abc-1d23", "+55 (11) 99999-9999"));

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC1D23", repository.SearchedPlate);
        Assert.Equal("11999999999", repository.SearchedPhone);
    }

    [Fact]
    public async Task CreateAsync_ShouldAddCustomerAndVehicleAndSaveOnce()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Maria Silva", "+55 (11) 99999-9999", "abc-1d23", VehicleSize.HatchSedan));

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Customers);
        Assert.Single(repository.Vehicles);
        Assert.Equal(1, repository.SaveChangesCount);
        Assert.Equal(tenantId, repository.Customers[0].TenantId);
        Assert.Equal(tenantId, repository.Vehicles[0].TenantId);
        Assert.Equal(repository.Customers[0].Id, repository.Vehicles[0].CustomerId);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicatePlateWithoutSaving()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        repository.Vehicles.Add(Vehicle.Create(tenantId, Guid.CreateVersion7(), "ABC1D23", VehicleSize.Suv).Value!);
        var service = CreateService(repository);

        var result = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Maria Silva", "5511999999999", "abc-1d23", VehicleSize.HatchSedan));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Empty(repository.Customers);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AddVehicleAsync_ShouldReturnAllVehiclesForTheCustomer()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!);
        var service = CreateService(repository);

        var result = await service.AddVehicleAsync(tenantId, customer.Id,
            new AddVehicleToCustomerCommand("XYZ9Z99", VehicleSize.Suv));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Vehicles.Count);
        Assert.Contains(result.Value.Vehicles, vehicle => vehicle.Plate == "ABC1D23");
        Assert.Contains(result.Value.Vehicles, vehicle => vehicle.Plate == "XYZ9Z99");
    }

    [Fact]
    public async Task UpdateCustomerAsync_ShouldUpdateCustomerAndSave()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        var service = CreateService(repository);

        var result = await service.UpdateCustomerAsync(tenantId, customer.Id,
            new UpdateCustomerCommand("Maria dos Santos", "11 98888-7777"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Maria dos Santos", result.Value!.CustomerName);
        Assert.Equal("11 98888-7777", result.Value.Phone);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateCustomerAsync_ShouldReturnNotFound_WhenCustomerDoesNotExist()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.UpdateCustomerAsync(tenantId, Guid.CreateVersion7(),
            new UpdateCustomerCommand("Maria dos Santos", "11 98888-7777"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateVehicleAsync_ShouldUpdateVehicleAndSave()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(vehicle);
        var service = CreateService(repository);

        var result = await service.UpdateVehicleAsync(tenantId, customer.Id, vehicle.Id,
            new UpdateVehicleCommand("XYZ9Z99", VehicleSize.Suv));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Vehicles);
        Assert.Equal("XYZ9Z99", result.Value.Vehicles.Single().Plate);
        Assert.Equal(VehicleSize.Suv.ToString(), result.Value.Vehicles.Single().Size);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateVehicleAsync_ShouldRejectDuplicatePlateFromAnotherVehicle()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var vehicle1 = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var vehicle2 = Vehicle.Create(tenantId, customer.Id, "XYZ9Z99", VehicleSize.Suv).Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(vehicle1);
        repository.Vehicles.Add(vehicle2);
        var service = CreateService(repository);

        var result = await service.UpdateVehicleAsync(tenantId, customer.Id, vehicle1.Id,
            new UpdateVehicleCommand("XYZ9Z99", VehicleSize.HatchSedan));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateVehicleAsync_ShouldAllowSamePlateOnSameVehicle()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Maria Silva", "5511999999999").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(vehicle);
        var service = CreateService(repository);

        var result = await service.UpdateVehicleAsync(tenantId, customer.Id, vehicle.Id,
            new UpdateVehicleCommand("abc-1d23", VehicleSize.Suv));

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC1D23", result.Value!.Vehicles.Single().Plate);
        Assert.Equal(VehicleSize.Suv.ToString(), result.Value.Vehicles.Single().Size);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AnonymizeCustomerAsync_Should_AnonymizeCustomer_DisassociateVehicles_ClearWhatsAppPreferences_AndRecordAudit()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Eduardo", "11988887777").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "ABC1D23", VehicleSize.HatchSedan).Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        repository.Vehicles.Add(vehicle);

        var fakePreference = new FakePreferenceLookup();
        var fakeAudit = new FakeAuditTrailService();
        var service = new CustomerVehicleApplicationService(repository, repository, repository, repository, fakePreference, fakeAudit);

        var operatorId = Guid.CreateVersion7();
        var command = new AnonymizeCustomerCommand(
            customer.Id,
            operatorId,
            "admin@lavaway.com.br",
            "Direito ao esquecimento solicitado pelo titular via canal do DPO.");

        var result = await service.AnonymizeCustomerAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value!.CustomerId);
        Assert.Equal(Customer.AnonymizedDefaultName, result.Value.AnonymizedName);
        Assert.Equal(1, result.Value.VehiclesDisassociatedCount);

        // Verify Customer state
        Assert.True(customer.IsAnonymized);
        Assert.Equal(Customer.AnonymizedDefaultName, customer.Name);
        Assert.Equal(string.Empty, customer.Phone);
        Assert.Equal(32, customer.NormalizedPhone.Length);

        // Verify Vehicle disassociated
        Assert.Null(vehicle.CustomerId);

        // Verify WhatsApp cleared
        Assert.Equal(tenantId, fakePreference.LastClearedTenantId);
        Assert.Equal("11988887777", fakePreference.LastClearedPhone);

        // Verify Audit Event recorded
        Assert.Single(fakeAudit.RecordedEvents);
        var audit = fakeAudit.RecordedEvents[0];
        Assert.Equal(operatorId, audit.ActorId);
        Assert.Equal("admin@lavaway.com.br", audit.ActorEmail);
        Assert.Equal(PlatformActionConstants.CustomerAnonymized, audit.Action);
        Assert.Equal(customer.Id.ToString(), audit.TargetId);

        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AnonymizeCustomerAsync_Should_ReturnNotFound_WhenCustomerDoesNotExist()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var result = await service.AnonymizeCustomerAsync(tenantId, new AnonymizeCustomerCommand(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "admin@lavaway.com.br",
            "Direito ao esquecimento"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task AnonymizeCustomerAsync_Should_ReturnConflict_WhenCustomerIsAlreadyAnonymized()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Eduardo", "11988887777").Value!;
        customer.Anonymize("abcdef0123456789abcdef0123456789");
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        var service = CreateService(repository);

        var result = await service.AnonymizeCustomerAsync(tenantId, new AnonymizeCustomerCommand(
            customer.Id,
            Guid.CreateVersion7(),
            "admin@lavaway.com.br",
            "Solicitação repetida"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("customer.already_anonymized", result.Error.Code);
    }

    [Fact]
    public async Task AnonymizeCustomerAsync_Should_ReturnValidationError_WhenReasonIsMissing()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Carlos Eduardo", "11988887777").Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);
        var service = CreateService(repository);

        var result = await service.AnonymizeCustomerAsync(tenantId, new AnonymizeCustomerCommand(
            customer.Id,
            Guid.CreateVersion7(),
            "admin@lavaway.com.br",
            "   "));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("customer.anonymize.reason_required", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Should_ReassignDisassociatedVehicle_WhenPlateExistsWithoutActiveOwner()
    {
        var tenantId = Guid.CreateVersion7();
        var repository = new FakeRepository();
        // Existing vehicle disassociated from previous owner
        var existingVehicle = Vehicle.Create(tenantId, Guid.CreateVersion7(), "ABC1D23", VehicleSize.Suv).Value!;
        existingVehicle.DisassociateCustomer();
        repository.Vehicles.Add(existingVehicle);

        var service = CreateService(repository);

        // New customer registers with the same plate
        var result = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Novo Proprietario", "11977776666", "ABC1D23", VehicleSize.Suv));

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Vehicles);
        Assert.Equal(repository.Customers[0].Id, existingVehicle.CustomerId);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AddVehicleAsync_Should_ReassignDisassociatedVehicle_WhenPlateExistsWithoutActiveOwner()
    {
        var tenantId = Guid.CreateVersion7();
        var customer = Customer.Create(tenantId, "Joao Silva", "11977778888").Value!;
        var repository = new FakeRepository();
        repository.Customers.Add(customer);

        var existingVehicle = Vehicle.Create(tenantId, Guid.CreateVersion7(), "ABC1D23", VehicleSize.HatchSedan).Value!;
        existingVehicle.DisassociateCustomer();
        repository.Vehicles.Add(existingVehicle);

        var service = CreateService(repository);

        var result = await service.AddVehicleAsync(tenantId, customer.Id, new AddVehicleToCustomerCommand("ABC1D23", VehicleSize.HatchSedan));

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Vehicles);
        Assert.Equal(customer.Id, existingVehicle.CustomerId);
    }

    private static CustomerVehicleApplicationService CreateService(FakeRepository repository) =>
        new(repository, repository, repository, repository);

    private sealed class FakePreferenceLookup : ICustomerCommunicationPreferenceLookup
    {
        public Guid? LastClearedTenantId { get; private set; }
        public string? LastClearedPhone { get; private set; }

        public Task<Result<CustomerCommunicationPreferenceDto?>> GetPreferenceAsync(Guid tenantId, string phone, CancellationToken ct = default) =>
            Task.FromResult<Result<CustomerCommunicationPreferenceDto?>>(Result<CustomerCommunicationPreferenceDto?>.Success(null));

        public Task<Result<CustomerCommunicationPreferenceDto>> UpdatePreferenceAsync(Guid tenantId, string phone, bool isOptedIn, string? reason = null, CancellationToken ct = default) =>
            Task.FromResult<Result<CustomerCommunicationPreferenceDto>>(Result<CustomerCommunicationPreferenceDto>.Failure(new Error("not_impl", "Not implemented", ErrorType.Validation)));

        public Task<Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>> ListPreferencesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>.Success([]));

        public Task<Result> ClearPreferencesForPhoneAsync(Guid tenantId, string phone, string? reason = null, CancellationToken ct = default)
        {
            LastClearedTenantId = tenantId;
            LastClearedPhone = phone;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeAuditTrailService : IAuditTrailService
    {
        public List<RecordAuditEventRequest> RecordedEvents { get; } = [];

        public Task<Result<Guid>> RecordEventAsync(RecordAuditEventRequest request, CancellationToken ct = default)
        {
            RecordedEvents.Add(request);
            return Task.FromResult(Result<Guid>.Success(Guid.CreateVersion7()));
        }
    }

    private sealed class FakeRepository : ICustomerVehicleSearchRepository, ICustomerRepository, IVehicleRepository, IUnitOfWork
    {
        public List<Customer> Customers { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public string? SearchedPlate { get; private set; }
        public string? SearchedPhone { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
        {
            var customer = Customers.SingleOrDefault(value => value.TenantId == tenantId && value.Id == customerId);
            if (customer is null)
            {
                return Task.FromResult<CustomerVehicleMatchDto?>(null);
            }

            var vehicles = Vehicles
                .Where(value => value.TenantId == tenantId && value.CustomerId == customerId)
                .Select(value => new VehicleSummaryDto(value.Id, value.Plate, value.Size.ToString()))
                .ToArray();
            return Task.FromResult<CustomerVehicleMatchDto?>(new CustomerVehicleMatchDto(
                customer.Id, customer.Name, customer.Phone, vehicles));
        }

        public Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
            Guid tenantId, string? normalizedPlate, string? normalizedPhone, int limit, CancellationToken ct = default)
        {
            SearchedPlate = normalizedPlate;
            SearchedPhone = normalizedPhone;
            return Task.FromResult<IReadOnlyCollection<CustomerVehicleMatchDto>>([]);
        }

        public Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult(Customers.SingleOrDefault(customer => customer.TenantId == tenantId && customer.Id == customerId));

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<Vehicle?> GetByPlateAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.SingleOrDefault(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate));

        public Task<IReadOnlyCollection<Vehicle>> GetVehiclesByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Vehicle>>(Vehicles.Where(v => v.TenantId == tenantId && v.CustomerId == customerId).ToList());

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate && vehicle.CustomerId != null));

        public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, Guid? excludeVehicleId, CancellationToken ct = default) =>
            Task.FromResult(Vehicles.Any(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate && vehicle.CustomerId != null && (!excludeVehicleId.HasValue || vehicle.Id != excludeVehicleId.Value)));

        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid tenantId, Guid vehicleId, CancellationToken ct) =>
            Task.FromResult(Vehicles.SingleOrDefault(vehicle => vehicle.TenantId == tenantId && vehicle.Id == vehicleId));

        public Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCount++;
            return Task.FromResult(Result<int>.Success(1));
        }
    }
}
