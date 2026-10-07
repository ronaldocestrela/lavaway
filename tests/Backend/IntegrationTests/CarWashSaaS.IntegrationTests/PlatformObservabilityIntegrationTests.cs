using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class PlatformObservabilityIntegrationTests(SqlServerFixture fixture)
{
    private sealed class TestGlobalTenantLookup : IGlobalTenantLookup
    {
        public Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, $"Tenant_{tenantId.ToString()[..8]}", TenantStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, null, null, null, null, null)));

        public Task<Result<GlobalTenantSummaryDto>> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<GlobalTenantSummaryDto>.Success(new GlobalTenantSummaryDto(
                tenantId, $"Tenant_{tenantId.ToString()[..8]}", request.NewStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TrialEndsAtUtc, request.Reason, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task PlatformMetrics_ShouldAggregateGlobalData_AcrossMultipleTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);
        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var subRepoA = new TenantSaasSubscriptionRepository(dbA);
        var pixRepoA = new PixChargeRepository(dbA);

        var planBasic = SaasPlan.Create(SaasPlanTier.Basic, "Básico", "Plano Básico", 100m, 150, 300, false, false, false, false, 2).Value!;
        var planPro = SaasPlan.Create(SaasPlanTier.Pro, "Pro", "Plano Pro", 250m, 600, 1500, true, true, true, false, 5).Value!;

        var subA = TenantSaasSubscription.CreateActive(tenantA, planBasic, "cus_A", "sub_A").Value!;
        await subRepoA.AddAsync(subA);

        var chargeA = PixCharge.Create(tenantA, Guid.NewGuid(), 80m, $"tx_A_{Guid.NewGuid():N}", "qrA", "copyA", DateTimeOffset.UtcNow.AddHours(1)).Value!;
        chargeA.MarkAsPaid(DateTimeOffset.UtcNow.AddMinutes(-10));
        await pixRepoA.AddAsync(chargeA);
        await dbA.SaveChangesAsync();

        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);
        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var subRepoB = new TenantSaasSubscriptionRepository(dbB);
        var pixRepoB = new PixChargeRepository(dbB);

        var subB = TenantSaasSubscription.CreateActive(tenantB, planPro, "cus_B", "sub_B").Value!;
        await subRepoB.AddAsync(subB);

        var chargeB = PixCharge.Create(tenantB, Guid.NewGuid(), 120m, $"tx_B_{Guid.NewGuid():N}", "qrB", "copyB", DateTimeOffset.UtcNow.AddHours(1)).Value!;
        chargeB.MarkAsPaid(DateTimeOffset.UtcNow.AddMinutes(-5));
        await pixRepoB.AddAsync(chargeB);
        await dbB.SaveChangesAsync();

        var invRepo = new SaasInvoiceRepository(dbA);
        var payHookRepo = new ProcessedPaymentWebhookRepository(dbA);
        var saasHookRepo = new ProcessedSaasWebhookEventRepository(dbA);
        var tenantLookup = new TestGlobalTenantLookup();

        var service = new PlatformBillingMetricsService(subRepoA, invRepo, pixRepoA, payHookRepo, saasHookRepo, tenantLookup);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        // Global metrics
        var revenue = await service.GetRevenueMetricsAsync(from, to);
        Assert.True(revenue.Mrr >= 350m);
        Assert.True(revenue.ActiveSubscriptionsCount >= 2);

        var globalPix = await service.GetPixMetricsAsync(from, to, null);
        Assert.True(globalPix.TotalAmountTransacted >= 200m);
        Assert.True(globalPix.TotalTransactionsCount >= 2);

        // Tenant-filtered metrics (Tenant A only)
        var tenantAPix = await service.GetPixMetricsAsync(from, to, tenantA);
        Assert.Equal(80m, tenantAPix.TotalAmountTransacted);
        Assert.Equal(1, tenantAPix.TotalTransactionsCount);
    }
}
