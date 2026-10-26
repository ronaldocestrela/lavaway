using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class TenantWhatsAppConfig : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string? EvolutionApiKeyEncrypted { get; private set; }
    public string? WebhookSecretEncrypted { get; private set; }
    public string? InstanceCustomName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastTestedAtUtc { get; private set; }
    public bool? LastTestSuccess { get; private set; }
    public string? LastTestMessage { get; private set; }

    private TenantWhatsAppConfig()
    {
    }

    public static Result<TenantWhatsAppConfig> Create(
        Guid tenantId,
        string? evolutionApiKeyEncrypted = null,
        string? webhookSecretEncrypted = null,
        string? instanceCustomName = null,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantWhatsAppConfig>.Failure(new Error("whatsapp.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var config = new TenantWhatsAppConfig
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            EvolutionApiKeyEncrypted = string.IsNullOrWhiteSpace(evolutionApiKeyEncrypted) ? null : evolutionApiKeyEncrypted.Trim(),
            WebhookSecretEncrypted = string.IsNullOrWhiteSpace(webhookSecretEncrypted) ? null : webhookSecretEncrypted.Trim(),
            InstanceCustomName = string.IsNullOrWhiteSpace(instanceCustomName) ? null : instanceCustomName.Trim(),
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        return Result<TenantWhatsAppConfig>.Success(config);
    }

    public Result Update(
        string? evolutionApiKeyEncrypted,
        string? webhookSecretEncrypted,
        string? instanceCustomName,
        bool isActive)
    {
        if (evolutionApiKeyEncrypted is not null)
        {
            EvolutionApiKeyEncrypted = string.IsNullOrWhiteSpace(evolutionApiKeyEncrypted) ? null : evolutionApiKeyEncrypted.Trim();
        }

        if (webhookSecretEncrypted is not null)
        {
            WebhookSecretEncrypted = string.IsNullOrWhiteSpace(webhookSecretEncrypted) ? null : webhookSecretEncrypted.Trim();
        }

        InstanceCustomName = string.IsNullOrWhiteSpace(instanceCustomName) ? null : instanceCustomName.Trim();
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
