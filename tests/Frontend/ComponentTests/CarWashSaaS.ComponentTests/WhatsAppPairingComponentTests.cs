using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class WhatsAppPairingComponentTests : BunitContext
{
    [Fact]
    public void WhatsAppPairingCard_ShouldRenderDisconnectedState_WhenDisconnected()
    {
        var connection = new WhatsAppConnectionDto(WhatsAppStatusConstants.Disconnected);
        var startClicked = false;

        var cut = Render<WhatsAppPairingCard>(parameters => parameters
            .Add(p => p.Connection, connection)
            .Add(p => p.OnStartPairing, () => startClicked = true));

        Assert.Contains("Desconectado", cut.Find(".status-badge").TextContent);
        Assert.NotNull(cut.Find("#btn-start-pairing"));

        cut.Find("#btn-start-pairing").Click();
        Assert.True(startClicked);
    }

    [Fact]
    public void WhatsAppPairingCard_ShouldRenderConnectingStateAndQrCode_WhenConnecting()
    {
        var connection = new WhatsAppConnectionDto(
            WhatsAppStatusConstants.Connecting,
            QrCode: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
            UpdatedAt: DateTimeOffset.UtcNow);

        var refreshClicked = false;

        var cut = Render<WhatsAppPairingCard>(parameters => parameters
            .Add(p => p.Connection, connection)
            .Add(p => p.OnRefreshPairing, () => refreshClicked = true));

        Assert.Contains("Aguardando Leitura", cut.Find(".status-badge").TextContent);
        Assert.NotNull(cut.Find(".qr-code-wrapper"));
        Assert.NotNull(cut.Find(".pairing-steps"));
        Assert.NotNull(cut.Find("#btn-refresh-pairing"));

        cut.Find("#btn-refresh-pairing").Click();
        Assert.True(refreshClicked);
    }

    [Fact]
    public void WhatsAppPairingCard_ShouldRenderConnectedState_WhenConnected()
    {
        var connection = new WhatsAppConnectionDto(
            WhatsAppStatusConstants.Connected,
            UpdatedAt: DateTimeOffset.UtcNow);

        var disconnectClicked = false;

        var cut = Render<WhatsAppPairingCard>(parameters => parameters
            .Add(p => p.Connection, connection)
            .Add(p => p.OnDisconnect, () => disconnectClicked = true));

        Assert.Contains("Conectado", cut.Find(".status-badge").TextContent);
        Assert.NotNull(cut.Find("#btn-disconnect-pairing"));

        cut.Find("#btn-disconnect-pairing").Click();
        Assert.True(disconnectClicked);
    }

    [Fact]
    public void WhatsAppPairingCard_ShouldRenderErrorMessage_WhenProvided()
    {
        var cut = Render<WhatsAppPairingCard>(parameters => parameters
            .Add(p => p.ErrorMessage, "Falha de comunicação com o provedor."));

        Assert.Contains("Falha de comunicação com o provedor.", cut.Find(".alert-danger").TextContent);
    }
}
