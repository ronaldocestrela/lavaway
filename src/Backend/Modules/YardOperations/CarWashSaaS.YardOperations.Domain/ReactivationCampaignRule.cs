using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class ReactivationCampaignRule : IMustHaveTenant
{
    private ReactivationCampaignRule()
    {
    }

    private ReactivationCampaignRule(
        Guid id,
        Guid tenantId,
        int daysInactive,
        string title,
        string messageTemplate,
        bool isEnabled,
        string? promotionalOffer)
    {
        Id = id;
        TenantId = tenantId;
        DaysInactive = daysInactive;
        Title = title;
        MessageTemplate = messageTemplate;
        IsEnabled = isEnabled;
        PromotionalOffer = string.IsNullOrWhiteSpace(promotionalOffer) ? null : promotionalOffer.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public int DaysInactive { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string MessageTemplate { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public string? PromotionalOffer { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<ReactivationCampaignRule> Create(
        Guid tenantId,
        int daysInactive,
        string title,
        string messageTemplate,
        bool isEnabled = true,
        string? promotionalOffer = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (daysInactive <= 0)
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.days_invalid", "Dias de inatividade devem ser maiores que zero.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.title_required", "Título da régua de campanha é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.template_required", "Template da mensagem é obrigatório.", ErrorType.Validation));
        }

        return Result<ReactivationCampaignRule>.Success(new ReactivationCampaignRule(
            Guid.CreateVersion7(),
            tenantId,
            daysInactive,
            title.Trim(),
            messageTemplate.Trim(),
            isEnabled,
            promotionalOffer));
    }

    public Result<ReactivationCampaignRule> Update(
        string title,
        string messageTemplate,
        bool isEnabled,
        string? promotionalOffer)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.title_required", "Título da régua de campanha é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            return Result<ReactivationCampaignRule>.Failure(new Error("campaign_rule.template_required", "Template da mensagem é obrigatório.", ErrorType.Validation));
        }

        Title = title.Trim();
        MessageTemplate = messageTemplate.Trim();
        IsEnabled = isEnabled;
        PromotionalOffer = string.IsNullOrWhiteSpace(promotionalOffer) ? null : promotionalOffer.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result<ReactivationCampaignRule>.Success(this);
    }
}
