using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class TenantSaasSubscription : IMustHaveTenant
{
    private TenantSaasSubscription()
    {
    }

    private TenantSaasSubscription(
        Guid id,
        Guid tenantId,
        SaasPlanTier planTier,
        TenantSubscriptionStatus status,
        decimal monthlyPrice,
        DateTimeOffset currentPeriodStartUtc,
        DateTimeOffset currentPeriodEndUtc,
        DateTimeOffset? gracePeriodEndsAtUtc,
        DateTimeOffset? nextBillingDateUtc,
        string? gatewayCustomerId,
        string? gatewaySubscriptionId,
        string? statusReason)
    {
        Id = id;
        TenantId = tenantId;
        PlanTier = planTier;
        Status = status;
        MonthlyPrice = monthlyPrice;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        NextBillingDateUtc = nextBillingDateUtc;
        GatewayCustomerId = gatewayCustomerId;
        GatewaySubscriptionId = gatewaySubscriptionId;
        StatusReason = statusReason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public SaasPlanTier PlanTier { get; private set; }
    public TenantSubscriptionStatus Status { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public DateTimeOffset CurrentPeriodStartUtc { get; private set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; private set; }
    public DateTimeOffset? GracePeriodEndsAtUtc { get; private set; }
    public DateTimeOffset? NextBillingDateUtc { get; private set; }
    public string? GatewayCustomerId { get; private set; }
    public string? GatewaySubscriptionId { get; private set; }
    public string? StatusReason { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<TenantSaasSubscription> CreateTrial(Guid tenantId, DateTimeOffset? endsAt = null, SaasPlanTier defaultTier = SaasPlanTier.Pro)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSaasSubscription>.Failure(new Error("saas_subscription.tenant.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        var now = DateTimeOffset.UtcNow;
        var end = endsAt ?? now.AddDays(14);

        return Result<TenantSaasSubscription>.Success(new TenantSaasSubscription(
            Guid.CreateVersion7(),
            tenantId,
            defaultTier,
            TenantSubscriptionStatus.Trial,
            monthlyPrice: 0m,
            currentPeriodStartUtc: now,
            currentPeriodEndUtc: end,
            gracePeriodEndsAtUtc: null,
            nextBillingDateUtc: end,
            gatewayCustomerId: null,
            gatewaySubscriptionId: null,
            statusReason: "Período de avaliação gratuita de 14 dias."));
    }

    public static Result<TenantSaasSubscription> CreateActive(
        Guid tenantId,
        SaasPlan plan,
        string? gatewayCustomerId,
        string? gatewaySubscriptionId,
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantSaasSubscription>.Failure(new Error("saas_subscription.tenant.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        ArgumentNullException.ThrowIfNull(plan);

        var start = periodStart ?? DateTimeOffset.UtcNow;
        var end = periodEnd ?? start.AddMonths(1);

        return Result<TenantSaasSubscription>.Success(new TenantSaasSubscription(
            Guid.CreateVersion7(),
            tenantId,
            plan.Tier,
            TenantSubscriptionStatus.Active,
            monthlyPrice: plan.MonthlyPrice,
            currentPeriodStartUtc: start,
            currentPeriodEndUtc: end,
            gracePeriodEndsAtUtc: null,
            nextBillingDateUtc: end,
            gatewayCustomerId: gatewayCustomerId,
            gatewaySubscriptionId: gatewaySubscriptionId,
            statusReason: null));
    }

    public Result Activate(SaasPlan plan, string? gatewaySubscriptionId = null, string? gatewayCustomerId = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        PlanTier = plan.Tier;
        MonthlyPrice = plan.MonthlyPrice;
        Status = TenantSubscriptionStatus.Active;
        StatusReason = null;
        GracePeriodEndsAtUtc = null;

        if (!string.IsNullOrWhiteSpace(gatewaySubscriptionId))
        {
            GatewaySubscriptionId = gatewaySubscriptionId;
        }

        if (!string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            GatewayCustomerId = gatewayCustomerId;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkOverdue(DateTimeOffset now, int gracePeriodDays = 5, string? reason = null)
    {
        if (Status == TenantSubscriptionStatus.Canceled)
        {
            return Result.Failure(new Error("saas_subscription.already_canceled", "Assinatura cancelada não pode entrar em carência.", ErrorType.Conflict));
        }

        // Se já estava em GracePeriod e a data limite de carência passou:
        if (Status == TenantSubscriptionStatus.GracePeriod && GracePeriodEndsAtUtc.HasValue && now > GracePeriodEndsAtUtc.Value)
        {
            Status = TenantSubscriptionStatus.Delinquent;
            StatusReason = reason ?? "Suspenso por inadimplência após término do período de carência.";
            UpdatedAtUtc = now;
            return Result.Success();
        }

        // Se ainda não estava em carência nem suspenso:
        if (Status != TenantSubscriptionStatus.Delinquent)
        {
            Status = TenantSubscriptionStatus.GracePeriod;
            GracePeriodEndsAtUtc = now.AddDays(gracePeriodDays);
            StatusReason = reason ?? $"Fatura pendente. Período de tolerância concedido até {GracePeriodEndsAtUtc:dd/MM/yyyy}.";
            UpdatedAtUtc = now;
            return Result.Success();
        }

        return Result.Success();
    }

    public Result RenewCycle(DateTimeOffset newStart, DateTimeOffset newEnd, DateTimeOffset? nextBilling = null)
    {
        CurrentPeriodStartUtc = newStart;
        CurrentPeriodEndUtc = newEnd;
        NextBillingDateUtc = nextBilling ?? newEnd;
        Status = TenantSubscriptionStatus.Active;
        GracePeriodEndsAtUtc = null;
        StatusReason = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result ChangePlan(SaasPlan newPlan)
    {
        ArgumentNullException.ThrowIfNull(newPlan);

        if (Status == TenantSubscriptionStatus.Canceled)
        {
            return Result.Failure(new Error("saas_subscription.canceled", "Não é possível alterar o plano de uma assinatura cancelada.", ErrorType.Conflict));
        }

        PlanTier = newPlan.Tier;
        MonthlyPrice = newPlan.MonthlyPrice;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Suspend(string? reason = null)
    {
        Status = TenantSubscriptionStatus.Delinquent;
        StatusReason = reason ?? "Assinatura suspensa por pendência financeira.";
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Cancel(string? reason = null)
    {
        Status = TenantSubscriptionStatus.Canceled;
        StatusReason = reason ?? "Assinatura cancelada.";
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public bool IsOperationalAllowed() =>
        Status is TenantSubscriptionStatus.Trial or TenantSubscriptionStatus.Active or TenantSubscriptionStatus.GracePeriod;
}
