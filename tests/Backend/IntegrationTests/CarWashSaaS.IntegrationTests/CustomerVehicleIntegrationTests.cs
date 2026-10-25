using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class CustomerVehicleIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task SearchAsync_ShouldReturnSharedPhoneMatchesAndApplyPlateFilter()
    {
        var tenantId = await CreateTenantAsync("Customer search tenant");
        await using var context = CreateYardContext(tenantId);
        var service = CreateService(context);

        var first = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Maria Silva", "+55 (11) 99999-9999", "abc-1d23", VehicleSize.HatchSedan));
        var second = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
            "Joao Silva", "5511999999999", "XYZ-9Z99", VehicleSize.Suv));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var phoneSearch = await service.SearchAsync(tenantId, new SearchCustomerVehiclesQuery(null, "(11) 99999-9999"));
        var combinedSearch = await service.SearchAsync(tenantId, new SearchCustomerVehiclesQuery("ABC-1D23", "+55 (11) 99999-9999"));

        Assert.True(phoneSearch.IsSuccess);
        Assert.Equal(2, phoneSearch.Value!.Count);
        Assert.All(phoneSearch.Value, match => Assert.Single(match.Vehicles));
        Assert.True(combinedSearch.IsSuccess);
        Assert.Single(combinedSearch.Value!);
        Assert.Equal(first.Value!.CustomerId, combinedSearch.Value!.Single().CustomerId);
    }

    [Fact]
    public async Task TenantB_ShouldNotSearchOrAttachVehiclesToTenantACustomer()
    {
        var tenantA = await CreateTenantAsync("Customer tenant A");
        var tenantB = await CreateTenantAsync("Customer tenant B");

        await using (var tenantAContext = CreateYardContext(tenantA))
        {
            var result = await CreateService(tenantAContext).CreateAsync(tenantA, new CreateCustomerWithVehicleCommand(
                "Tenant A customer", "555-0101", "ABC-1D23", VehicleSize.HatchSedan));
            Assert.True(result.IsSuccess);
        }

        await using var tenantBContext = CreateYardContext(tenantB);
        var tenantBService = CreateService(tenantBContext);

        var search = await tenantBService.SearchAsync(tenantB, new SearchCustomerVehiclesQuery("ABC-1D23", "5550101"));
        var addVehicle = await tenantBService.AddVehicleAsync(
            tenantB,
            (await CreateTenantACustomerIdAsync(tenantA)),
            new AddVehicleToCustomerCommand("DEF-4G56", VehicleSize.Suv));

        Assert.True(search.IsSuccess);
        Assert.Empty(search.Value!);
        Assert.False(addVehicle.IsSuccess);
        Assert.Equal(CarWashSaaS.Shared.Contracts.ErrorType.NotFound, addVehicle.Error!.Type);
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
        var tenant = Tenant.Create(name).Value!;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<Guid> CreateTenantACustomerIdAsync(Guid tenantId)
    {
        await using var context = CreateYardContext(tenantId);
        return (await context.Customers.Select(customer => customer.Id).SingleAsync());
    }

    private YardOperationsDbContext CreateYardContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessor);
    }

    [Fact]
    public async Task CustomerLookupService_ShouldRegisterAndQueryCustomer_AndIsolateBetweenTenants()
    {
        var tenantA = await CreateTenantAsync("Lookup Tenant A");
        var tenantB = await CreateTenantAsync("Lookup Tenant B");

        await using (var tenantAContext = CreateYardContext(tenantA))
        {
            var lookupA = CreateLookupService(tenantAContext);
            var regA = await lookupA.RegisterCustomerAsync(tenantA, "Carlos Teste", "11999990001");
            Assert.True(regA.IsSuccess);

            var regVehicle = await lookupA.RegisterVehicleForCustomerAsync(tenantA, regA.Value!.CustomerId, "ABC-1234", "HatchSedan");
            Assert.True(regVehicle.IsSuccess);

            var queryA = await lookupA.GetByPhoneAsync(tenantA, "11999990001");
            Assert.True(queryA.IsSuccess);
            Assert.NotNull(queryA.Value);
            Assert.Equal("Carlos Teste", queryA.Value!.CustomerName);
            Assert.Single(queryA.Value.Vehicles);
            Assert.Equal("ABC1234", queryA.Value.Vehicles.First().Plate);
        }

        await using var tenantBContext = CreateYardContext(tenantB);
        var lookupB = CreateLookupService(tenantBContext);

        // Tenant B must not see Tenant A customer
        var queryB = await lookupB.GetByPhoneAsync(tenantB, "11999990001");
        Assert.True(queryB.IsSuccess);
        Assert.Null(queryB.Value);
    }

    [Fact]
    public async Task AnonymizeCustomerAsync_ShouldPersistAnonymizationAndDisassociateVehiclesInDatabase()
    {
        var tenantId = await CreateTenantAsync("Anonymize Tenant");
        Guid customerId;

        await using (var context = CreateYardContext(tenantId))
        {
            var service = CreateService(context);
            var created = await service.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
                "Carlos Titular", "11988881234", "LGPD123", VehicleSize.HatchSedan));
            Assert.True(created.IsSuccess);
            customerId = created.Value!.CustomerId;

            var anonymizeCommand = new AnonymizeCustomerCommand(
                customerId,
                Guid.CreateVersion7(),
                "dpo@lavaway.com.br",
                "Solicitação formal do titular Art. 18 LGPD.");

            var anonymized = await service.AnonymizeCustomerAsync(tenantId, anonymizeCommand);
            Assert.True(anonymized.IsSuccess);
            Assert.Equal(Customer.AnonymizedDefaultName, anonymized.Value!.AnonymizedName);
            Assert.Equal(1, anonymized.Value.VehiclesDisassociatedCount);
        }

        // Verify with fresh DbContext
        await using (var verifyContext = CreateYardContext(tenantId))
        {
            var persistedCustomer = await verifyContext.Customers.SingleAsync(c => c.TenantId == tenantId && c.Id == customerId);
            Assert.True(persistedCustomer.IsAnonymized);
            Assert.Equal(Customer.AnonymizedDefaultName, persistedCustomer.Name);
            Assert.Equal(string.Empty, persistedCustomer.Phone);
            Assert.NotNull(persistedCustomer.AnonymizedAtUtc);

            var persistedVehicle = await verifyContext.Vehicles.SingleAsync(v => v.TenantId == tenantId && v.Plate == "LGPD123");
            Assert.Null(persistedVehicle.CustomerId);

            // Verify a new customer can claim this vehicle without duplicate plate conflict
            var verifyService = CreateService(verifyContext);
            var newOwnerResult = await verifyService.CreateAsync(tenantId, new CreateCustomerWithVehicleCommand(
                "Novo Comprador", "11977779999", "LGPD123", VehicleSize.HatchSedan));
            Assert.True(newOwnerResult.IsSuccess);
            Assert.Equal(persistedVehicle.Id, newOwnerResult.Value!.Vehicles.Single().Id);
        }
    }

    [Fact]
    public async Task TenantB_CannotAnonymize_CustomerBelongingToTenantA()
    {
        var tenantA = await CreateTenantAsync("Anonymize Tenant A");
        var tenantB = await CreateTenantAsync("Anonymize Tenant B");
        Guid tenantACustomerId;

        await using (var tenantAContext = CreateYardContext(tenantA))
        {
            var serviceA = CreateService(tenantAContext);
            var created = await serviceA.CreateAsync(tenantA, new CreateCustomerWithVehicleCommand(
                "Cliente Tenant A", "11911112222", "AAA1A11", VehicleSize.HatchSedan));
            Assert.True(created.IsSuccess);
            tenantACustomerId = created.Value!.CustomerId;
        }

        await using (var tenantBContext = CreateYardContext(tenantB))
        {
            var serviceB = CreateService(tenantBContext);
            var anonymizeAttempt = await serviceB.AnonymizeCustomerAsync(tenantB, new AnonymizeCustomerCommand(
                tenantACustomerId,
                Guid.CreateVersion7(),
                "hacker@tenantb.com",
                "Tentativa cross-tenant"));

            Assert.False(anonymizeAttempt.IsSuccess);
            Assert.Equal(CarWashSaaS.Shared.Contracts.ErrorType.NotFound, anonymizeAttempt.Error!.Type);
        }
    }

    [Fact]
    public async Task ExportCustomerDossier_ShouldCompileFullAggregatedData_AcrossEntities()
    {
        var tenantId = await CreateTenantAsync("Export Dossier Tenant");
        Guid customerId;
        Guid vehicleId;
        Guid workOrderId;

        await using (var context = CreateYardContext(tenantId))
        {
            var customer = Customer.Create(tenantId, "Rodrigo LGPD", "11988889999").Value!;
            await context.Customers.AddAsync(customer);
            customerId = customer.Id;

            var vehicle = Vehicle.Create(tenantId, customerId, "DOS1E23", VehicleSize.HatchSedan).Value!;
            await context.Vehicles.AddAsync(vehicle);
            vehicleId = vehicle.Id;

            var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Polimento Tecnico", 150.00m, 60, 1, ServiceCategoryConstants.Polimento).Value!;
            var workOrder = WorkOrder.Create(tenantId, customerId, vehicleId, [item]).Value!;
            workOrder.ChangeStatus(WorkOrderStatus.InWashing);
            workOrder.ChangeStatus(WorkOrderStatus.Finishing);
            workOrder.ChangeStatus(WorkOrderStatus.QualityControl);
            workOrder.ChangeStatus(WorkOrderStatus.ReadyForPickup);
            workOrder.RegisterPickup(DateTimeOffset.UtcNow, "Entregue ao cliente");
            workOrder.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow.AddMinutes(-10));
            workOrder.RecordSatisfactionRating(5, "Servico espetacular", DateTimeOffset.UtcNow);
            workOrder.MarkPaymentConfirmed(150.00m, "Cartao", DateTimeOffset.UtcNow, "CARD-999");
            await context.WorkOrders.AddAsync(workOrder);
            workOrderId = workOrder.Id;

            await context.SaveChangesAsync();
        }

        await using (var context = CreateYardContext(tenantId))
        {
            var exportService = CreateExportService(context);

            var dossierResult = await exportService.ExportDossierAsync(tenantId, customerId);
            Assert.True(dossierResult.IsSuccess);

            var dossier = dossierResult.Value!;
            Assert.Equal(customerId, dossier.Profile.CustomerId);
            Assert.Equal("Rodrigo LGPD", dossier.Profile.Name);
            Assert.Equal("11988889999", dossier.Profile.Phone);
            Assert.False(dossier.Profile.IsAnonymized);

            Assert.Single(dossier.Vehicles);
            Assert.Equal("DOS1E23", dossier.Vehicles[0].Plate);

            Assert.Single(dossier.WorkOrders);
            Assert.Equal(workOrderId, dossier.WorkOrders[0].WorkOrderId);
            Assert.Equal(150.00m, dossier.WorkOrders[0].TotalAmount);
            Assert.True(dossier.WorkOrders[0].IsPaid);
            Assert.NotNull(dossier.WorkOrders[0].Survey);
            Assert.Equal(5, dossier.WorkOrders[0].Survey!.Rating);
            Assert.Equal("Servico espetacular", dossier.WorkOrders[0].Survey!.Feedback);

            var pdfResult = await exportService.ExportDossierPdfAsync(tenantId, customerId);
            Assert.True(pdfResult.IsSuccess);
            Assert.NotEmpty(pdfResult.Value!);
            Assert.Equal(0x25, pdfResult.Value[0]); // %PDF
        }
    }

    [Fact]
    public async Task TenantB_CannotExport_CustomerBelongingToTenantA()
    {
        var tenantA = await CreateTenantAsync("Export Tenant A");
        var tenantB = await CreateTenantAsync("Export Tenant B");
        Guid tenantACustomerId;

        await using (var contextA = CreateYardContext(tenantA))
        {
            var customer = Customer.Create(tenantA, "Titular Seguro Tenant A", "11912345678").Value!;
            await contextA.Customers.AddAsync(customer);
            await contextA.SaveChangesAsync();
            tenantACustomerId = customer.Id;
        }

        await using (var contextB = CreateYardContext(tenantB))
        {
            var exportServiceB = CreateExportService(contextB);
            var exportAttempt = await exportServiceB.ExportDossierAsync(tenantB, tenantACustomerId);

            Assert.False(exportAttempt.IsSuccess);
            Assert.Equal(CarWashSaaS.Shared.Contracts.ErrorType.NotFound, exportAttempt.Error!.Type);
            Assert.Equal("customer.not_found", exportAttempt.Error.Code);
        }
    }

    private static CustomerLookupService CreateLookupService(YardOperationsDbContext context) => new(
        new CustomerVehicleSearchRepository(context),
        new CustomerRepository(context),
        new VehicleRepository(context),
        new YardOperationsUnitOfWork(context));

    private static CustomerVehicleApplicationService CreateService(YardOperationsDbContext context) => new(
        new CustomerVehicleSearchRepository(context),
        new CustomerRepository(context),
        new VehicleRepository(context),
        new YardOperationsUnitOfWork(context));

    private static CustomerDataExportService CreateExportService(YardOperationsDbContext context) => new(
        new CustomerRepository(context),
        new VehicleRepository(context),
        new WorkOrderRepository(context),
        pdfGenerator: new CustomerDossierPdfGenerator());
}
