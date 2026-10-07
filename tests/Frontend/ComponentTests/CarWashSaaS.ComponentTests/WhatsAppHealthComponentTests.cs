using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class WhatsAppHealthComponentTests : BunitContext
{
    [Fact]
    public void WhatsAppDisconnectedAlertBanner_ShouldRender_WhenIsVisible_AndTriggerCallbackOnReconnect()
    {
        var reconnectTriggered = false;

        var cut = Render<WhatsAppDisconnectedAlertBanner>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.DisconnectReason, "Sessão revogada pelo WhatsApp")
            .Add(p => p.OnReconnectRequested, () => reconnectTriggered = true));

        Assert.Contains("WhatsApp Desconectado", cut.Markup);
        Assert.Contains("Sessão revogada pelo WhatsApp", cut.Markup);
        Assert.Contains("A conexão com o WhatsApp da sua loja foi interrompida", cut.Markup);

        var button = cut.Find("button");
        Assert.NotNull(button);
        button.Click();

        Assert.True(reconnectTriggered);
    }

    [Fact]
    public void WhatsAppDisconnectedAlertBanner_ShouldNotRender_WhenIsVisibleIsFalse()
    {
        var cut = Render<WhatsAppDisconnectedAlertBanner>(parameters => parameters
            .Add(p => p.IsVisible, false));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void WhatsAppInstanceDiagnosticModal_ShouldRenderDetails_AndTriggerActions()
    {
        var tenantId = Guid.NewGuid();
        var instance = new PlatformWhatsAppInstanceItemDto(
            TenantId: tenantId,
            TenantName: "Estética Automotiva Premium",
            ProviderSessionId: "lavaway-session-test",
            Status: "disconnected",
            LastConnectedAtUtc: DateTimeOffset.UtcNow.AddDays(-2),
            LastDisconnectedAtUtc: DateTimeOffset.UtcNow.AddHours(-1),
            DisconnectReason: "Timeout no socket",
            HasActiveAlert: true,
            AlertCount: 2,
            LastAlertSentAtUtc: DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        Guid? probedTenantId = null;
        Guid? alertedTenantId = null;

        var cut = Render<WhatsAppInstanceDiagnosticModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Instance, instance)
            .Add(p => p.OnRunProbe, id => probedTenantId = id)
            .Add(p => p.OnTriggerAlert, id => alertedTenantId = id));

        Assert.Contains("Estética Automotiva Premium", cut.Markup);
        Assert.Contains("lavaway-session-test", cut.Markup);
        Assert.Contains("Timeout no socket", cut.Markup);
        Assert.Contains("Desconectado", cut.Markup);

        // Dispara probe
        var probeButton = cut.Find("button:contains('Executar Teste de Conexão')");
        probeButton.Click();
        Assert.Equal(tenantId, probedTenantId);

        // Dispara alerta
        var alertButton = cut.Find("button:contains('Reenviar Alerta de QR Code')");
        alertButton.Click();
        Assert.Equal(tenantId, alertedTenantId);
    }

    [Fact]
    public void WhatsAppInstanceDiagnosticModal_ShouldNotRender_WhenClosed()
    {
        var cut = Render<WhatsAppInstanceDiagnosticModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }
}
