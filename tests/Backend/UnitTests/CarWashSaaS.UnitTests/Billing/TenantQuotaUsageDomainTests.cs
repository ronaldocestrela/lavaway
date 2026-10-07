using CarWashSaaS.Billing.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantQuotaUsageDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CanCreateWorkOrder_Should_Return_True_When_Under_Limit()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMonths(1);
        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, start, end).Value!;

        Assert.True(quota.CanCreateWorkOrder(maxLimit: 150));
    }

    [Fact]
    public void RecordWorkOrderCreated_Should_Fail_When_Limit_Reached()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMonths(1);
        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, start, end).Value!;

        // Simula 2 OS permitidas
        var r1 = quota.RecordWorkOrderCreated(maxLimit: 2);
        var r2 = quota.RecordWorkOrderCreated(maxLimit: 2);
        var r3 = quota.RecordWorkOrderCreated(maxLimit: 2);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.False(r3.IsSuccess);
        Assert.Equal("tenant.quota.work_orders_exceeded", r3.Error!.Code);
        Assert.Equal(2, quota.WorkOrdersCreatedCount);
    }

    [Fact]
    public void Unlimited_Limit_Should_Always_Allow_WorkOrders_And_WhatsApp()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMonths(1);
        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, start, end).Value!;

        // 0 significa ilimitado (ex: Enterprise)
        for (int i = 0; i < 50; i++)
        {
            var r = quota.RecordWorkOrderCreated(maxLimit: 0);
            Assert.True(r.IsSuccess);
            var rw = quota.RecordWhatsAppSent(maxLimit: 0);
            Assert.True(rw.IsSuccess);
        }

        Assert.Equal(50, quota.WorkOrdersCreatedCount);
        Assert.Equal(50, quota.WhatsAppMessagesSentCount);
    }

    [Fact]
    public void ResetForNewCycle_Should_Clear_Counters()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMonths(1);
        var quota = TenantQuotaUsage.CreateForCycle(_tenantId, start, end).Value!;

        quota.RecordWorkOrderCreated(maxLimit: 10);
        quota.RecordWhatsAppSent(maxLimit: 10);
        Assert.Equal(1, quota.WorkOrdersCreatedCount);
        Assert.Equal(1, quota.WhatsAppMessagesSentCount);

        var newStart = end;
        var newEnd = newStart.AddMonths(1);
        quota.ResetForNewCycle(newStart, newEnd);

        Assert.Equal(0, quota.WorkOrdersCreatedCount);
        Assert.Equal(0, quota.WhatsAppMessagesSentCount);
        Assert.Equal(newStart, quota.CycleStartUtc);
        Assert.Equal(newEnd, quota.CycleEndUtc);
    }
}
