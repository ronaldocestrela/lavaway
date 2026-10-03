using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class YardSetupTenantIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task YardSetup_ShouldPersistCapacityTeamAndCommissions_AndEnforceStrictTenantIsolation()
    {
        var tenantA = await CreateTenantAsync("Lava-Jato Alpha");
        var tenantB = await CreateTenantAsync("Lava-Jato Beta");

        Guid memberAId;
        Guid ruleAId;

        // 1. Tenant A configura capacidade, colaborador e comissão
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceA = CreateYardSetupService(contextA);

            var capacityResult = await serviceA.CreateCapacityAsync(tenantA, new CreateYardCapacityCommand(6, "Boxes Principais Alpha"));
            Assert.True(capacityResult.IsSuccess);
            Assert.Equal(6, capacityResult.Value!.TotalBoxes);

            var memberResult = await serviceA.CreateTeamMemberAsync(tenantA, new CreateTeamMemberCommand("Carlos Alberto", "Lavador", "carlos@alpha.com"));
            Assert.True(memberResult.IsSuccess);
            memberAId = memberResult.Value!.Id;

            var ruleResult = await serviceA.CreateCommissionRuleAsync(tenantA, new CreateCommissionRuleCommand("Lavagem Completa", "Lavador", 12.5m));
            Assert.True(ruleResult.IsSuccess);
            ruleAId = ruleResult.Value!.Id;
        }

        // 2. Tenant A consulta e atualiza os dados
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceA = CreateYardSetupService(contextA);

            var capResult = await serviceA.GetCapacityAsync(tenantA);
            Assert.True(capResult.IsSuccess);
            Assert.Equal(6, capResult.Value!.TotalBoxes);

            var updateCap = await serviceA.UpdateCapacityAsync(tenantA, new UpdateYardCapacityCommand(8, "Boxes Expandidos"));
            Assert.True(updateCap.IsSuccess);
            Assert.Equal(8, updateCap.Value!.TotalBoxes);

            var members = await serviceA.ListTeamMembersAsync(tenantA);
            Assert.True(members.IsSuccess);
            Assert.Single(members.Value!);

            var toggleResult = await serviceA.ToggleTeamMemberStatusAsync(tenantA, memberAId);
            Assert.True(toggleResult.IsSuccess);
            Assert.False(toggleResult.Value!.IsActive);

            var updateRule = await serviceA.UpdateCommissionRuleAsync(tenantA, ruleAId, new UpdateCommissionRuleCommand(18.0m));
            Assert.True(updateRule.IsSuccess);
            Assert.Equal(18.0m, updateRule.Value!.Percentage);
        }

        // 3. Tenant B deve receber NotFound e listas vazias (Isolamento rígido)
        await using (var contextB = CreateYardContext(tenantB))
        {
            var serviceB = CreateYardSetupService(contextB);

            var capB = await serviceB.GetCapacityAsync(tenantB);
            Assert.False(capB.IsSuccess);
            Assert.Equal(ErrorType.NotFound, capB.Error!.Type);

            var membersB = await serviceB.ListTeamMembersAsync(tenantB);
            Assert.True(membersB.IsSuccess);
            Assert.Empty(membersB.Value!);

            var memberByIdB = await serviceB.GetTeamMemberAsync(tenantB, memberAId);
            Assert.False(memberByIdB.IsSuccess);
            Assert.Equal(ErrorType.NotFound, memberByIdB.Error!.Type);

            var rulesB = await serviceB.ListCommissionRulesAsync(tenantB);
            Assert.True(rulesB.IsSuccess);
            Assert.Empty(rulesB.Value!);

            var updateRuleB = await serviceB.UpdateCommissionRuleAsync(tenantB, ruleAId, new UpdateCommissionRuleCommand(50m));
            Assert.False(updateRuleB.IsSuccess);
            Assert.Equal(ErrorType.NotFound, updateRuleB.Error!.Type);
        }

        // 4. Tenant A exclui a comissão
        await using (var contextA = CreateYardContext(tenantA))
        {
            var serviceA = CreateYardSetupService(contextA);
            var deleteResult = await serviceA.DeleteCommissionRuleAsync(tenantA, ruleAId);
            Assert.True(deleteResult.IsSuccess);

            var rules = await serviceA.ListCommissionRulesAsync(tenantA);
            Assert.True(rules.IsSuccess);
            Assert.Empty(rules.Value!);
        }
    }

    private YardSetupApplicationService CreateYardSetupService(YardOperationsDbContext context)
    {
        var capacityRepo = new YardCapacityRepository(context);
        var teamRepo = new TeamMemberRepository(context);
        var commissionRepo = new CommissionRuleRepository(context);
        return new YardSetupApplicationService(capacityRepo, teamRepo, commissionRepo);
    }

    private YardOperationsDbContext CreateYardContext(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return new YardOperationsDbContext(fixture.CreateYardOperationsOptions(), accessor);
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        await using var context = new TenantsDbContext(fixture.CreateTenantsOptions(), new CurrentTenantAccessor());
        var tenant = Tenant.Create(name).Value!;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant.Id;
    }
}
