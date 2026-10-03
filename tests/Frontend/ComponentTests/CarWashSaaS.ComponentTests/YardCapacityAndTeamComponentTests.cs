using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class YardCapacityAndTeamComponentTests : BunitContext
{
    [Fact]
    public void YardCapacityCard_ShouldRenderCapacityMetrics_WhenCapacityProvided()
    {
        var capacity = new YardCapacityDto(Guid.NewGuid(), 8, "Boxes Principais");

        var cut = Render<YardCapacityCard>(parameters => parameters
            .Add(p => p.Capacity, capacity));

        Assert.Contains("8", cut.Find(".metric-value").TextContent);
        Assert.Contains("Boxes Principais", cut.Find(".metric-name").TextContent);
        Assert.Equal(8, cut.FindAll(".box-slot").Count);
    }

    [Fact]
    public void YardCapacityCard_ShouldEnterEditMode_WhenEditClicked()
    {
        var capacity = new YardCapacityDto(Guid.NewGuid(), 6, "Setor A");

        var cut = Render<YardCapacityCard>(parameters => parameters
            .Add(p => p.Capacity, capacity));

        var editBtn = cut.Find("#btn-edit-capacity");
        editBtn.Click();

        Assert.NotNull(cut.Find("#capacity-boxes"));
        Assert.NotNull(cut.Find("#btn-save-capacity"));
        Assert.NotNull(cut.Find("#btn-cancel-capacity"));
    }

    [Fact]
    public void YardCapacityCard_ShouldTriggerOnSave_WhenSaveClickedWithValidData()
    {
        var capacity = new YardCapacityDto(Guid.NewGuid(), 4, "Inicial");
        YardCapacityFormModel? savedModel = null;

        var cut = Render<YardCapacityCard>(parameters => parameters
            .Add(p => p.Capacity, capacity)
            .Add(p => p.OnSave, m => savedModel = m));

        cut.Find("#btn-edit-capacity").Click();

        var boxesInput = cut.Find("#capacity-boxes");
        boxesInput.Change("10");

        cut.Find("#btn-save-capacity").Click();

        Assert.NotNull(savedModel);
        Assert.Equal(10, savedModel.TotalBoxes);
    }

    [Fact]
    public void TeamMemberModal_ShouldNotDisplay_WhenIsVisibleIsFalse()
    {
        var cut = Render<TeamMemberModal>(parameters => parameters
            .Add(p => p.IsVisible, false));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void TeamMemberModal_ShouldRenderFields_WhenVisible()
    {
        var model = new TeamMemberFormModel
        {
            FullName = "Roberto Silva",
            Role = "Polidor",
            Email = "roberto@lava.com"
        };

        var cut = Render<TeamMemberModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model));

        Assert.NotNull(cut.Find(".modal-backdrop"));
        Assert.NotNull(cut.Find("#member-name"));
        Assert.NotNull(cut.Find("#member-role"));
        Assert.NotNull(cut.Find("#member-email"));
        Assert.Contains("Polidor", cut.Markup);
    }

    [Fact]
    public void TeamMemberModal_ShouldTriggerOnSave_WhenValid()
    {
        var model = new TeamMemberFormModel
        {
            FullName = "Maria Santos",
            Role = "Lavadora"
        };

        TeamMemberFormModel? saved = null;

        var cut = Render<TeamMemberModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.OnSave, m => saved = m));

        cut.Find("#btn-save-member").Click();

        Assert.NotNull(saved);
        Assert.Equal("Maria Santos", saved.FullName);
        Assert.Equal("Lavadora", saved.Role);
    }

    [Fact]
    public void CommissionRuleModal_ShouldRenderFields_AndTriggerOnSave()
    {
        var model = new CommissionRuleFormModel
        {
            ServiceName = "Lavagem Completa",
            RoleName = "Lavador",
            Percentage = 15m
        };

        CommissionRuleFormModel? saved = null;

        var cut = Render<CommissionRuleModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.OnSave, m => saved = m));

        Assert.NotNull(cut.Find("#commission-percentage"));
        Assert.Contains("15%", cut.Markup);

        cut.Find("#btn-save-commission").Click();

        Assert.NotNull(saved);
        Assert.Equal("Lavagem Completa", saved.ServiceName);
        Assert.Equal(15m, saved.Percentage);
    }
}
