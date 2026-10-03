using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class YardSetupApplicationService(
    IYardCapacityRepository yardCapacityRepository,
    ITeamMemberRepository teamMemberRepository,
    ICommissionRuleRepository commissionRuleRepository)
{
    public async Task<Result<YardCapacity>> GetCapacityAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var capacity = await yardCapacityRepository.GetByTenantAsync(tenantId, ct);
        if (capacity is null)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.not_found", "Yard capacity was not found.", ErrorType.NotFound));
        }

        return Result<YardCapacity>.Success(capacity);
    }

    public async Task<Result<YardCapacity>> CreateCapacityAsync(Guid tenantId, CreateYardCapacityCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existing = await yardCapacityRepository.GetByTenantAsync(tenantId, ct);
        if (existing is not null)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.duplicate", "Yard capacity already exists for this tenant.", ErrorType.Conflict));
        }

        var result = YardCapacity.Create(tenantId, command.TotalBoxes, command.Description);
        if (!result.IsSuccess)
        {
            return result;
        }

        await yardCapacityRepository.AddAsync(result.Value!, ct);
        return result;
    }

    public async Task<Result<YardCapacity>> UpdateCapacityAsync(Guid tenantId, UpdateYardCapacityCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var capacity = await yardCapacityRepository.GetByTenantAsync(tenantId, ct);
        if (capacity is null)
        {
            return Result<YardCapacity>.Failure(new Error("yard_capacity.not_found", "Yard capacity was not found.", ErrorType.NotFound));
        }

        var updateResult = capacity.Update(command.TotalBoxes, command.Description);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        await yardCapacityRepository.UpdateAsync(capacity, ct);
        return Result<YardCapacity>.Success(capacity);
    }

    public async Task<Result<IReadOnlyCollection<TeamMember>>> ListTeamMembersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<TeamMember>>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var members = await teamMemberRepository.ListByTenantAsync(tenantId, ct);
        return Result<IReadOnlyCollection<TeamMember>>.Success(members);
    }

    public async Task<Result<TeamMember>> GetTeamMemberAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TeamMember>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var member = await teamMemberRepository.GetByIdAsync(tenantId, id, ct);
        if (member is null)
        {
            return Result<TeamMember>.Failure(new Error("team_member.not_found", "Team member was not found.", ErrorType.NotFound));
        }

        return Result<TeamMember>.Success(member);
    }

    public async Task<Result<TeamMember>> CreateTeamMemberAsync(Guid tenantId, CreateTeamMemberCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TeamMember>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var existing = await teamMemberRepository.GetByEmailAsync(tenantId, command.Email.Trim(), ct);
            if (existing is not null)
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.duplicate", "A team member with this email already exists for this tenant.", ErrorType.Conflict));
            }
        }

        var result = TeamMember.Create(tenantId, command.FullName, command.Role, command.Email);
        if (!result.IsSuccess)
        {
            return result;
        }

        await teamMemberRepository.AddAsync(result.Value!, ct);
        return result;
    }

    public async Task<Result<TeamMember>> UpdateTeamMemberAsync(Guid tenantId, Guid id, UpdateTeamMemberCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TeamMember>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var member = await teamMemberRepository.GetByIdAsync(tenantId, id, ct);
        if (member is null)
        {
            return Result<TeamMember>.Failure(new Error("team_member.not_found", "Team member was not found.", ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var existingWithEmail = await teamMemberRepository.GetByEmailAsync(tenantId, command.Email.Trim(), ct);
            if (existingWithEmail is not null && existingWithEmail.Id != id)
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.duplicate", "A team member with this email already exists for this tenant.", ErrorType.Conflict));
            }
        }

        var updateResult = member.Update(command.FullName, command.Role, command.Email ?? string.Empty);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        await teamMemberRepository.UpdateAsync(member, ct);
        return Result<TeamMember>.Success(member);
    }

    public async Task<Result<TeamMember>> ToggleTeamMemberStatusAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TeamMember>.Failure(new Error("team_member.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var member = await teamMemberRepository.GetByIdAsync(tenantId, id, ct);
        if (member is null)
        {
            return Result<TeamMember>.Failure(new Error("team_member.not_found", "Team member was not found.", ErrorType.NotFound));
        }

        if (member.IsActive)
        {
            member.Deactivate();
        }
        else
        {
            member.Activate();
        }

        await teamMemberRepository.UpdateAsync(member, ct);
        return Result<TeamMember>.Success(member);
    }

    public async Task<Result<IReadOnlyCollection<CommissionRule>>> ListCommissionRulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<CommissionRule>>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var rules = await commissionRuleRepository.ListByTenantAsync(tenantId, ct);
        return Result<IReadOnlyCollection<CommissionRule>>.Success(rules);
    }

    public async Task<Result<CommissionRule>> CreateCommissionRuleAsync(Guid tenantId, CreateCommissionRuleCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existing = await commissionRuleRepository.GetByServiceAndRoleAsync(tenantId, command.ServiceName.Trim(), command.RoleName.Trim(), ct);
        if (existing is not null)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.duplicate", "A commission rule already exists for this service and role in this tenant.", ErrorType.Conflict));
        }

        var result = CommissionRule.Create(tenantId, command.ServiceName, command.RoleName, command.Percentage);
        if (!result.IsSuccess)
        {
            return result;
        }

        await commissionRuleRepository.AddAsync(result.Value!, ct);
        return result;
    }

    public async Task<Result<CommissionRule>> UpdateCommissionRuleAsync(Guid tenantId, Guid id, UpdateCommissionRuleCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var rule = await commissionRuleRepository.GetByIdAsync(tenantId, id, ct);
        if (rule is null)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.not_found", "Commission rule was not found.", ErrorType.NotFound));
        }

        var updateResult = rule.UpdatePercentage(command.Percentage);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        await commissionRuleRepository.UpdateAsync(rule, ct);
        return Result<CommissionRule>.Success(rule);
    }

    public async Task<Result> DeleteCommissionRuleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var rule = await commissionRuleRepository.GetByIdAsync(tenantId, id, ct);
        if (rule is null)
        {
            return Result.Failure(new Error("commission_rule.not_found", "Commission rule was not found.", ErrorType.NotFound));
        }

        await commissionRuleRepository.DeleteAsync(rule, ct);
        return Result.Success();
    }
}
