using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class WhatsAppMessageApplicationService(
    IWhatsAppConnectionRepository connectionRepository,
    IOutboundWhatsAppMessageRepository messageRepository,
    ITenantWhatsAppQuotaRepository quotaRepository,
    ICustomerCommunicationPreferenceRepository preferenceRepository,
    IBackgroundQueue backgroundQueue)
{
    public async Task<Result<WhatsAppMessageDto>> SendTestMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string messageText,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        // 1. Validar conexão ativa
        var connection = await connectionRepository.GetByTenantAsync(tenantId, ct);
        if (connection is null || connection.Status != WhatsAppConnectionStatus.Connected)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.not_connected", "WhatsApp instance is not connected. Please pair your WhatsApp first.", ErrorType.Validation));
        }

        // 2. Validar consentimento do destinatário
        var normalizedPhone = OutboundWhatsAppMessage.CleanPhoneNumber(recipientPhone);
        var preference = await preferenceRepository.GetByPhoneAsync(tenantId, normalizedPhone, ct);
        if (preference is not null && !preference.IsOptedIn)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.recipient.opted_out", "The recipient has opted out of WhatsApp messages.", ErrorType.Validation));
        }

        // 3. Validar cota / rate-limit
        var quota = await quotaRepository.GetOrCreateAsync(tenantId, ct);
        if (!quota.CanSend())
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.quota_exceeded", "Sending rate limit or daily quota exceeded for this store.", ErrorType.Conflict));
        }

        // 4. Criar mensagem de domínio
        var messageResult = OutboundWhatsAppMessage.Create(tenantId, recipientPhone, messageText);
        if (!messageResult.IsSuccess)
        {
            return Result<WhatsAppMessageDto>.Failure(messageResult.Error!);
        }

        var message = messageResult.Value!;

        // 5. Registrar débito na cota e persistir
        var quotaRecordResult = quota.RecordSend();
        if (!quotaRecordResult.IsSuccess)
        {
            return Result<WhatsAppMessageDto>.Failure(quotaRecordResult.Error!);
        }

        await quotaRepository.SaveChangesAsync(ct);
        await messageRepository.AddAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);

        // 6. Enfileirar no RabbitMQ para processamento assíncrono confiável
        var queueMessage = new TenantQueueMessage(
            tenantId,
            "whatsapp.message.dispatch",
            message.Id.ToString("D"),
            message.Id);

        await backgroundQueue.EnqueueAsync(queueMessage, ct);

        return Result<WhatsAppMessageDto>.Success(MapToDto(message));
    }

    public async Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(
        Guid tenantId,
        int count = 20,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<WhatsAppMessageDto>>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var messages = await messageRepository.GetRecentAsync(tenantId, Math.Clamp(count, 1, 100), ct);
        var dtos = messages.Select(MapToDto).ToList();
        return Result<IReadOnlyList<WhatsAppMessageDto>>.Success(dtos);
    }

    public async Task<Result<WhatsAppMessageDto>> GetMessageByIdAsync(
        Guid tenantId,
        Guid messageId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var message = await messageRepository.GetByIdAsync(messageId, ct);
        if (message is null || message.TenantId != tenantId)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.message.not_found", "Message not found.", ErrorType.NotFound));
        }

        return Result<WhatsAppMessageDto>.Success(MapToDto(message));
    }

    public async Task<Result<WhatsAppQuotaDto>> GetQuotaAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppQuotaDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var quota = await quotaRepository.GetOrCreateAsync(tenantId, ct);
        quota.SyncWindows();

        var dto = new WhatsAppQuotaDto(
            quota.SentToday,
            quota.MaxMessagesPerDay,
            quota.SentInCurrentMinute,
            quota.MaxMessagesPerMinute,
            !quota.CanSend());

        return Result<WhatsAppQuotaDto>.Success(dto);
    }

    public async Task<Result<bool>> ProcessDeliveryWebhookAsync(
        Guid tenantId,
        string providerMessageId,
        string status,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(providerMessageId))
        {
            return Result<bool>.Success(false);
        }

        var message = await messageRepository.GetByProviderMessageIdAsync(tenantId, providerMessageId, ct);
        if (message is null)
        {
            // Webhook para mensagem não gerenciada ou já tratada
            return Result<bool>.Success(false);
        }

        var normalizedStatus = status.ToLowerInvariant().Trim();
        if (normalizedStatus is "delivered" or "delivery")
        {
            message.MarkDelivered();
            await messageRepository.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }

        if (normalizedStatus is "read" or "viewed")
        {
            message.MarkRead();
            await messageRepository.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }

        return Result<bool>.Success(false);
    }

    public static WhatsAppMessageDto MapToDto(OutboundWhatsAppMessage message) => new(
        message.Id,
        message.RecipientPhone,
        message.Body,
        message.Status.ToString().ToLowerInvariant(),
        message.FailureReason,
        message.AttemptCount,
        message.CreatedAt,
        message.SentAtUtc,
        message.DeliveredAtUtc);
}
