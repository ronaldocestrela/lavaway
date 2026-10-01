using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class TenantIsolationIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task TenantB_ShouldNotReadOrModifyTenantA_Customer()
    {
        if (!fixture.IsAvailable)
        {
            return;
        }

        var tenantA = await CreateTenantAsync("Tenant A");
        var tenantB = await CreateTenantAsync("Tenant B");
        var customer = Customer.Create(tenantA, "Customer A", "555-0101").Value!;

        await using (var context = CreateYardContext(tenantA))
        {
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
        }

        await using var tenantBContext = CreateYardContext(tenantB);
        Assert.Null(await tenantBContext.Customers.SingleOrDefaultAsync(value => value.Id == customer.Id));

        var attachedCustomer = Customer.Create(tenantA, "Customer A", "555-0101").Value!;
        tenantBContext.Customers.Attach(attachedCustomer);
        tenantBContext.Entry(attachedCustomer).Property(value => value.Name).CurrentValue = "Tampered";
        tenantBContext.Entry(attachedCustomer).Property(value => value.Name).IsModified = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => tenantBContext.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantB_ShouldNotReadTenantA_IdentityUser_AndNewUserShouldInheritTenant()
    {
        if (!fixture.IsAvailable)
        {
            return;
        }

        var tenantA = await CreateTenantAsync("Identity Tenant A");
        var tenantB = await CreateTenantAsync("Identity Tenant B");
        var userId = Guid.NewGuid();

        await using (var context = CreateIdentityContext(tenantA))
        {
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"tenant-a-{userId:N}",
                NormalizedUserName = $"TENANT-A-{userId:N}",
                TenantId = Guid.Empty
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            Assert.Equal(tenantA, user.TenantId);
        }

        await using var tenantBContext = CreateIdentityContext(tenantB);
        Assert.Null(await tenantBContext.Users.SingleOrDefaultAsync(value => value.Id == userId));
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new CarWashSaaS.Tenants.Infrastructure.TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
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

    private IdentityModuleDbContext CreateIdentityContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
    }
}