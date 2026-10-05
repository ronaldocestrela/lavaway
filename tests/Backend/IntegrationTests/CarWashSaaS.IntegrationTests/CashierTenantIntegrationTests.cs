using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class CashierTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task CashTransaction_ShouldBeIsolatedByTenant_PreventingCrossTenantDataLeak()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new CashTransactionRepository(dbA);

        var txA = CashTransaction.CreateIncome(
            tenantA,
            150.00m,
            PaymentMethodConstants.Cash,
            "Recebimento em dinheiro OS",
            workOrderId: workOrderId,
            registeredByUserName: "Atendente A").Value!;

        await repoA.AddAsync(txA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A deve ver a transação
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var listA = await repoA.ListByDateAsync(tenantA, date);
        Assert.Contains(listA, t => t.Id == txA.Id);

        // 2. Tenant B NÃO deve ver a transação de Tenant A
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var repoB = new CashTransactionRepository(dbB);

        var listB = await repoB.ListByDateAsync(tenantB, date);
        Assert.DoesNotContain(listB, t => t.Id == txA.Id);

        var queryB = await dbB.CashTransactions.Where(t => t.Id == txA.Id).FirstOrDefaultAsync();
        Assert.Null(queryB);
    }

    [Fact]
    public async Task DailyCashClosing_ShouldBeIsolatedByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 5);

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new DailyCashClosingRepository(dbA);

        var closingA = DailyCashClosing.Close(
            tenantA,
            date,
            Guid.NewGuid(),
            "Gestor A",
            1000m, 400m, 300m, 200m, 100m, 100m, 50m,
            actualCashInDrawer: 350m,
            notes: "Fechamento A").Value!;

        await repoA.AddAsync(closingA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A encontra fechamento
        var foundA = await repoA.GetByDateAsync(tenantA, date);
        Assert.NotNull(foundA);
        Assert.Equal(1000m, foundA.TotalIncome);

        // 2. Tenant B NÃO encontra fechamento de Tenant A
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var repoB = new DailyCashClosingRepository(dbB);

        var foundB = await repoB.GetByDateAsync(tenantB, date);
        Assert.Null(foundB);
    }
}
