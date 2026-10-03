using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class CommissionRule : IMustHaveTenant
{
    private CommissionRule()
    {
    }

    private CommissionRule(Guid id, Guid tenantId, string serviceName, string roleName, decimal percentage)
    {
        Id = id;
        TenantId = tenantId;
        ServiceName = serviceName;
        RoleName = roleName;
        Percentage = percentage;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string ServiceName { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }

    public static Result<CommissionRule> Create(Guid tenantId, string serviceName, string roleName, decimal percentage)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedServiceName = string.IsNullOrWhiteSpace(serviceName) ? string.Empty : serviceName.Trim();
        if (normalizedServiceName.Length is 0 or > 200)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.service_name.invalid", "A valid service name is required.", ErrorType.Validation));
        }

        var normalizedRoleName = string.IsNullOrWhiteSpace(roleName) ? string.Empty : roleName.Trim();
        if (normalizedRoleName.Length is 0 or > 80)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.role_name.invalid", "A valid role name is required.", ErrorType.Validation));
        }

        if (percentage < 0m || percentage > 100m)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.percentage.invalid", "Commission percentage must be between 0 and 100.", ErrorType.Validation));
        }

        return Result<CommissionRule>.Success(new CommissionRule(Guid.CreateVersion7(), tenantId, normalizedServiceName, normalizedRoleName, percentage));
    }

    public Result<CommissionRule> UpdatePercentage(decimal percentage)
    {
        if (percentage < 0m || percentage > 100m)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.percentage.invalid", "Commission percentage must be between 0 and 100.", ErrorType.Validation));
        }

        Percentage = percentage;
        return Result<CommissionRule>.Success(this);
    }
}
