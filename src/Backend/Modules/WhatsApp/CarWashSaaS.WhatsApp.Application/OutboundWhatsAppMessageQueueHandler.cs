using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class OutboundWhatsAppMessageQueueHandler(
    IOutboundWhatsAppMessageRepository messageRepository,
    IWhatsAppMessageSender messageSender,
    ILogger<OutboundWhatsAppMessageQueueHandler> logger) : ITenantQueueMessageHandler
{
    public const string WhatsAppMessageDispatchEventType = "whatsapp.message.dispatch";

    public string EventType => WhatsAppMessageDispatchEventType;

    public async Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.Payload, out var messageId))
        {
            messageId = message.MessageId;
        }

        var outboundMessage = await messageRepository.GetByIdAsync(messageId, cancellationToken);
        if (outboundMessage is null)
        {
            logger.LogWarning("Outbound WhatsApp message {MessageId} not found for tenant {TenantId}.", messageId, message.TenantId);
            return;
        }

        if (outboundMessage.Status is WhatsAppMessageStatus.Sent or WhatsAppMessageStatus.Delivered or WhatsAppMessageStatus.Read or WhatsAppMessageStatus.Failed or WhatsAppMessageStatus.Rejected)
        {
            logger.LogInformation("Outbound WhatsApp message {MessageId} is already in state {Status}. Skipping dispatch.", messageId, outboundMessage.Status);
            return;
        }

        outboundMessage.MarkSending();
        await messageRepository.SaveChangesAsync(cancellationToken);

        var sendResult = !string.IsNullOrWhiteSpace(outboundMessage.MediaType) && !string.IsNullOrWhiteSpace(outboundMessage.MediaUrlOrBase64)
            ? await messageSender.SendMediaMessageAsync(
                outboundMessage.TenantId,
                outboundMessage.RecipientPhone,
                outboundMessage.MediaUrlOrBase64,
                outboundMessage.MediaType,
                outboundMessage.MediaMimeType ?? "application/octet-stream",
                outboundMessage.MediaFileName ?? "document.pdf",
                outboundMessage.Body,
                cancellationToken)
            : await messageSender.SendTextMessageAsync(
                outboundMessage.TenantId,
                outboundMessage.RecipientPhone,
                outboundMessage.Body,
                cancellationToken);

        if (sendResult.IsSuccess)
        {
            outboundMessage.MarkSent(sendResult.Value!);
            await messageRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Outbound WhatsApp message {MessageId} successfully sent with provider ID {ProviderId}.", messageId, sendResult.Value);
        }
        else
        {
            var isTerminal = sendResult.Error!.Type is ErrorType.Validation or ErrorType.NotFound;
            outboundMessage.RecordAttemptFailure(sendResult.Error.Code, sendResult.Error.Description, httpStatusCode: null, isTerminal: isTerminal);
            await messageRepository.SaveChangesAsync(cancellationToken);

            logger.LogError("Failed to send WhatsApp message {MessageId}: {ErrorCode} - {Description}",
                messageId, sendResult.Error.Code, sendResult.Error.Description);

            if (!isTerminal)
            {
                throw new InvalidOperationException($"Transient failure sending WhatsApp message: {sendResult.Error.Description}");
            }
        }
    }
}
