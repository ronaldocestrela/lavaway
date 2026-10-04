using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class WhatsAppMessagingComponentTests : BunitContext
{
    [Fact]
    public void WhatsAppTestMessageCard_ShouldRenderDisabled_WhenNotConnected()
    {
        var cut = Render<WhatsAppTestMessageCard>(parameters => parameters
            .Add(p => p.IsConnected, false));

        Assert.Contains("WhatsApp Desconectado", cut.Find(".channel-badge").TextContent);
        Assert.NotNull(cut.Find(".test-disabled-banner"));
        Assert.True(cut.Find("#btnSendWhatsAppTest").HasAttribute("disabled"));
    }

    [Fact]
    public void WhatsAppTestMessageCard_ShouldTriggerSend_WhenValidAndConnected()
    {
        SendWhatsAppTestMessageRequest? sentRequest = null;

        var cut = Render<WhatsAppTestMessageCard>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnSendTestMessage, request => sentRequest = request));

        Assert.Contains("Canal Conectado", cut.Find(".channel-badge").TextContent);

        // Digita número válido
        var phoneInput = cut.Find("#recipientPhone");
        phoneInput.Input("11988887777");

        var sendButton = cut.Find("#btnSendWhatsAppTest");
        Assert.False(sendButton.HasAttribute("disabled"));

        sendButton.Click();

        Assert.NotNull(sentRequest);
        Assert.Equal("11988887777", sentRequest.RecipientPhone);
        Assert.False(string.IsNullOrWhiteSpace(sentRequest.MessageText));
    }

    [Fact]
    public void WhatsAppQuotaMeter_ShouldDisplayCountersAndThrottlingStatus()
    {
        var quota = new WhatsAppQuotaDto(
            MessagesSentToday: 150,
            DailyLimit: 500,
            MessagesSentCurrentMinute: 18,
            MinuteLimit: 20,
            IsThrottled: false);

        var cut = Render<WhatsAppQuotaMeter>(parameters => parameters
            .Add(p => p.Quota, quota));

        Assert.Contains("150 / 500", cut.Markup);
        Assert.Contains("18 / 20", cut.Markup);
        Assert.Contains("Operação Normal", cut.Find(".throttle-badge").TextContent);
    }

    [Fact]
    public void WhatsAppMessageHistoryTable_ShouldRenderMessages_WithStatusBadges()
    {
        var messages = new List<WhatsAppMessageDto>
        {
            new(Guid.NewGuid(), "11999998888", "Mensagem 1", "sent", null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null),
            new(Guid.NewGuid(), "11977776666", "Mensagem 2", "failed", "Invalid number", 2, DateTimeOffset.UtcNow, null, null)
        };

        var cut = Render<WhatsAppMessageHistoryTable>(parameters => parameters
            .Add(p => p.Messages, messages));

        Assert.Contains("(11) 99999-8888", cut.Markup);
        Assert.Contains("Enviado", cut.Markup);
        Assert.Contains("Falha", cut.Markup);
        Assert.Contains("Invalid number", cut.Markup);
    }
}
