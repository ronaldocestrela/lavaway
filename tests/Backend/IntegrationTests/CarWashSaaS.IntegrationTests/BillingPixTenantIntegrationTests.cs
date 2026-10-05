using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class BillingPixTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task PixCharge_ShouldBeIsolatedByTenant_PreventingCrossTenantDataLeak()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);

        await using var dbA = new BillingDbContext(fixture.CreateBillingOptions(), accessorA);
        var repoA = new PixChargeRepository(dbA);

        var chargeA = PixCharge.Create(
            tenantA,
            workOrderId,
            180m,
            $"TX_{Guid.NewGuid():N}",
            "data:image/svg+xml;base64,sample",
            "00020126580014br.gov.bcb.pix...",
            DateTimeOffset.UtcNow.AddMinutes(30)).Value!;

        await repoA.AddAsync(chargeA);
        await repoA.SaveChangesAsync();

        // 1. Tenant A should see the charge
        var foundByTenantA = await repoA.GetByIdAsync(tenantA, chargeA.Id);
        Assert.NotNull(foundByTenantA);
        Assert.Equal(180m, foundByTenantA.Amount);

        // 2. Tenant B should NOT see the charge (isolated by Global Query Filter and explicit TenantId)
        var accessorB = new CurrentTenantAccessor();
        accessorB.SetTenant(tenantB);

        await using var dbB = new BillingDbContext(fixture.CreateBillingOptions(), accessorB);
        var repoB = new PixChargeRepository(dbB);

        var foundByTenantB = await repoB.GetByIdAsync(tenantB, chargeA.Id);
        Assert.Null(foundByTenantB);

        var listByTenantB = await dbB.PixCharges.Where(c => c.Id == chargeA.Id).FirstOrDefaultAsync();
        Assert.Null(listByTenantB);

        var activeForTenantB = await repoB.GetActiveByWorkOrderIdAsync(tenantB, workOrderId);
        Assert.Null(activeForTenantB);
    }
}
