using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class RoleModalComponentTests : BunitContext
{
    private static readonly IReadOnlyList<PermissionDefinitionDto> SamplePermissions =
    [
        new("CreateWorkOrders", "Criar Ordens de Serviço", "Permite abrir ordens", "Ordens de Serviço"),
        new("UpdateWorkOrderStatus", "Atualizar Status", "Permite atualizar checklist", "Pátio & Operações"),
        new("ManageCashier", "Operar Caixa", "Permite abrir e fechar caixa", "Financeiro & Caixa")
    ];

    [Fact]
    public void RoleModal_ShouldRenderCorrectly_WhenVisible()
    {
        var cut = Render<RoleModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.AvailablePermissions, SamplePermissions));

        Assert.NotNull(cut.Find("#role-name"));
        Assert.NotNull(cut.Find("#role-desc"));
        Assert.Contains("Ordens de Serviço", cut.Markup);
        Assert.Contains("Pátio", cut.Markup);
        Assert.Contains("Financeiro", cut.Markup);
        Assert.Contains("Criar Ordens de Serviço", cut.Markup);
    }

    [Fact]
    public void RoleModal_ShouldInvokeOnSave_WhenSaveClicked()
    {
        (Guid? Id, string Name, string? Description, IReadOnlyList<string> Permissions)? savedArgs = null;

        var cut = Render<RoleModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.RoleName, "Supervisor de Pátio")
            .Add(p => p.RoleDescription, "Responsável pela pista")
            .Add(p => p.SelectedPermissions, new HashSet<string> { "CreateWorkOrders" })
            .Add(p => p.AvailablePermissions, SamplePermissions)
            .Add(p => p.OnSave, args => savedArgs = args));

        cut.Find("#btn-save-role").Click();

        Assert.NotNull(savedArgs);
        Assert.Equal("Supervisor de Pátio", savedArgs.Value.Name);
        Assert.Equal("Responsável pela pista", savedArgs.Value.Description);
        Assert.Contains("CreateWorkOrders", savedArgs.Value.Permissions);
    }

    [Fact]
    public void TeamMemberModal_ShouldRenderAvailableRoles_WhenRolesProvided()
    {
        var customRoleId = Guid.NewGuid();
        var roles = new List<CustomRoleDto>
        {
            new(Guid.NewGuid(), "Administrator", "Admin padrão", true, ["ConfigureStore"], 1),
            new(customRoleId, "Supervisor Pátio", "Líder de pátio", false, ["CreateWorkOrders"], 2)
        };

        var model = new TeamMemberFormModel
        {
            FullName = "Roberto Silva",
            Role = "Supervisor",
            Email = "roberto@lava.com",
            EnableSystemAccess = true
        };

        var cut = Render<TeamMemberModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.AvailableRoles, roles));

        Assert.Contains("Supervisor Pátio", cut.Markup);
        Assert.Contains("Personalizado", cut.Markup);
    }
}
