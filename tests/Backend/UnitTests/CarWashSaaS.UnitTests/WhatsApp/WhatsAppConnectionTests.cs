using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class WhatsAppConnectionTests
{
    [Fact]
    public void Create_Should_Succeed_With_Valid_Connection_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = WhatsAppConnection.Create(tenantId, "provider-session-123", "qr-code-456");

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value!.TenantId);
        Assert.Equal(WhatsAppConnectionStatus.Connecting, result.Value.Status);
        Assert.Equal("provider-session-123", result.Value.ProviderSessionId);
        Assert.Equal("qr-code-456", result.Value.QrCodeValue);
    }

    [Fact]
    public void Create_Should_Reject_Empty_Tenant_Id()
    {
        var result = WhatsAppConnection.Create(Guid.Empty, "provider-session-123", "qr-code-456");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Refresh_Should_Reset_QrCode_And_Set_Status_To_Connecting()
    {
        var connection = WhatsAppConnection.Create(Guid.NewGuid(), "provider-session-123", "old-qr").Value!;

        var result = connection.Refresh("new-qr");

        Assert.True(result.IsSuccess);
        Assert.Equal("new-qr", connection.QrCodeValue);
        Assert.Equal(WhatsAppConnectionStatus.Connecting, connection.Status);
    }
}
