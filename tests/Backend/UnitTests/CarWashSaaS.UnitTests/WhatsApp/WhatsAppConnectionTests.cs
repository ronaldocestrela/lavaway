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

    [Fact]
    public void MarkDisconnected_Should_Update_Status_Reason_And_Activate_Alert()
    {
        var now = DateTimeOffset.UtcNow;
        var connection = WhatsAppConnection.Create(Guid.NewGuid(), "provider-session-123", "qr-123").Value!;
        connection.MarkConnected(now.AddHours(-1));

        var result = connection.MarkDisconnected("Provider closed connection", now);

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppConnectionStatus.Disconnected, connection.Status);
        Assert.Equal(now, connection.LastDisconnectedAtUtc);
        Assert.Equal("Provider closed connection", connection.DisconnectReason);
        Assert.True(connection.HasActiveAlert);
    }

    [Fact]
    public void MarkConnected_Should_Reset_ActiveAlert_And_DisconnectReason()
    {
        var now = DateTimeOffset.UtcNow;
        var connection = WhatsAppConnection.Create(Guid.NewGuid(), "provider-session-123", "qr-123").Value!;
        connection.MarkDisconnected("Lost signal", now.AddMinutes(-30));
        Assert.True(connection.HasActiveAlert);

        var result = connection.MarkConnected(now);

        Assert.True(result.IsSuccess);
        Assert.Equal(WhatsAppConnectionStatus.Connected, connection.Status);
        Assert.Equal(now, connection.LastConnectedAtUtc);
        Assert.False(connection.HasActiveAlert);
        Assert.Null(connection.DisconnectReason);
    }

    [Fact]
    public void ShouldSendAlert_Should_Respect_Cooldown_Period()
    {
        var now = DateTimeOffset.UtcNow;
        var cooldown = TimeSpan.FromHours(1);
        var connection = WhatsAppConnection.Create(Guid.NewGuid(), "session-1", "qr-1").Value!;
        connection.MarkDisconnected("Lost", now);

        // Primeiro envio: nunca enviou antes -> deve permitir
        Assert.True(connection.ShouldSendAlert(cooldown, now));

        // Registra envio
        connection.RecordAlertDispatched(now);
        Assert.Equal(1, connection.AlertCount);
        Assert.Equal(now, connection.LastAlertSentAtUtc);

        // Tentativa 10 minutos depois -> deve bloquear devido ao cooldown
        Assert.False(connection.ShouldSendAlert(cooldown, now.AddMinutes(10)));

        // Tentativa 61 minutos depois -> deve permitir
        Assert.True(connection.ShouldSendAlert(cooldown, now.AddMinutes(61)));
    }

    [Fact]
    public void WhatsAppConnectionIncident_Create_Should_Succeed_With_Valid_Parameters()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var result = WhatsAppConnectionIncident.Create(
            tenantId,
            "session-abc",
            WhatsAppIncidentType.Disconnected,
            "Battery low or phone unreachable",
            alertDispatched: true,
            recipientEmail: "owner@carwash.com",
            occurredAtUtc: now);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal("session-abc", result.Value.ProviderSessionId);
        Assert.Equal(WhatsAppIncidentType.Disconnected, result.Value.Type);
        Assert.Equal("Battery low or phone unreachable", result.Value.Reason);
        Assert.True(result.Value.AlertDispatched);
        Assert.Equal("owner@carwash.com", result.Value.RecipientEmail);
        Assert.Equal(now, result.Value.OccurredAtUtc);
    }
}
