using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class OutboundWhatsAppMessage : IMustHaveTenant
{
    private readonly List<WhatsAppDeliveryAttempt> _attempts = [];

    private OutboundWhatsAppMessage()
    {
    }

    private OutboundWhatsAppMessage(
        Guid id,
        Guid tenantId,
        string recipientPhone,
        string body,
        string idempotencyKey,
        string? mediaType = null,
        string? mediaUrlOrBase64 = null,
        string? mediaMimeType = null,
        string? mediaFileName = null)
    {
        Id = id;
        TenantId = tenantId;
        RecipientPhone = recipientPhone;
        Body = body;
        IdempotencyKey = idempotencyKey;
        Status = WhatsAppMessageStatus.Queued;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        MediaType = mediaType;
        MediaUrlOrBase64 = mediaUrlOrBase64;
        MediaMimeType = mediaMimeType;
        MediaFileName = mediaFileName;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string RecipientPhone { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public WhatsAppMessageStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? FailureReason { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public string? MediaType { get; private set; }
    public string? MediaUrlOrBase64 { get; private set; }
    public string? MediaMimeType { get; private set; }
    public string? MediaFileName { get; private set; }

    public IReadOnlyCollection<WhatsAppDeliveryAttempt> DeliveryAttempts => _attempts.AsReadOnly();


    public static Result<OutboundWhatsAppMessage> Create(
        Guid tenantId,
        string recipientPhone,
        string body,
        string? idempotencyKey = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.recipient.required", "Recipient phone is required.", ErrorType.Validation));
        }

        var cleanedPhone = CleanPhoneNumber(recipientPhone);
        if (cleanedPhone.Length < 10 || cleanedPhone.Length > 20)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.recipient.invalid", "Recipient phone must contain between 10 and 20 digits.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > 4096)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.body.invalid", "Message body must be between 1 and 4096 characters.", ErrorType.Validation));
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? Guid.CreateVersion7().ToString("N")
            : idempotencyKey.Trim();

        return Result<OutboundWhatsAppMessage>.Success(new OutboundWhatsAppMessage(
            Guid.CreateVersion7(),
            tenantId,
            cleanedPhone,
            body.Trim(),
            key));
    }

    public static Result<OutboundWhatsAppMessage> CreateWithMedia(
        Guid tenantId,
        string recipientPhone,
        string caption,
        string mediaType,
        string mediaUrlOrBase64,
        string mediaMimeType,
        string mediaFileName,
        string? idempotencyKey = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.recipient.required", "Recipient phone is required.", ErrorType.Validation));
        }

        var cleanedPhone = CleanPhoneNumber(recipientPhone);
        if (cleanedPhone.Length < 10 || cleanedPhone.Length > 20)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.recipient.invalid", "Recipient phone must contain between 10 and 20 digits.", ErrorType.Validation));
        }

        var normalizedMediaType = (mediaType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedMediaType is not ("document" or "image"))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.media_type.invalid", "Media type must be 'document' or 'image'.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(mediaUrlOrBase64))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.media_content.required", "Media content (URL or Base64) is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(mediaMimeType))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.media_mimetype.required", "Media MIME type is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(mediaFileName))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.media_filename.required", "Media file name is required.", ErrorType.Validation));
        }

        var normalizedCaption = caption?.Trim() ?? string.Empty;
        if (normalizedCaption.Length > 4096)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.body.invalid", "Caption must not exceed 4096 characters.", ErrorType.Validation));
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? Guid.CreateVersion7().ToString("N")
            : idempotencyKey.Trim();

        return Result<OutboundWhatsAppMessage>.Success(new OutboundWhatsAppMessage(
            Guid.CreateVersion7(),
            tenantId,
            cleanedPhone,
            normalizedCaption,
            key,
            normalizedMediaType,
            mediaUrlOrBase64.Trim(),
            mediaMimeType.Trim(),
            mediaFileName.Trim()));
    }


    public Result<OutboundWhatsAppMessage> MarkSending()
    {
        if (Status is WhatsAppMessageStatus.Sent or WhatsAppMessageStatus.Delivered or WhatsAppMessageStatus.Read)
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.invalid_state", "Message has already been sent.", ErrorType.Validation));
        }

        Status = WhatsAppMessageStatus.Sending;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public Result<OutboundWhatsAppMessage> MarkSent(string providerMessageId)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
        {
            return Result<OutboundWhatsAppMessage>.Failure(new Error("whatsapp.provider_message_id.required", "Provider message ID is required.", ErrorType.Validation));
        }

        ProviderMessageId = providerMessageId.Trim();
        Status = WhatsAppMessageStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
        UpdatedAt = SentAtUtc.Value;
        AttemptCount++;
        _attempts.Add(new WhatsAppDeliveryAttempt(AttemptCount, isSuccess: true, errorCode: null, errorMessage: null, httpStatusCode: 200));

        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public Result<OutboundWhatsAppMessage> MarkDelivered()
    {
        if (Status == WhatsAppMessageStatus.Read)
        {
            return Result<OutboundWhatsAppMessage>.Success(this);
        }

        Status = WhatsAppMessageStatus.Delivered;
        DeliveredAtUtc = DateTimeOffset.UtcNow;
        UpdatedAt = DeliveredAtUtc.Value;

        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public Result<OutboundWhatsAppMessage> MarkRead()
    {
        Status = WhatsAppMessageStatus.Read;
        ReadAtUtc = DateTimeOffset.UtcNow;
        DeliveredAtUtc ??= ReadAtUtc;
        UpdatedAt = ReadAtUtc.Value;

        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public Result<OutboundWhatsAppMessage> RecordAttemptFailure(string errorCode, string errorMessage, int? httpStatusCode, bool isTerminal = false)
    {
        AttemptCount++;
        _attempts.Add(new WhatsAppDeliveryAttempt(AttemptCount, isSuccess: false, errorCode, errorMessage, httpStatusCode));

        if (isTerminal)
        {
            Status = WhatsAppMessageStatus.Failed;
            FailureReason = $"{errorCode}: {errorMessage}";
        }
        else
        {
            Status = WhatsAppMessageStatus.Queued;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public Result<OutboundWhatsAppMessage> MarkRejected(string reason)
    {
        Status = WhatsAppMessageStatus.Rejected;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<OutboundWhatsAppMessage>.Success(this);
    }

    public static string CleanPhoneNumber(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
