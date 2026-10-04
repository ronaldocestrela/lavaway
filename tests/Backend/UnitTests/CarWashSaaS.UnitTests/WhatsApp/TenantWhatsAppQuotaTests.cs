using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class TenantWhatsAppQuotaTests
{
    [Fact]
    public void CreateDefault_Should_Initialize_With_Configured_Limits()
    {
        var tenantId = Guid.NewGuid();

        var result = TenantWhatsAppQuota.CreateDefault(tenantId, maxPerMinute: 15, maxPerDay: 300);

        Assert.True(result.IsSuccess);
        var quota = result.Value!;
        Assert.Equal(tenantId, quota.TenantId);
        Assert.Equal(15, quota.MaxMessagesPerMinute);
        Assert.Equal(300, quota.MaxMessagesPerDay);
        Assert.True(quota.CanSend());
    }

    [Fact]
    public void RecordSend_Should_Increment_Counters_And_Block_When_Exceeded()
    {
        var quota = TenantWhatsAppQuota.CreateDefault(Guid.NewGuid(), maxPerMinute: 2, maxPerDay: 5).Value!;

        var send1 = quota.RecordSend();
        Assert.True(send1.IsSuccess);
        Assert.Equal(1, quota.SentInCurrentMinute);
        Assert.Equal(1, quota.SentToday);

        var send2 = quota.RecordSend();
        Assert.True(send2.IsSuccess);
        Assert.Equal(2, quota.SentInCurrentMinute);
        Assert.Equal(2, quota.SentToday);

        // Ultrapassou limite por minuto
        Assert.False(quota.CanSend());
        var send3 = quota.RecordSend();
        Assert.False(send3.IsSuccess);
        Assert.Equal("whatsapp.quota_exceeded.minute", send3.Error!.Code);
    }

    [Fact]
    public void RecordSend_Should_Enforce_Daily_Limit()
    {
        var quota = TenantWhatsAppQuota.CreateDefault(Guid.NewGuid(), maxPerMinute: 100, maxPerDay: 2).Value!;

        Assert.True(quota.RecordSend().IsSuccess);
        Assert.True(quota.RecordSend().IsSuccess);

        var excessSend = quota.RecordSend();
        Assert.False(excessSend.IsSuccess);
        Assert.Equal("whatsapp.quota_exceeded.daily", excessSend.Error!.Code);
    }
}
