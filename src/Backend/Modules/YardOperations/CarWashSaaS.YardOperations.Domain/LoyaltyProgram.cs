using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class LoyaltyProgram : IMustHaveTenant
{
    private LoyaltyProgram()
    {
    }

    private LoyaltyProgram(
        Guid id,
        Guid tenantId,
        bool isEnabled,
        int targetStamps,
        string rewardTitle,
        int proximityThreshold,
        bool allServicesEligible,
        string? eligibleCategoryFilter,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        IsEnabled = isEnabled;
        TargetStamps = targetStamps;
        RewardTitle = rewardTitle;
        ProximityThreshold = proximityThreshold;
        AllServicesEligible = allServicesEligible;
        EligibleCategoryFilter = eligibleCategoryFilter;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public bool IsEnabled { get; private set; }
    public int TargetStamps { get; private set; }
    public string RewardTitle { get; private set; } = string.Empty;
    public int ProximityThreshold { get; private set; }
    public bool AllServicesEligible { get; private set; }
    public string? EligibleCategoryFilter { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<LoyaltyProgram> CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<LoyaltyProgram>.Failure(new Error("loyalty_program.tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var program = new LoyaltyProgram(
            Guid.CreateVersion7(),
            tenantId,
            isEnabled: true,
            targetStamps: 10,
            rewardTitle: "Lavagem Completa Grátis",
            proximityThreshold: 1,
            allServicesEligible: true,
            eligibleCategoryFilter: null,
            updatedAtUtc: DateTimeOffset.UtcNow);

        return Result<LoyaltyProgram>.Success(program);
    }

    public Result Update(
        bool isEnabled,
        int targetStamps,
        string rewardTitle,
        int proximityThreshold = 1,
        bool allServicesEligible = true,
        string? eligibleCategoryFilter = null)
    {
        if (targetStamps <= 0)
        {
            return Result.Failure(new Error("loyalty_program.target.invalid", "A meta de selos deve ser maior que zero.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(rewardTitle) || rewardTitle.Trim().Length > 200)
        {
            return Result.Failure(new Error("loyalty_program.reward.invalid", "O título da recompensa é obrigatório e deve ter até 200 caracteres.", ErrorType.Validation));
        }

        if (proximityThreshold <= 0 || proximityThreshold >= targetStamps)
        {
            return Result.Failure(new Error("loyalty_program.proximity.invalid", "O limiar de proximidade deve ser positivo e menor que a meta de selos.", ErrorType.Validation));
        }

        IsEnabled = isEnabled;
        TargetStamps = targetStamps;
        RewardTitle = rewardTitle.Trim();
        ProximityThreshold = proximityThreshold;
        AllServicesEligible = allServicesEligible;
        EligibleCategoryFilter = string.IsNullOrWhiteSpace(eligibleCategoryFilter) ? null : eligibleCategoryFilter.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    public bool IsServiceEligible(string? category, string? serviceName)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (AllServicesEligible)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(EligibleCategoryFilter))
        {
            return true;
        }

        var allowedCategories = EligibleCategoryFilter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return !string.IsNullOrWhiteSpace(category) &&
               allowedCategories.Any(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
