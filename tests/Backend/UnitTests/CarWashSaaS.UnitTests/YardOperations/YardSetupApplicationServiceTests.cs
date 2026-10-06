using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class YardSetupApplicationServiceTests
{
    private readonly InMemoryYardCapacityRepository _capacityRepo = new();
    private readonly InMemoryTeamMemberRepository _teamRepo = new();
    private readonly InMemoryCommissionRuleRepository _commissionRepo = new();
    private readonly YardSetupApplicationService _service;

    public YardSetupApplicationServiceTests()
    {
        _service = new YardSetupApplicationService(_capacityRepo, _teamRepo, _commissionRepo);
    }

    [Fact]
    public async Task CreateCapacityAsync_ShouldCreateCapacity_WhenValid()
    {
        var tenantId = Guid.NewGuid();
        var command = new CreateYardCapacityCommand(6, "Boxes principais");

        var result = await _service.CreateCapacityAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value!.TotalBoxes);
        Assert.Equal("Boxes principais", result.Value.Description);
    }

    [Fact]
    public async Task CreateCapacityAsync_ShouldFail_WhenCapacityAlreadyExists()
    {
        var tenantId = Guid.NewGuid();
        await _service.CreateCapacityAsync(tenantId, new CreateYardCapacityCommand(4, "Pátio"));

        var result = await _service.CreateCapacityAsync(tenantId, new CreateYardCapacityCommand(8, "Duplicado"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("yard_capacity.duplicate", result.Error.Code);
    }

    [Fact]
    public async Task UpdateCapacityAsync_ShouldUpdate_WhenExists()
    {
        var tenantId = Guid.NewGuid();
        await _service.CreateCapacityAsync(tenantId, new CreateYardCapacityCommand(4, "Pátio inicial"));

        var result = await _service.UpdateCapacityAsync(tenantId, new UpdateYardCapacityCommand(10, "Pátio expandido"));

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.TotalBoxes);
        Assert.Equal("Pátio expandido", result.Value.Description);
    }

    [Fact]
    public async Task UpdateCapacityAsync_ShouldFail_WhenNotFound()
    {
        var tenantId = Guid.NewGuid();

        var result = await _service.UpdateCapacityAsync(tenantId, new UpdateYardCapacityCommand(5, "Pátio"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task CreateTeamMemberAsync_ShouldCreateMember_WhenValid()
    {
        var tenantId = Guid.NewGuid();
        var command = new CreateTeamMemberCommand("João Santos", "Lavador", "joao@lava.com");

        var result = await _service.CreateTeamMemberAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        Assert.Equal("João Santos", result.Value!.FullName);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task CreateTeamMemberAsync_ShouldFail_WhenEmailAlreadyExists()
    {
        var tenantId = Guid.NewGuid();
        await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("João", "Lavador", "joao@lava.com"));

        var result = await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("João Silva", "Polidor", "joao@lava.com"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("team_member.email.duplicate", result.Error.Code);
    }

    [Fact]
    public async Task CreateTeamMemberAsync_ShouldAllowMultipleMembersWithoutEmail()
    {
        var tenantId = Guid.NewGuid();

        var first = await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("João", "Lavador", null));
        var second = await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("Maria", "Polidora", string.Empty));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var members = await _service.ListTeamMembersAsync(tenantId);

        Assert.True(members.IsSuccess);
        Assert.Equal(2, members.Value!.Count);
        Assert.All(members.Value!, member => Assert.Null(member.Email));
    }

    [Fact]
    public async Task UpdateTeamMemberAsync_ShouldUpdate_WhenValid()
    {
        var tenantId = Guid.NewGuid();
        var created = (await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("Pedro", "Secador", "pedro@lava.com"))).Value!;

        var result = await _service.UpdateTeamMemberAsync(tenantId, created.Id, new UpdateTeamMemberCommand("Pedro Alves", "Gerente de Pátio", "pedro.alves@lava.com"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Pedro Alves", result.Value!.FullName);
        Assert.Equal("Gerente de Pátio", result.Value.Role);
    }

    [Fact]
    public async Task UpdateTeamMemberAsync_ShouldFail_WhenEmailTakenByAnotherMember()
    {
        var tenantId = Guid.NewGuid();
        var member1 = (await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("Maria", "Lavadora", "maria@lava.com"))).Value!;
        var member2 = (await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("Ana", "Polidora", "ana@lava.com"))).Value!;

        var result = await _service.UpdateTeamMemberAsync(tenantId, member2.Id, new UpdateTeamMemberCommand("Ana Paula", "Polidora", "maria@lava.com"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task ToggleTeamMemberStatusAsync_ShouldToggleActiveStatus()
    {
        var tenantId = Guid.NewGuid();
        var member = (await _service.CreateTeamMemberAsync(tenantId, new CreateTeamMemberCommand("Lucas", "Lavador", "lucas@lava.com"))).Value!;
        Assert.True(member.IsActive);

        var deact = await _service.ToggleTeamMemberStatusAsync(tenantId, member.Id);
        Assert.True(deact.IsSuccess);
        Assert.False(deact.Value!.IsActive);

        var act = await _service.ToggleTeamMemberStatusAsync(tenantId, member.Id);
        Assert.True(act.IsSuccess);
        Assert.True(act.Value!.IsActive);
    }

    [Fact]
    public async Task CreateCommissionRuleAsync_ShouldCreateRule_WhenValid()
    {
        var tenantId = Guid.NewGuid();
        var command = new CreateCommissionRuleCommand("Lavagem Completa", "Lavador", 10m);

        var result = await _service.CreateCommissionRuleAsync(tenantId, command);

        Assert.True(result.IsSuccess);
        Assert.Equal(10m, result.Value!.Percentage);
    }

    [Fact]
    public async Task CreateCommissionRuleAsync_ShouldFail_WhenSameServiceAndRoleExists()
    {
        var tenantId = Guid.NewGuid();
        await _service.CreateCommissionRuleAsync(tenantId, new CreateCommissionRuleCommand("Ducha", "Lavador", 8m));

        var result = await _service.CreateCommissionRuleAsync(tenantId, new CreateCommissionRuleCommand("Ducha", "Lavador", 12m));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("commission_rule.duplicate", result.Error.Code);
    }

    [Fact]
    public async Task UpdateCommissionRuleAsync_ShouldUpdatePercentage()
    {
        var tenantId = Guid.NewGuid();
        var rule = (await _service.CreateCommissionRuleAsync(tenantId, new CreateCommissionRuleCommand("Polimento", "Polidor", 15m))).Value!;

        var result = await _service.UpdateCommissionRuleAsync(tenantId, rule.Id, new UpdateCommissionRuleCommand(25m));

        Assert.True(result.IsSuccess);
        Assert.Equal(25m, result.Value!.Percentage);
    }

    [Fact]
    public async Task DeleteCommissionRuleAsync_ShouldRemoveRule()
    {
        var tenantId = Guid.NewGuid();
        var rule = (await _service.CreateCommissionRuleAsync(tenantId, new CreateCommissionRuleCommand("Cristalização", "Polidor", 20m))).Value!;

        var deleteResult = await _service.DeleteCommissionRuleAsync(tenantId, rule.Id);
        Assert.True(deleteResult.IsSuccess);

        var rules = (await _service.ListCommissionRulesAsync(tenantId)).Value!;
        Assert.DoesNotContain(rules, r => r.Id == rule.Id);
    }

    private sealed class InMemoryYardCapacityRepository : IYardCapacityRepository
    {
        private readonly List<YardCapacity> _capacities = [];

        public Task<YardCapacity?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult(_capacities.FirstOrDefault(c => c.TenantId == tenantId));
        }

        public Task AddAsync(YardCapacity yardCapacity, CancellationToken ct = default)
        {
            _capacities.Add(yardCapacity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(YardCapacity yardCapacity, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTeamMemberRepository : ITeamMemberRepository
    {
        private readonly List<TeamMember> _members = [];

        public Task<IReadOnlyCollection<TeamMember>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<TeamMember>>(_members.Where(m => m.TenantId == tenantId).ToList());
        }

        public Task<TeamMember?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_members.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));
        }

        public Task<TeamMember?> GetByEmailAsync(Guid tenantId, string? email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult<TeamMember?>(null);
            }

            return Task.FromResult(_members.FirstOrDefault(m => m.TenantId == tenantId && string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(TeamMember teamMember, CancellationToken ct = default)
        {
            _members.Add(teamMember);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TeamMember teamMember, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCommissionRuleRepository : ICommissionRuleRepository
    {
        private readonly List<CommissionRule> _rules = [];

        public Task<IReadOnlyCollection<CommissionRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<CommissionRule>>(_rules.Where(r => r.TenantId == tenantId).ToList());
        }

        public Task<CommissionRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_rules.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));
        }

        public Task<CommissionRule?> GetByServiceAndRoleAsync(Guid tenantId, string serviceName, string roleName, CancellationToken ct = default)
        {
            return Task.FromResult(_rules.FirstOrDefault(r => r.TenantId == tenantId &&
                r.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase) &&
                r.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            _rules.Add(commissionRule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CommissionRule commissionRule, CancellationToken ct = default)
        {
            _rules.Remove(commissionRule);
            return Task.CompletedTask;
        }
    }
}
