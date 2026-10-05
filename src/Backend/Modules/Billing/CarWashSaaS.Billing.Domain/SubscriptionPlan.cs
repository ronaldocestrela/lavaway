using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed class SubscriptionPlan : IMustHaveTenant
{
    private SubscriptionPlan()
    {
    }

    private SubscriptionPlan(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        decimal monthlyPrice,
        int creditsPerCycle,
        int allowedPlatesLimit,
        int billingIntervalDays,
        bool isActive,
        DateTimeOffset createdUtc)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        MonthlyPrice = monthlyPrice;
        CreditsPerCycle = creditsPerCycle;
        AllowedPlatesLimit = allowedPlatesLimit;
        BillingIntervalDays = billingIntervalDays;
        IsActive = isActive;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public int CreditsPerCycle { get; private set; }
    public int AllowedPlatesLimit { get; private set; }
    public int BillingIntervalDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset? UpdatedUtc { get; private set; }

    public static Result<SubscriptionPlan> Create(
        Guid tenantId,
        string name,
        string? description,
        decimal monthlyPrice,
        int creditsPerCycle,
        int allowedPlatesLimit,
        int billingIntervalDays = 30)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.name_required", "Nome do plano é obrigatório.", ErrorType.Validation));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 120)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.name_toolong", "Nome do plano não pode ultrapassar 120 caracteres.", ErrorType.Validation));
        }

        if (monthlyPrice <= 0)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.price_invalid", "O valor mensal do plano deve ser maior que zero.", ErrorType.Validation));
        }

        if (creditsPerCycle <= 0)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.credits_invalid", "A quantidade de créditos por ciclo deve ser maior que zero.", ErrorType.Validation));
        }

        if (allowedPlatesLimit <= 0)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.plates_limit_invalid", "O limite de veículos autorizados deve ser de no mínimo 1 placa.", ErrorType.Validation));
        }

        if (billingIntervalDays <= 0)
        {
            return Result<SubscriptionPlan>.Failure(new Error("plan.interval_invalid", "O intervalo de faturamento em dias deve ser maior que zero.", ErrorType.Validation));
        }

        return Result<SubscriptionPlan>.Success(new SubscriptionPlan(
            Guid.CreateVersion7(),
            tenantId,
            trimmedName,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            monthlyPrice,
            creditsPerCycle,
            allowedPlatesLimit,
            billingIntervalDays,
            true,
            DateTimeOffset.UtcNow));
    }

    public Result Update(
        string name,
        string? description,
        decimal monthlyPrice,
        int creditsPerCycle,
        int allowedPlatesLimit,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("plan.name_required", "Nome do plano é obrigatório.", ErrorType.Validation));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 120)
        {
            return Result.Failure(new Error("plan.name_toolong", "Nome do plano não pode ultrapassar 120 caracteres.", ErrorType.Validation));
        }

        if (monthlyPrice <= 0)
        {
            return Result.Failure(new Error("plan.price_invalid", "O valor mensal do plano deve ser maior que zero.", ErrorType.Validation));
        }

        if (creditsPerCycle <= 0)
        {
            return Result.Failure(new Error("plan.credits_invalid", "A quantidade de créditos por ciclo deve ser maior que zero.", ErrorType.Validation));
        }

        if (allowedPlatesLimit <= 0)
        {
            return Result.Failure(new Error("plan.plates_limit_invalid", "O limite de veículos autorizados deve ser de no mínimo 1 placa.", ErrorType.Validation));
        }

        Name = trimmedName;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MonthlyPrice = monthlyPrice;
        CreditsPerCycle = creditsPerCycle;
        AllowedPlatesLimit = allowedPlatesLimit;
        IsActive = isActive;
        UpdatedUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }
}
