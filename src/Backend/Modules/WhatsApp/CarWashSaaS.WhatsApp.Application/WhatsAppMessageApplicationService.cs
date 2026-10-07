using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class WhatsAppMessageApplicationService(
    IWhatsAppConnectionRepository connectionRepository,
    IOutboundWhatsAppMessageRepository messageRepository,
    ITenantWhatsAppQuotaRepository quotaRepository,
    ICustomerCommunicationPreferenceRepository preferenceRepository,
    IBackgroundQueue backgroundQueue,
    ITenantPlanQuotaLookup? planQuotaLookup = null) : IOutboundWhatsAppDispatcher
{
    public async Task<Result<WhatsAppMessageDto>> SendTestMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string messageText,
        CancellationToken ct = default)
    {
        return await DispatchTextMessageAsync(tenantId, recipientPhone, messageText, idempotencyKey: null, ct: ct);
    }

    public async Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string messageText,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var idempotency = await ResolveIdempotencyAsync(tenantId, idempotencyKey, ct);
        if (idempotency.Existing is not null)
        {
            return Result<WhatsAppMessageDto>.Success(MapToDto(idempotency.Existing));
        }

        idempotencyKey = idempotency.Key;

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
        if (planQuotaLookup is not null)
        {
            var planQuotaCheck = await planQuotaLookup.CheckWhatsAppQuotaAsync(tenantId, ct);
            if (!planQuotaCheck.IsSuccess)
            {
                return Result<WhatsAppMessageDto>.Failure(planQuotaCheck.Error!);
            }
            if (!planQuotaCheck.Value!.CanProceed)
            {
                return Result<WhatsAppMessageDto>.Failure(new Error(
                    "tenant.quota.whatsapp_exceeded",
                    planQuotaCheck.Value.Reason ?? "Limite mensal de mensagens WhatsApp do plano atingido ou conta com restrição financeira.",
                    ErrorType.Conflict));
            }
        }

        var quota = await quotaRepository.GetOrCreateAsync(tenantId, ct);
        if (!quota.CanSend())
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.quota_exceeded", "Sending rate limit or daily quota exceeded for this store.", ErrorType.Conflict));
        }

        // 4. Criar mensagem de domínio
        var messageResult = OutboundWhatsAppMessage.Create(tenantId, recipientPhone, messageText, idempotencyKey);
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

        if (planQuotaLookup is not null)
        {
            await planQuotaLookup.ConsumeWhatsAppQuotaAsync(tenantId, ct);
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

    public Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string caption,
        string mediaType,
        string mediaUrlOrBase64,
        string mediaMimeType,
        string mediaFileName,
        string? idempotencyKey = null,
        CancellationToken ct = default)
        => SendMediaMessageAsync(tenantId, recipientPhone, caption, mediaType, mediaUrlOrBase64, mediaMimeType, mediaFileName, idempotencyKey, ct);

    public async Task<Result<WhatsAppMessageDto>> SendMediaMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string caption,
        string mediaType,
        string mediaUrlOrBase64,
        string mediaMimeType,
        string mediaFileName,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppMessageDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var idempotency = await ResolveIdempotencyAsync(tenantId, idempotencyKey, ct);
        if (idempotency.Existing is not null)
        {
            return Result<WhatsAppMessageDto>.Success(MapToDto(idempotency.Existing));
        }

        idempotencyKey = idempotency.Key;

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

        // 4. Criar mensagem com mídia de domínio
        var messageResult = OutboundWhatsAppMessage.CreateWithMedia(
            tenantId,
            recipientPhone,
            caption,
            mediaType,
            mediaUrlOrBase64,
            mediaMimeType,
            mediaFileName,
            idempotencyKey);

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

    // Chave já usada: devolve a mensagem existente, exceto se falhou (permite reenvio com nova chave).
    private async Task<(OutboundWhatsAppMessage? Existing, string? Key)> ResolveIdempotencyAsync(
        Guid tenantId, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return (null, idempotencyKey);
        }

        var key = idempotencyKey.Trim();
        var existing = await messageRepository.GetByIdempotencyKeyAsync(tenantId, key, ct);
        if (existing is null)
        {
            return (null, key);
        }

        if (existing.Status is WhatsAppMessageStatus.Failed or WhatsAppMessageStatus.Rejected)
        {
            return (null, $"{key}-retry-{Guid.CreateVersion7():N}"[..Math.Min(128, key.Length + 7 + 32)]);
        }

        return (existing, key);
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
        message.DeliveredAtUtc,
        message.MediaType,
        message.MediaFileName);
}

