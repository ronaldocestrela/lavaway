using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class YardSetupApplicationService(
    IYardCapacityRepository yardCapacityRepository,
    ITeamMemberRepository teamMemberRepository,
    ICommissionRuleRepository commissionRuleRepository,
    ITenantPlanQuotaLookup? quotaLookup = null,
    IUserAccountProvisioner? userProvisioner = null)
{
    private async Task<Result> EnsureCommissionsFeatureAsync(Guid tenantId, CancellationToken ct)
    {
        if (quotaLookup is null)
        {
            return Result.Success();
        }

        var check = await quotaLookup.HasFeatureAsync(tenantId, SaasFeature.Commissions, ct);
        if (!check.IsSuccess)
        {
            return Result.Failure(check.Error!);
        }

        if (!check.Value)
        {
            return Result.Failure(new Error(
                "tenant.plan.commissions_not_allowed",
                "Gestão e regras de comissão não estão disponíveis no plano contratado. Faça upgrade para o plano Pro ou Enterprise.",
                ErrorType.Unauthorized));
        }

        return Result.Success();
    }

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

        if (quotaLookup is not null)
        {
            var boxValidation = await quotaLookup.ValidateBoxesLimitAsync(tenantId, command.TotalBoxes, ct);
            if (!boxValidation.IsSuccess)
            {
                return Result<YardCapacity>.Failure(boxValidation.Error!);
            }
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

        if (quotaLookup is not null)
        {
            var boxValidation = await quotaLookup.ValidateBoxesLimitAsync(tenantId, command.TotalBoxes, ct);
            if (!boxValidation.IsSuccess)
            {
                return Result<YardCapacity>.Failure(boxValidation.Error!);
            }
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

        if (quotaLookup is not null)
        {
            var members = await teamMemberRepository.ListByTenantAsync(tenantId, ct);
            var activeCount = members.Count(m => m.IsActive);
            var limitValidation = await quotaLookup.ValidateTeamMemberLimitAsync(tenantId, activeCount, ct);
            if (!limitValidation.IsSuccess)
            {
                return Result<TeamMember>.Failure(limitValidation.Error!);
            }
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        if (normalizedEmail is not null)
        {
            var existing = await teamMemberRepository.GetByEmailAsync(tenantId, normalizedEmail, ct);
            if (existing is not null)
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.duplicate", "A team member with this email already exists for this tenant.", ErrorType.Conflict));
            }
        }

        Guid? provisionedUserId = null;
        string? systemRole = null;

        if (command.EnableSystemAccess)
        {
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.required_for_system_access", "E-mail é obrigatório para liberar acesso ao sistema.", ErrorType.Validation));
            }

            if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 6)
            {
                return Result<TeamMember>.Failure(new Error("team_member.password.invalid", "A senha deve conter pelo menos 6 caracteres.", ErrorType.Validation));
            }

            systemRole = string.IsNullOrWhiteSpace(command.SystemRole) ? "Operator" : command.SystemRole.Trim();

            if (userProvisioner is not null)
            {
                var provisionResult = await userProvisioner.ProvisionUserAsync(
                    tenantId,
                    normalizedEmail,
                    command.Password,
                    systemRole,
                    ct);

                if (!provisionResult.IsSuccess)
                {
                    return Result<TeamMember>.Failure(provisionResult.Error!);
                }

                provisionedUserId = provisionResult.Value;
            }
        }

        var result = TeamMember.Create(
            tenantId,
            command.FullName,
            command.Role,
            normalizedEmail,
            provisionedUserId,
            systemRole);

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

        var normalizedEmail = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        if (normalizedEmail is not null)
        {
            var existingWithEmail = await teamMemberRepository.GetByEmailAsync(tenantId, normalizedEmail, ct);
            if (existingWithEmail is not null && existingWithEmail.Id != id)
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.duplicate", "A team member with this email already exists for this tenant.", ErrorType.Conflict));
            }
        }

        if (command.EnableSystemAccess)
        {
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return Result<TeamMember>.Failure(new Error("team_member.email.required_for_system_access", "E-mail é obrigatório para liberar acesso ao sistema.", ErrorType.Validation));
            }

            var systemRole = string.IsNullOrWhiteSpace(command.SystemRole) ? (member.SystemRole ?? "Operator") : command.SystemRole.Trim();

            if (member.UserId.HasValue)
            {
                // Já possui usuário, atualiza se necessário
                if (userProvisioner is not null)
                {
                    var updateAccessResult = await userProvisioner.UpdateUserAccessAsync(
                        tenantId,
                        member.UserId.Value,
                        normalizedEmail,
                        command.Password,
                        systemRole,
                        member.IsActive,
                        ct);

                    if (!updateAccessResult.IsSuccess)
                    {
                        return Result<TeamMember>.Failure(updateAccessResult.Error!);
                    }
                }

                member.LinkUserAccount(member.UserId.Value, systemRole);
            }
            else
            {
                // Concedendo acesso retroativo
                if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 6)
                {
                    return Result<TeamMember>.Failure(new Error("team_member.password.invalid", "A senha deve conter pelo menos 6 caracteres.", ErrorType.Validation));
                }

                if (userProvisioner is not null)
                {
                    var provisionResult = await userProvisioner.ProvisionUserAsync(
                        tenantId,
                        normalizedEmail,
                        command.Password,
                        systemRole,
                        ct);

                    if (!provisionResult.IsSuccess)
                    {
                        return Result<TeamMember>.Failure(provisionResult.Error!);
                    }

                    member.LinkUserAccount(provisionResult.Value, systemRole);
                }
            }
        }
        else if (member.UserId.HasValue)
        {
            // Revogando acesso ao sistema
            if (userProvisioner is not null)
            {
                await userProvisioner.SetUserActiveStatusAsync(tenantId, member.UserId.Value, false, ct);
            }

            member.UnlinkUserAccount();
        }

        var updateResult = member.Update(command.FullName, command.Role, normalizedEmail);
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
            if (quotaLookup is not null)
            {
                var members = await teamMemberRepository.ListByTenantAsync(tenantId, ct);
                var activeCount = members.Count(m => m.IsActive);
                var limitValidation = await quotaLookup.ValidateTeamMemberLimitAsync(tenantId, activeCount, ct);
                if (!limitValidation.IsSuccess)
                {
                    return Result<TeamMember>.Failure(limitValidation.Error!);
                }
            }

            member.Activate();
        }

        // Sincroniza o status de acesso com o Identity
        if (member.UserId.HasValue && userProvisioner is not null)
        {
            await userProvisioner.SetUserActiveStatusAsync(tenantId, member.UserId.Value, member.IsActive, ct);
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

        var check = await EnsureCommissionsFeatureAsync(tenantId, ct);
        if (!check.IsSuccess)
        {
            return Result<IReadOnlyCollection<CommissionRule>>.Failure(check.Error!);
        }

        var rules = await commissionRuleRepository.ListByTenantAsync(tenantId, ct);
        return Result<IReadOnlyCollection<CommissionRule>>.Success(rules);
    }

    public async Task<Result<CommissionRule>> GetCommissionRuleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var check = await EnsureCommissionsFeatureAsync(tenantId, ct);
        if (!check.IsSuccess)
        {
            return Result<CommissionRule>.Failure(check.Error!);
        }

        var rule = await commissionRuleRepository.GetByIdAsync(tenantId, id, ct);
        if (rule is null)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.not_found", "Commission rule was not found.", ErrorType.NotFound));
        }

        return Result<CommissionRule>.Success(rule);
    }

    public async Task<Result<CommissionRule>> CreateCommissionRuleAsync(Guid tenantId, CreateCommissionRuleCommand command, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var check = await EnsureCommissionsFeatureAsync(tenantId, ct);
        if (!check.IsSuccess)
        {
            return Result<CommissionRule>.Failure(check.Error!);
        }

        var existing = await commissionRuleRepository.GetByServiceAndRoleAsync(tenantId, command.ServiceName, command.RoleName, ct);
        if (existing is not null)
        {
            return Result<CommissionRule>.Failure(new Error("commission_rule.duplicate", "A commission rule for this service and role already exists.", ErrorType.Conflict));
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

        var check = await EnsureCommissionsFeatureAsync(tenantId, ct);
        if (!check.IsSuccess)
        {
            return Result<CommissionRule>.Failure(check.Error!);
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

        var check = await EnsureCommissionsFeatureAsync(tenantId, ct);
        if (!check.IsSuccess)
        {
            return check;
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
