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
public sealed class WorkOrderIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CreateAsync_ShouldPersistWorkOrderWithItemsAndNotes()
    {
        var tenantId = await CreateTenantAsync("WorkOrder Persist Tenant");
        await using var context = CreateYardContext(tenantId);
        var workOrderService = CreateWorkOrderService(context);

        // 1. Create customer and vehicle
        var customer = Customer.Create(tenantId, "Juliana Paes", "11999998888").Value!;
        var vehicle = Vehicle.Create(tenantId, customer.Id, "BRA2E19", VehicleSize.Suv).Value!;
        context.Customers.Add(customer);
        context.Vehicles.Add(vehicle);

        // 2. Create services with price for Suv
        var price1 = ServicePrice.Create(tenantId, VehicleSize.Suv, 90m, 50).Value!;
        var service1 = Service.Create(tenantId, "Ducha com Cera", "Lavagem", [price1]).Value!;
        var price2 = ServicePrice.Create(tenantId, VehicleSize.Suv, 60m, 30).Value!;
        var service2 = Service.Create(tenantId, "Higienização Interna", "Higienização", [price2]).Value!;
        context.Services.AddRange(service1, service2);
        await context.SaveChangesAsync();

        // 3. Create work order
        var command = new CreateWorkOrderCommand(
            customer.Id,
            vehicle.Id,
            [
                new CreateWorkOrderItemInput(service1.Id, 1),
                new CreateWorkOrderItemInput(service2.Id, 1)
            ],
            "Cliente solicitou capricho nas rodas.");

        var createResult = await workOrderService.CreateAsync(tenantId, command);

        Assert.True(createResult.IsSuccess);
        var createdDto = createResult.Value!;
        Assert.Equal(150m, createdDto.TotalAmount);
        Assert.Equal(80, createdDto.EstimatedDurationMinutes);
        Assert.Equal(2, createdDto.Items.Count);
        Assert.Equal("Cliente solicitou capricho nas rodas.", createdDto.Notes);
        Assert.Equal("Waiting", createdDto.Status);
        Assert.Equal("Juliana Paes", createdDto.CustomerName);
        Assert.Equal("BRA2E19", createdDto.Plate);
        Assert.Equal("Suv", createdDto.VehicleSize);

        // 4. Verify reading back from clean context
        await using var readContext = CreateYardContext(tenantId);
        var readService = CreateWorkOrderService(readContext);
        var getResult = await readService.GetAsync(tenantId, createdDto.Id);

        Assert.True(getResult.IsSuccess);
        var fetched = getResult.Value!;
        Assert.Equal(createdDto.Id, fetched.Id);
        Assert.Equal(150m, fetched.TotalAmount);
        Assert.Equal(80, fetched.EstimatedDurationMinutes);
        Assert.Equal("Cliente solicitou capricho nas rodas.", fetched.Notes);
        Assert.Equal(2, fetched.Items.Count);
    }

    [Fact]
    public async Task TenantB_ShouldNotAccessOrOpenWorkOrderForTenantACustomerOrVehicle()
    {
        var tenantA = await CreateTenantAsync("WorkOrder Tenant A");
        var tenantB = await CreateTenantAsync("WorkOrder Tenant B");

        Guid tenantACustomerId;
        Guid tenantAVehicleId;
        Guid tenantAServiceId;
        Guid tenantAWorkOrderId;

        await using (var contextA = CreateYardContext(tenantA))
        {
            var customer = Customer.Create(tenantA, "Proprietário A", "11911112222").Value!;
            var vehicle = Vehicle.Create(tenantA, customer.Id, "TEN1A01", VehicleSize.HatchSedan).Value!;
            var price = ServicePrice.Create(tenantA, VehicleSize.HatchSedan, 50m, 30).Value!;
            var service = Service.Create(tenantA, "Lavagem Simples", "Lavagem", [price]).Value!;

            contextA.Customers.Add(customer);
            contextA.Vehicles.Add(vehicle);
            contextA.Services.Add(service);
            await contextA.SaveChangesAsync();

            tenantACustomerId = customer.Id;
            tenantAVehicleId = vehicle.Id;
            tenantAServiceId = service.Id;

            var workOrderServiceA = CreateWorkOrderService(contextA);
            var orderResult = await workOrderServiceA.CreateAsync(tenantA, new CreateWorkOrderCommand(
                tenantACustomerId,
                tenantAVehicleId,
                [new CreateWorkOrderItemInput(tenantAServiceId, 1)]));

            Assert.True(orderResult.IsSuccess);
            tenantAWorkOrderId = orderResult.Value!.Id;
        }

        // Now Tenant B attempts to open a work order using Tenant A's customer & vehicle
        await using var contextB = CreateYardContext(tenantB);
        var workOrderServiceB = CreateWorkOrderService(contextB);

        var crossTenantCreate = await workOrderServiceB.CreateAsync(tenantB, new CreateWorkOrderCommand(
            tenantACustomerId,
            tenantAVehicleId,
            [new CreateWorkOrderItemInput(tenantAServiceId, 1)]));

        Assert.False(crossTenantCreate.IsSuccess);
        Assert.Equal(ErrorType.NotFound, crossTenantCreate.Error!.Type);

        // Tenant B attempts to read Tenant A's work order
        var crossTenantGet = await workOrderServiceB.GetAsync(tenantB, tenantAWorkOrderId);
        Assert.False(crossTenantGet.IsSuccess);
        Assert.Equal(ErrorType.NotFound, crossTenantGet.Error!.Type);
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
        var tenant = Tenant.Create(name).Value!;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant.Id;
    }

    private YardOperationsDbContext CreateYardContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessor);
    }

    private static WorkOrderApplicationService CreateWorkOrderService(YardOperationsDbContext context) => new(
        new WorkOrderRepository(context),
        new CustomerRepository(context),
        new VehicleRepository(context),
        new ServiceRepository(context),
        new YardOperationsUnitOfWork(context));
}
