using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class SubscriptionTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task SubscriptionPlan_ShouldBeIsolatedByTenant_PreventingCrossTenantDataLeak()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new SubscriptionPlanRepository(dbA);

        var planA = SubscriptionPlan.Create(
            tenantA,
            "Plano Mensal Ouro A",
            "4 lavagens por mês",
            199.90m,
            4,
            2,
            30).Value!;

        await repoA.AddAsync(planA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A deve ver o plano
        var foundA = await repoA.GetByIdAsync(tenantA, planA.Id);
        Assert.NotNull(foundA);
        Assert.Equal("Plano Mensal Ouro A", foundA.Name);

        var listA = await repoA.ListActiveAsync(tenantA);
        Assert.Contains(listA, p => p.Id == planA.Id);

        // 2. Tenant B NÃO deve ver o plano de Tenant A
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var repoB = new SubscriptionPlanRepository(dbB);

        var foundB = await repoB.GetByIdAsync(tenantB, planA.Id);
        Assert.Null(foundB);

        var listB = await repoB.ListActiveAsync(tenantB);
        Assert.DoesNotContain(listB, p => p.Id == planA.Id);

        var directB = await dbB.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planA.Id);
        Assert.Null(directB);
    }

    [Fact]
    public async Task CustomerSubscription_And_Plate_ShouldBeIsolatedByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerIdA = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new CustomerSubscriptionRepository(dbA);

        var subA = CustomerSubscription.Create(
            tenantA,
            customerIdA,
            "Cliente Especial A",
            "11988881111",
            Guid.NewGuid(),
            "Plano Mensal",
            creditsPerCycle: 4,
            allowedPlatesLimit: 2,
            initialPlates: ["SUB1A23"],
            periodStartUtc: now.AddDays(-1),
            periodEndUtc: now.AddDays(29)).Value!;

        await repoA.AddAsync(subA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A localiza por Id e por Placa
        var byPlateA = await repoA.GetActiveByPlateAsync(tenantA, "SUB1A23", now);
        Assert.NotNull(byPlateA);
        Assert.Equal("Cliente Especial A", byPlateA.CustomerName);
        Assert.Single(byPlateA.AuthorizedPlates);

        // 2. Tenant B NÃO localiza assinatura nem por Placa nem por Id
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var repoB = new CustomerSubscriptionRepository(dbB);

        var byPlateB = await repoB.GetActiveByPlateAsync(tenantB, "SUB1A23", now);
        Assert.Null(byPlateB);

        var byIdB = await repoB.GetByIdAsync(tenantB, subA.Id);
        Assert.Null(byIdB);

        var directB = await dbB.CustomerSubscriptions.FirstOrDefaultAsync(s => s.Id == subA.Id);
        Assert.Null(directB);
    }

    [Fact]
    public async Task SubscriptionUsage_Should_Deduct_Credits_And_Persist_Ledger_Isolated()
    {
        var tenantA = Guid.NewGuid();
        var customerIdA = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new CustomerSubscriptionRepository(dbA);

        var subA = CustomerSubscription.Create(
            tenantA,
            customerIdA,
            "João Comprador",
            "11988882222",
            Guid.NewGuid(),
            "Plano 2 Lavagens",
            creditsPerCycle: 2,
            allowedPlatesLimit: 1,
            initialPlates: ["LAV9B88"],
            periodStartUtc: now.AddDays(-2),
            periodEndUtc: now.AddDays(28)).Value!;

        var workOrderId = Guid.NewGuid();
        var usageResult = subA.ConsumeCredit("LAV9B88", workOrderId, "Ducha Simples", now, "Primeiro uso");
        Assert.True(usageResult.IsSuccess);

        await repoA.AddAsync(subA);
        await repoA.SaveChangesAsync();

        // Recarrega do banco e valida persistência dos consumos e saldo atualizado
        var reloadedA = await repoA.GetByIdAsync(tenantA, subA.Id);
        Assert.NotNull(reloadedA);
        Assert.Equal(1, reloadedA.UsedCreditsInCycle);
        Assert.Equal(1, reloadedA.AvailableCredits);
        Assert.Single(reloadedA.Usages);

        var usages = await repoA.ListUsagesBySubscriptionIdAsync(tenantA, subA.Id);
        Assert.Single(usages);
        Assert.Equal("LAV9B88", usages[0].Plate);
        Assert.Equal(workOrderId, usages[0].WorkOrderId);
    }
}
