using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class StoreProfileTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CreateAndUpdate_ShouldPersistProfile_PerTenant()
    {
        var tenantA = await CreateTenantAsync("Tenant A Store");
        var tenantB = await CreateTenantAsync("Tenant B Store");

        var tenantAAccessor = new CurrentTenantAccessor();
        tenantAAccessor.SetTenant(tenantA);
        await using var tenantAContext = new TenantsDbContext(fixture.CreateTenantsOptions(), tenantAAccessor);
        var tenantARepository = new StoreProfileRepository(tenantAContext);
        var tenantAService = new StoreProfileApplicationService(tenantARepository);

        var createResult = await tenantAService.CreateAsync(tenantA, new CreateStoreProfileCommand(
            "LavaWay Auto Center Ltda",
            "LavaWay Centro",
            "11222333000181",
            "+5511999999999",
            "Rua das Flores, 123",
            "São Paulo",
            "SP",
            "01000-000"));

        Assert.True(createResult.IsSuccess);

        var tenantBAccessor = new CurrentTenantAccessor();
        tenantBAccessor.SetTenant(tenantB);
        await using var tenantBContext = new TenantsDbContext(fixture.CreateTenantsOptions(), tenantBAccessor);
        var tenantBRepository = new StoreProfileRepository(tenantBContext);
        var tenantBService = new StoreProfileApplicationService(tenantBRepository);

        var getByTenantB = await tenantBService.GetAsync(tenantB);
        Assert.False(getByTenantB.IsSuccess);
        Assert.Equal(ErrorType.NotFound, getByTenantB.Error!.Type);

        var freshTenantAAccessor = new CurrentTenantAccessor();
        freshTenantAAccessor.SetTenant(tenantA);
        await using var freshTenantAContext = new TenantsDbContext(fixture.CreateTenantsOptions(), freshTenantAAccessor);
        var freshProfile = await freshTenantAContext.StoreProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.TenantId == tenantA);

        Assert.NotNull(freshProfile);

        var updateTenantAAccessor = new CurrentTenantAccessor();
        updateTenantAAccessor.SetTenant(tenantA);
        await using var updateTenantAContext = new TenantsDbContext(fixture.CreateTenantsOptions(), updateTenantAAccessor);
        var updateTenantAService = new StoreProfileApplicationService(new StoreProfileRepository(updateTenantAContext));
        var updateResult = await updateTenantAService.UpdateAsync(tenantA, new UpdateStoreProfileCommand(
            "LavaWay Auto Center Atualizada Ltda",
            "LavaWay Centro Atualizado",
            "11222333000181",
            "+5511888888888",
            "Avenida Paulista, 456",
            "Rio de Janeiro",
            "RJ",
            "22000-000"));

        Assert.True(updateResult.IsSuccess);

        var persisted = await freshTenantAContext.StoreProfiles
            .AsNoTracking()
            .SingleAsync(profile => profile.TenantId == tenantA);

        Assert.Equal("LavaWay Auto Center Atualizada Ltda", persisted.LegalName);
        Assert.Equal("RJ", persisted.State);
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
        var tenant = Tenant.Create(name).Value!;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant.Id;
    }
}
