using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class ReactivationCampaignLog : IMustHaveTenant
{
    private ReactivationCampaignLog()
    {
    }

    private ReactivationCampaignLog(
        Guid id,
        Guid tenantId,
        Guid campaignRuleId,
        Guid customerId,
        string customerPhone,
        int daysInactive,
        string idempotencyKey,
        DateTimeOffset sentAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        CampaignRuleId = campaignRuleId;
        CustomerId = customerId;
        CustomerPhone = customerPhone;
        DaysInactive = daysInactive;
        IdempotencyKey = idempotencyKey;
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public Guid CampaignRuleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerPhone { get; private set; } = string.Empty;
    public int DaysInactive { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; }

    public static Result<ReactivationCampaignLog> Create(
        Guid tenantId,
        Guid campaignRuleId,
        Guid customerId,
        string customerPhone,
        int daysInactive,
        string idempotencyKey,
        DateTimeOffset? sentAtUtc = null)
    {
        if (tenantId == Guid.Empty || campaignRuleId == Guid.Empty || customerId == Guid.Empty)
        {
            return Result<ReactivationCampaignLog>.Failure(new Error("campaign_log.required_fields", "Tenant, regra e cliente são obrigatórios.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            return Result<ReactivationCampaignLog>.Failure(new Error("campaign_log.phone_required", "Telefone do cliente é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<ReactivationCampaignLog>.Failure(new Error("campaign_log.idempotency_required", "Chave de idempotência é obrigatória.", ErrorType.Validation));
        }

        return Result<ReactivationCampaignLog>.Success(new ReactivationCampaignLog(
            Guid.CreateVersion7(),
            tenantId,
            campaignRuleId,
            customerId,
            customerPhone.Trim(),
            daysInactive,
            idempotencyKey.Trim(),
            sentAtUtc ?? DateTimeOffset.UtcNow));
    }
}
