using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class SaasBillingTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task SaasSubscriptionAndInvoices_ShouldBeIsolatedByTenant_InDatabase()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var subRepoA = new TenantSaasSubscriptionRepository(dbA);
        var invoiceRepoA = new SaasInvoiceRepository(dbA);
        var quotaRepoA = new TenantQuotaUsageRepository(dbA);

        var plan = SaasPlan.GetStandardPlans().First(p => p.Tier == SaasPlanTier.Pro);
        var subA = TenantSaasSubscription.CreateActive(tenantA, plan, "cus_A", "sub_A").Value!;
        var invoiceA = SaasInvoice.CreatePending(tenantA, "inv_A_100", 299.00m, DateTimeOffset.UtcNow.AddDays(5)).Value!;
        var quotaA = TenantQuotaUsage.CreateForCycle(tenantA, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1)).Value!;
        quotaA.RecordWorkOrderCreated(600);

        await subRepoA.AddAsync(subA);
        await invoiceRepoA.AddAsync(invoiceA);
        await quotaRepoA.AddAsync(quotaA);
        await subRepoA.SaveChangesAsync();

        // Tenant A consulta com sucesso
        var foundSubA = await subRepoA.GetByTenantIdAsync(tenantA);
        Assert.NotNull(foundSubA);
        Assert.Equal(tenantA, foundSubA.TenantId);

        var invoicesA = await invoiceRepoA.ListByTenantIdAsync(tenantA);
        Assert.Single(invoicesA);
        Assert.Equal("inv_A_100", invoicesA[0].GatewayInvoiceId);

        // Tenant B NÃO deve ver faturas do Tenant A quando consultado com seu próprio contexto
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var invoiceRepoB = new SaasInvoiceRepository(dbB);

        var invoicesB = await invoiceRepoB.ListByTenantIdAsync(tenantB);
        Assert.Empty(invoicesB);

        var directInvoiceB = await dbB.SaasInvoices.FirstOrDefaultAsync(i => i.Id == invoiceA.Id);
        Assert.Null(directInvoiceB);
    }

    [Fact]
    public async Task SaasPlans_ShouldBeGloballyAvailable_AcrossDifferentTenants()
    {
        var tenantA = Guid.NewGuid();
        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var planRepo = new SaasPlanRepository(dbA);
        await planRepo.EnsureSeedDataAsync();

        var plans = await planRepo.ListActiveAsync();
        Assert.NotEmpty(plans);
        Assert.Contains(plans, p => p.Tier == SaasPlanTier.Basic);
        Assert.Contains(plans, p => p.Tier == SaasPlanTier.Pro);
        Assert.Contains(plans, p => p.Tier == SaasPlanTier.Enterprise);
    }
}
