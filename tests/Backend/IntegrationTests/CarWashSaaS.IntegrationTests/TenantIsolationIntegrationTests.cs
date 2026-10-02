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

    [Fact]
    public async Task TenantB_ShouldNotReadOrModifyTenantA_YardSetupData()
    {
        var tenantA = await CreateTenantAsync("Yard A");
        var tenantB = await CreateTenantAsync("Yard B");

        var capacity = YardCapacity.Create(tenantA, 8, "Boxes principais").Value!;
        var member = TeamMember.Create(tenantA, "Maria Costa", "Operador", "maria@yarda.com").Value!;
        var commissionRule = CommissionRule.Create(tenantA, "Lavagem completa", "Operador", 12.5m).Value!;

        await using (var context = CreateYardContext(tenantA))
        {
            context.YardCapacities.Add(capacity);
            context.TeamMembers.Add(member);
            context.CommissionRules.Add(commissionRule);
            await context.SaveChangesAsync();
        }

        await using var tenantBContext = CreateYardContext(tenantB);
        Assert.Null(await tenantBContext.YardCapacities.SingleOrDefaultAsync(value => value.Id == capacity.Id));
        Assert.Null(await tenantBContext.TeamMembers.SingleOrDefaultAsync(value => value.Id == member.Id));
        Assert.Null(await tenantBContext.CommissionRules.SingleOrDefaultAsync(value => value.Id == commissionRule.Id));

        var tamperedCapacity = YardCapacity.Create(tenantA, 10, "Tentativa").Value!;
        tenantBContext.YardCapacities.Attach(tamperedCapacity);
        tenantBContext.Entry(tamperedCapacity).Property(value => value.Description).CurrentValue = "Tampered";
        tenantBContext.Entry(tamperedCapacity).Property(value => value.Description).IsModified = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => tenantBContext.SaveChangesAsync());
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