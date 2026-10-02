using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class ServiceCatalogTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task ServiceCatalog_ShouldPersistPricesAcrossVehicleSizes_AndEnforceStrictTenantIsolation()
    {
        var tenantA = await CreateTenantAsync("Tenant A CarWash");
        var tenantB = await CreateTenantAsync("Tenant B CarWash");

        Guid serviceAId;

        // Tenant A creates a service with 4 vehicle sizes
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceCatalogA = CreateService(contextA);
            var createResult = await serviceCatalogA.CreateAsync(tenantA, new CreateServiceCommand(
                "Lavagem Completa",
                ServiceCategoryConstants.LavagemCompleta,
                [
                    new ServicePriceInput(VehicleSize.HatchSedan, 70m, 45),
                    new ServicePriceInput(VehicleSize.Suv, 90m, 60),
                    new ServicePriceInput(VehicleSize.PickupVan, 110m, 75),
                    new ServicePriceInput(VehicleSize.Motorcycle, 50m, 30)
                ]));

            Assert.True(createResult.IsSuccess);
            Assert.NotNull(createResult.Value);
            serviceAId = createResult.Value.Id;
            Assert.Equal(4, createResult.Value.Prices.Count);
        }

        // Tenant A queries catalog
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceCatalogA = CreateService(contextA);
            var listResult = await serviceCatalogA.ListAsync(tenantA);
            Assert.True(listResult.IsSuccess);
            var found = Assert.Single(listResult.Value!);
            Assert.Equal(serviceAId, found.Id);
            Assert.Equal(4, found.Prices.Count);

            var getResult = await serviceCatalogA.GetAsync(tenantA, serviceAId);
            Assert.True(getResult.IsSuccess);
            Assert.Equal("Lavagem Completa", getResult.Value!.Name);
        }

        // Tenant B queries catalog - must receive empty list and NotFound on Tenant A's ID
        await using (var contextB = CreateYardContext(tenantB))
        {
            var serviceCatalogB = CreateService(contextB);
            var listB = await serviceCatalogB.ListAsync(tenantB);
            Assert.True(listB.IsSuccess);
            Assert.Empty(listB.Value!);

            var getB = await serviceCatalogB.GetAsync(tenantB, serviceAId);
            Assert.False(getB.IsSuccess);
            Assert.Equal(ErrorType.NotFound, getB.Error!.Type);

            // Tenant B attempts to update Tenant A's service
            var updateB = await serviceCatalogB.UpdateAsync(tenantB, serviceAId, new UpdateServiceCommand(
                "Hacked Service",
                ServiceCategoryConstants.LavagemCompleta,
                [
                    new ServicePriceInput(VehicleSize.HatchSedan, 10m, 10)
                ]));
            Assert.False(updateB.IsSuccess);
            Assert.Equal(ErrorType.NotFound, updateB.Error!.Type);
        }

        // Tenant A updates service
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceCatalogA = CreateService(contextA);
            var updateResult = await serviceCatalogA.UpdateAsync(tenantA, serviceAId, new UpdateServiceCommand(
                "Lavagem Completa Premium",
                ServiceCategoryConstants.LavagemCompleta,
                [
                    new ServicePriceInput(VehicleSize.HatchSedan, 80m, 50),
                    new ServicePriceInput(VehicleSize.Suv, 100m, 65)
                ]));

            Assert.True(updateResult.IsSuccess);
            Assert.Equal("Lavagem Completa Premium", updateResult.Value!.Name);
            Assert.Equal(2, updateResult.Value.Prices.Count);
        }
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

    private static ServiceCatalogApplicationService CreateService(YardOperationsDbContext context) => new(
        new ServiceRepository(context));
}
