using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class TeamMember : IMustHaveTenant
{
    private TeamMember()
    {
    }

    private TeamMember(Guid id, Guid tenantId, string fullName, string role, string email)
    {
        Id = id;
        TenantId = tenantId;
        FullName = fullName;
        Role = role;
        Email = email;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static Result<TeamMember> Create(Guid tenantId, string fullName, string role, string email)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TeamMember>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedName = string.IsNullOrWhiteSpace(fullName) ? string.Empty : fullName.Trim();
        if (normalizedName.Length is 0 or > 200)
        {
            return Result<TeamMember>.Failure(new Error("team_member.name.invalid", "A valid full name is required.", ErrorType.Validation));
        }

        var normalizedRole = string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        if (normalizedRole.Length is 0 or > 80)
        {
            return Result<TeamMember>.Failure(new Error("team_member.role.invalid", "A valid role is required.", ErrorType.Validation));
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        if (normalizedEmail.Length > 200)
        {
            return Result<TeamMember>.Failure(new Error("team_member.email.invalid", "Email cannot exceed 200 characters.", ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && !normalizedEmail.Contains('@'))
        {
            return Result<TeamMember>.Failure(new Error("team_member.email.invalid", "A valid email is required.", ErrorType.Validation));
        }

        return Result<TeamMember>.Success(new TeamMember(Guid.CreateVersion7(), tenantId, normalizedName, normalizedRole, normalizedEmail));
    }

    public Result<TeamMember> Update(string fullName, string role, string email)
    {
        var normalizedName = string.IsNullOrWhiteSpace(fullName) ? string.Empty : fullName.Trim();
        if (normalizedName.Length is 0 or > 200)
        {
            return Result<TeamMember>.Failure(new Error("team_member.name.invalid", "A valid full name is required.", ErrorType.Validation));
        }

        var normalizedRole = string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        if (normalizedRole.Length is 0 or > 80)
        {
            return Result<TeamMember>.Failure(new Error("team_member.role.invalid", "A valid role is required.", ErrorType.Validation));
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        if (normalizedEmail.Length > 200)
        {
            return Result<TeamMember>.Failure(new Error("team_member.email.invalid", "Email cannot exceed 200 characters.", ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && !normalizedEmail.Contains('@'))
        {
            return Result<TeamMember>.Failure(new Error("team_member.email.invalid", "A valid email is required.", ErrorType.Validation));
        }

        FullName = normalizedName;
        Role = normalizedRole;
        Email = normalizedEmail;

        return Result<TeamMember>.Success(this);
    }

    public Result<TeamMember> Deactivate()
    {
        IsActive = false;
        return Result<TeamMember>.Success(this);
    }

    public Result<TeamMember> Activate()
    {
        IsActive = true;
        return Result<TeamMember>.Success(this);
    }
}
