using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class WhatsAppConnection : IMustHaveTenant
{
    private WhatsAppConnection()
    {
    }

    private WhatsAppConnection(Guid id, Guid tenantId, string providerSessionId, string qrCodeValue)
    {
        Id = id;
        TenantId = tenantId;
        ProviderSessionId = providerSessionId;
        QrCodeValue = qrCodeValue;
        Status = WhatsAppConnectionStatus.Connecting;
        UpdatedAt = DateTimeOffset.UtcNow;
        CreatedAt = UpdatedAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string ProviderSessionId { get; private set; } = string.Empty;
    public string QrCodeValue { get; private set; } = string.Empty;
    public WhatsAppConnectionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<WhatsAppConnection> Create(Guid tenantId, string providerSessionId, string qrCodeValue)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(providerSessionId) || providerSessionId.Trim().Length > 200)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_session.invalid", "A valid provider session is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(qrCodeValue) || qrCodeValue.Trim().Length > 2000)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.qr_code.invalid", "A valid QR code payload is required.", ErrorType.Validation));
        }

        return Result<WhatsAppConnection>.Success(new WhatsAppConnection(
            Guid.CreateVersion7(),
            tenantId,
            providerSessionId.Trim(),
            qrCodeValue.Trim()));
    }

    public Result<WhatsAppConnection> Refresh(string qrCodeValue)
    {
        if (string.IsNullOrWhiteSpace(qrCodeValue) || qrCodeValue.Trim().Length > 2000)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.qr_code.invalid", "A valid QR code payload is required.", ErrorType.Validation));
        }

        QrCodeValue = qrCodeValue.Trim();
        Status = WhatsAppConnectionStatus.Connecting;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<WhatsAppConnection>.Success(this);
    }

    public Result<WhatsAppConnection> RefreshSession(string providerSessionId, string qrCodeValue)
    {
        if (string.IsNullOrWhiteSpace(providerSessionId) || providerSessionId.Trim().Length > 200)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_session.invalid", "A valid provider session is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(qrCodeValue) || qrCodeValue.Trim().Length > 2000)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.qr_code.invalid", "A valid QR code payload is required.", ErrorType.Validation));
        }

        ProviderSessionId = providerSessionId.Trim();
        QrCodeValue = qrCodeValue.Trim();
        Status = WhatsAppConnectionStatus.Connecting;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<WhatsAppConnection>.Success(this);
    }

    public Result<WhatsAppConnection> MarkConnected()
    {
        Status = WhatsAppConnectionStatus.Connected;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<WhatsAppConnection>.Success(this);
    }

    public Result<WhatsAppConnection> MarkDisconnected()
    {
        Status = WhatsAppConnectionStatus.Disconnected;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<WhatsAppConnection>.Success(this);
    }
}
