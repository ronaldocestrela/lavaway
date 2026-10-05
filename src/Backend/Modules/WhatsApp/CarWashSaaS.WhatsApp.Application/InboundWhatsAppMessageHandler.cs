using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class InboundWhatsAppMessageHandler(
    ChatbotConversationEngine chatbotEngine,
    ILogger<InboundWhatsAppMessageHandler> logger) : ITenantQueueMessageHandler
{
    public const string InboundWhatsAppDispatchEventType = "whatsapp.inbound.dispatch";

    public string EventType => InboundWhatsAppDispatchEventType;

    public async Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
    {
        InboundWhatsAppReceivedEvent? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InboundWhatsAppReceivedEvent>(message.Payload);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize InboundWhatsAppReceivedEvent payload for tenant {TenantId}.", message.TenantId);
            return;
        }

        if (payload is null)
        {
            logger.LogWarning("Inbound WhatsApp message payload was null for tenant {TenantId}.", message.TenantId);
            return;
        }

        logger.LogInformation("Processing inbound WhatsApp message from {Phone} for tenant {TenantId}.", payload.SenderPhone, message.TenantId);

        var result = await chatbotEngine.ProcessIncomingMessageAsync(
            message.TenantId,
            payload.SenderPhone,
            payload.PushName,
            payload.MessageText,
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Chatbot engine returned failure for {Phone}: {ErrorCode} - {Description}",
                payload.SenderPhone, result.Error?.Code, result.Error?.Description);
        }
    }
}
