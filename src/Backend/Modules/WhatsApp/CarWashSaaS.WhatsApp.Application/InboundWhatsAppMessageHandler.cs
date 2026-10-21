using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class InboundWhatsAppMessageHandler(
    ChatbotConversationEngine chatbotEngine,
    ILogger<InboundWhatsAppMessageHandler> logger,
    ITenantPlanQuotaLookup? quotaLookup = null,
    IOutboundWhatsAppDispatcher? dispatcher = null,
    ITenantStoreProfileLookup? storeProfileLookup = null) : ITenantQueueMessageHandler
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

        if (quotaLookup is not null)
        {
            var aiCheck = await quotaLookup.HasFeatureAsync(message.TenantId, SaasFeature.AiChatbot, cancellationToken);
            if (aiCheck.IsSuccess && !aiCheck.Value)
            {
                logger.LogInformation("Tenant {TenantId} does not have AiChatbot enabled. Sending fallback message.", message.TenantId);

                if (dispatcher is not null)
                {
                    string storeName = "nosso estabelecimento";
                    if (storeProfileLookup is not null)
                    {
                        var profileResult = await storeProfileLookup.GetProfileAsync(message.TenantId, cancellationToken);
                        if (profileResult.IsSuccess && !string.IsNullOrWhiteSpace(profileResult.Value?.TradeName))
                        {
                            storeName = profileResult.Value.TradeName;
                        }
                    }

                    var fallbackText = $"Olá! Agradecemos o seu contato com {storeName}. No momento nosso atendimento digital automatizado não está ativo. Um de nossos atendentes humanos responderá sua mensagem em breve! 🚗✨";
                    await dispatcher.DispatchTextMessageAsync(message.TenantId, payload.SenderPhone, fallbackText, null, cancellationToken);
                }

                return;
            }
        }

        logger.LogInformation("Processing inbound WhatsApp message from {Phone} for tenant {TenantId}.", payload.SenderPhone, message.TenantId);

        try
        {
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing chatbot inbound message for {Phone} in tenant {TenantId}.",
                payload.SenderPhone, message.TenantId);
        }
    }
}
