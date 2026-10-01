using CarWashSaaS.Shared.Configuration;
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
        if (!fixture.IsAvailable)
        {
            return;
        }

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
        if (!fixture.IsAvailable)
        {
            return;
        }

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

    private static CustomerVehicleApplicationService CreateService(YardOperationsDbContext context) => new(
        new CustomerVehicleSearchRepository(context),
        new CustomerRepository(context),
        new VehicleRepository(context),
        new YardOperationsUnitOfWork(context));
}