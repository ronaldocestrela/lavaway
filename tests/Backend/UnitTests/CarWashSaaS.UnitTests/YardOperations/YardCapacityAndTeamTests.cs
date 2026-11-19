using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class YardCapacityAndTeamTests
{
    [Fact]
    public void YardCapacity_Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = YardCapacity.Create(tenantId, 6, "Boxes de lavagem");

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value!.TotalBoxes);
        Assert.Equal("Boxes de lavagem", result.Value!.Description);
        Assert.Equal(tenantId, result.Value!.TenantId);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
    }

    [Fact]
    public void YardCapacity_Create_Should_Reject_NonPositive_Capacity()
    {
        var tenantId = Guid.NewGuid();

        var result = YardCapacity.Create(tenantId, 0, "Capacidade inválida");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("yard_capacity.total_boxes.invalid", result.Error.Code);
    }

    [Fact]
    public void YardCapacity_Create_Should_Reject_Empty_Tenant()
    {
        var result = YardCapacity.Create(Guid.Empty, 5, "Pátio");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("yard_capacity.tenant.required", result.Error.Code);
    }

    [Fact]
    public void YardCapacity_Update_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();
        var capacity = YardCapacity.Create(tenantId, 4, "Boxes iniciais").Value!;

        var result = capacity.Update(8, "Boxes ampliados");

        Assert.True(result.IsSuccess);
        Assert.Equal(8, capacity.TotalBoxes);
        Assert.Equal("Boxes ampliados", capacity.Description);
    }

    [Fact]
    public void YardCapacity_Update_Should_Reject_NonPositive_Capacity()
    {
        var tenantId = Guid.NewGuid();
        var capacity = YardCapacity.Create(tenantId, 4, "Boxes").Value!;

        var result = capacity.Update(-1, "Novo");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void TeamMember_Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "Carlos Lavador", "Lavador", "carlos@lava.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("Carlos Lavador", result.Value!.FullName);
        Assert.Equal("Lavador", result.Value!.Role);
        Assert.Equal("carlos@lava.com", result.Value!.Email);
        Assert.True(result.Value!.IsActive);
    }

    [Fact]
    public void TeamMember_Create_Should_Succeed_Without_Email()
    {
        var tenantId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "Carlos Lavador", "Lavador", null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Email);
    }

    [Fact]
    public void TeamMember_Create_Should_Reject_Empty_Name()
    {
        var tenantId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "   ", "Operador de pátio", "operator@example.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("team_member.name.invalid", result.Error.Code);
    }

    [Fact]
    public void TeamMember_Create_Should_Reject_Invalid_Email()
    {
        var tenantId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "João Silva", "Polidor", "email-invalido-sem-arroba");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("team_member.email.invalid", result.Error.Code);
    }

    [Fact]
    public void TeamMember_Update_Should_Modify_Fields()
    {
        var tenantId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "João", "Lavador", "joao@lava.com").Value!;

        var result = member.Update("João Silva", "Encarregado", "joaosilva@lava.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("João Silva", member.FullName);
        Assert.Equal("Encarregado", member.Role);
        Assert.Equal("joaosilva@lava.com", member.Email);
    }

    [Fact]
    public void TeamMember_Update_Should_Clear_Email_WhenBlank()
    {
        var tenantId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "João", "Lavador", "joao@lava.com").Value!;

        var result = member.Update("João Silva", "Encarregado", "   ");

        Assert.True(result.IsSuccess);
        Assert.Null(member.Email);
    }

    [Fact]
    public void TeamMember_Deactivate_And_Activate_Should_Toggle_Status()
    {
        var tenantId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "Marcos", "Secador", "marcos@lava.com").Value!;
        Assert.True(member.IsActive);

        var deactResult = member.Deactivate();
        Assert.True(deactResult.IsSuccess);
        Assert.False(member.IsActive);

        var actResult = member.Activate();
        Assert.True(actResult.IsSuccess);
        Assert.True(member.IsActive);
    }

    [Fact]
    public void TeamMember_Create_Should_Support_User_Account_Link()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = TeamMember.Create(tenantId, "Lucas Operador", "Lavador", "lucas@lava.com", userId, "Operator");

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value!.UserId);
        Assert.Equal("Operator", result.Value!.SystemRole);
        Assert.True(result.Value!.HasSystemAccess);
    }

    [Fact]
    public void TeamMember_LinkUserAccount_Should_Set_UserId_And_SystemRole()
    {
        var tenantId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "Ana Recepcionista", "Recepcionista", "ana@lava.com").Value!;
        Assert.False(member.HasSystemAccess);

        var userId = Guid.NewGuid();
        var linkResult = member.LinkUserAccount(userId, "Receptionist");

        Assert.True(linkResult.IsSuccess);
        Assert.Equal(userId, member.UserId);
        Assert.Equal("Receptionist", member.SystemRole);
        Assert.True(member.HasSystemAccess);
    }

    [Fact]
    public void TeamMember_UnlinkUserAccount_Should_Clear_System_Access()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "Lucas", "Polidor", "lucas@lava.com", userId, "Operator").Value!;
        Assert.True(member.HasSystemAccess);

        var unlinkResult = member.UnlinkUserAccount();

        Assert.True(unlinkResult.IsSuccess);
        Assert.Null(member.UserId);
        Assert.Null(member.SystemRole);
        Assert.False(member.HasSystemAccess);
    }

    [Fact]
    public void TeamMember_LinkUserAccount_Should_Reject_Empty_UserId()
    {
        var tenantId = Guid.NewGuid();
        var member = TeamMember.Create(tenantId, "Lucas", "Polidor", "lucas@lava.com").Value!;

        var linkResult = member.LinkUserAccount(Guid.Empty, "Operator");

        Assert.False(linkResult.IsSuccess);
        Assert.Equal("team_member.user_id.invalid", linkResult.Error!.Code);
    }

    [Fact]
    public void CommissionRule_Create_Should_Succeed_With_Valid_Data()
    {
        var tenantId = Guid.NewGuid();

        var result = CommissionRule.Create(tenantId, "Lavagem Completa", "Polidor", 15.0m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lavagem Completa", result.Value!.ServiceName);
        Assert.Equal("Polidor", result.Value!.RoleName);
        Assert.Equal(15.0m, result.Value!.Percentage);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void CommissionRule_Create_Should_Reject_Invalid_Percentage(decimal percentage)
    {
        var tenantId = Guid.NewGuid();

        var result = CommissionRule.Create(tenantId, "Ducha", "Lavador", percentage);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("commission_rule.percentage.invalid", result.Error.Code);
    }

    [Fact]
    public void CommissionRule_UpdatePercentage_Should_Modify_Value()
    {
        var tenantId = Guid.NewGuid();
        var rule = CommissionRule.Create(tenantId, "Higienização", "Operador", 10.0m).Value!;

        var result = rule.UpdatePercentage(20.0m);

        Assert.True(result.IsSuccess);
        Assert.Equal(20.0m, rule.Percentage);
    }

    [Fact]
    public void CommissionRule_UpdatePercentage_Should_Reject_Out_Of_Range()
    {
        var tenantId = Guid.NewGuid();
        var rule = CommissionRule.Create(tenantId, "Higienização", "Operador", 10.0m).Value!;

        var result = rule.UpdatePercentage(150.0m);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
