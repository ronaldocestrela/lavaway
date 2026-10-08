using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class TenantPaymentGatewayConfig : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Provider { get; private set; } = PaymentGatewayProviderConstants.PagarMe;
    public string? PagarMeSecretKeyEncrypted { get; private set; }
    public string? PagarMePublicKey { get; private set; }
    public string? PagarMeWebhookSecretEncrypted { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastTestedAtUtc { get; private set; }
    public bool? LastTestSuccess { get; private set; }
    public string? LastTestMessage { get; private set; }

    private TenantPaymentGatewayConfig()
    {
    }

    public static Result<TenantPaymentGatewayConfig> Create(
        Guid tenantId,
        string provider,
        string? pagarMeSecretKeyEncrypted = null,
        string? pagarMePublicKey = null,
        string? pagarMeWebhookSecretEncrypted = null,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantPaymentGatewayConfig>.Failure(new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (!PaymentGatewayProviderConstants.IsValid(provider))
        {
            return Result<TenantPaymentGatewayConfig>.Failure(new Error("billing.invalid_provider", $"Provedor de pagamento '{provider}' é inválido.", ErrorType.Validation));
        }

        var config = new TenantPaymentGatewayConfig
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Provider = provider.Trim(),
            PagarMeSecretKeyEncrypted = pagarMeSecretKeyEncrypted?.Trim(),
            PagarMePublicKey = pagarMePublicKey?.Trim(),
            PagarMeWebhookSecretEncrypted = pagarMeWebhookSecretEncrypted?.Trim(),
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return Result<TenantPaymentGatewayConfig>.Success(config);
    }

    public Result Update(
        string provider,
        string? pagarMeSecretKeyEncrypted,
        string? pagarMePublicKey,
        string? pagarMeWebhookSecretEncrypted,
        bool isActive)
    {
        if (!PaymentGatewayProviderConstants.IsValid(provider))
        {
            return Result.Failure(new Error("billing.invalid_provider", $"Provedor de pagamento '{provider}' é inválido.", ErrorType.Validation));
        }

        Provider = provider.Trim();

        if (pagarMeSecretKeyEncrypted is not null)
        {
            PagarMeSecretKeyEncrypted = string.IsNullOrWhiteSpace(pagarMeSecretKeyEncrypted) ? null : pagarMeSecretKeyEncrypted.Trim();
        }

        PagarMePublicKey = string.IsNullOrWhiteSpace(pagarMePublicKey) ? null : pagarMePublicKey.Trim();

        if (pagarMeWebhookSecretEncrypted is not null)
        {
            PagarMeWebhookSecretEncrypted = string.IsNullOrWhiteSpace(pagarMeWebhookSecretEncrypted) ? null : pagarMeWebhookSecretEncrypted.Trim();
        }

        IsActive = isActive;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public void RecordTestResult(bool success, string message)
    {
        LastTestedAtUtc = DateTimeOffset.UtcNow;
        LastTestSuccess = success;
        LastTestMessage = string.IsNullOrWhiteSpace(message) ? (success ? "Conexão validada com sucesso." : "Falha na validação.") : message.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
