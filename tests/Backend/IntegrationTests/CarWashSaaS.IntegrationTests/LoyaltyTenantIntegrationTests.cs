using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class LoyaltyTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CustomerLoyaltyAccount_ShouldBeIsolatedByTenant_PreventingCrossTenantDataLeak()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerIdA = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessorA);
        var repoA = new CustomerLoyaltyRepository(dbA);

        var accountA = CustomerLoyaltyAccount.Create(tenantA, customerIdA).Value!;
        accountA.CreditStamps(5, Guid.NewGuid(), "OS-100");
        await repoA.AddAccountAsync(accountA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A deve ver a conta e o saldo de 5 selos
        var foundA = await repoA.GetByCustomerIdAsync(tenantA, customerIdA);
        Assert.NotNull(foundA);
        Assert.Equal(5, foundA.Balance);
        Assert.Single(foundA.Transactions);

        // 2. Tenant B NÃO deve ver a conta do Tenant A
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessorB);
        var repoB = new CustomerLoyaltyRepository(dbB);

        var foundB = await repoB.GetByCustomerIdAsync(tenantB, customerIdA);
        Assert.Null(foundB);

        var directQueryB = await dbB.CustomerLoyaltyAccounts.FirstOrDefaultAsync(a => a.Id == accountA.Id);
        Assert.Null(directQueryB);

        var listB = await repoB.ListAccountsAsync(tenantB);
        Assert.DoesNotContain(listB, a => a.Id == accountA.Id);
    }

    [Fact]
    public async Task LoyaltyProgram_ShouldBeIsolatedByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessorA);
        var repoA = new LoyaltyProgramRepository(dbA);

        var programA = LoyaltyProgram.CreateDefault(tenantA).Value!;
        programA.Update(true, 15, "Polimento Cristalizado Grátis", 1, true, null);
        await repoA.AddAsync(programA);
        await repoA.SaveChangesAsync();

        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessorB);
        var repoB = new LoyaltyProgramRepository(dbB);

        var programB = LoyaltyProgram.CreateDefault(tenantB).Value!;
        programB.Update(true, 5, "Ducha Rápida", 1, true, null);
        await repoB.AddAsync(programB);
        await repoB.SaveChangesAsync();

        // Verifica integridade isolada
        var queryA = await repoA.GetByTenantIdAsync(tenantA);
        Assert.NotNull(queryA);
        Assert.Equal(15, queryA.TargetStamps);
        Assert.Equal("Polimento Cristalizado Grátis", queryA.RewardTitle);

        var queryB = await repoB.GetByTenantIdAsync(tenantB);
        Assert.NotNull(queryB);
        Assert.Equal(5, queryB.TargetStamps);
        Assert.Equal("Ducha Rápida", queryB.RewardTitle);
    }
}
