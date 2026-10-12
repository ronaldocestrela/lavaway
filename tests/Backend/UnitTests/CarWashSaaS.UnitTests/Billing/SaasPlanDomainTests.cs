using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class SaasPlanDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiatePlanCorrectly()
    {
        // Act
        var result = SaasPlan.Create(
            tier: SaasPlanTier.Basic,
            name: "Básico",
            description: "Plano para iniciantes",
            monthlyPrice: 149.00m,
            maxWorkOrdersPerCycle: 150,
            maxWhatsAppMessagesPerCycle: 300,
            hasCustomerSubscriptions: false,
            hasLoyalty: false,
            hasCommissions: false,
            hasAiChatbot: false,
            maxTeamMembers: 3,
            maxBoxes: 2,
            isActive: true);

        // Assert
        Assert.True(result.IsSuccess);
        var plan = result.Value!;
        Assert.Equal(SaasPlanTier.Basic, plan.Tier);
        Assert.Equal("Básico", plan.Name);
        Assert.Equal(149.00m, plan.MonthlyPrice);
        Assert.Equal(3, plan.MaxTeamMembers);
        Assert.Equal(2, plan.MaxBoxes);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Create_WithNegativeBoxes_ShouldFailValidation()
    {
        // Act
        var result = SaasPlan.Create(
            tier: SaasPlanTier.Basic,
            name: "Básico",
            description: "Plano inválido",
            monthlyPrice: 149.00m,
            maxWorkOrdersPerCycle: 150,
            maxWhatsAppMessagesPerCycle: 300,
            hasCustomerSubscriptions: false,
            hasLoyalty: false,
            hasCommissions: false,
            hasAiChatbot: false,
            maxTeamMembers: 3,
            maxBoxes: -1,
            isActive: true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("saas_plan.boxes.invalid", result.Error!.Code);
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateAllFields()
    {
        // Arrange
        var plan = SaasPlan.Create(
            tier: SaasPlanTier.Pro,
            name: "Pro Original",
            description: "Original",
            monthlyPrice: 299.00m,
            maxWorkOrdersPerCycle: 600,
            maxWhatsAppMessagesPerCycle: 1500,
            hasCustomerSubscriptions: true,
            hasLoyalty: true,
            hasCommissions: true,
            hasAiChatbot: true,
            maxTeamMembers: 10,
            maxBoxes: 5,
            isActive: true).Value!;

        // Act
        var result = plan.UpdateDetails(
            name: "Pro Turbo",
            description: "Descrição Atualizada",
            monthlyPrice: 349.90m,
            maxWorkOrdersPerCycle: 800,
            maxWhatsAppMessagesPerCycle: 2000,
            maxTeamMembers: 15,
            maxBoxes: 8,
            hasCustomerSubscriptions: true,
            hasLoyalty: false,
            hasCommissions: true,
            hasAiChatbot: true,
            isActive: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Pro Turbo", plan.Name);
        Assert.Equal("Descrição Atualizada", plan.Description);
        Assert.Equal(349.90m, plan.MonthlyPrice);
        Assert.Equal(800, plan.MaxWorkOrdersPerCycle);
        Assert.Equal(2000, plan.MaxWhatsAppMessagesPerCycle);
        Assert.Equal(15, plan.MaxTeamMembers);
        Assert.Equal(8, plan.MaxBoxes);
        Assert.False(plan.HasLoyalty);
    }

    [Theory]
    [InlineData("", "saas_plan.name.required")]
    [InlineData("   ", "saas_plan.name.required")]
    public void UpdateDetails_WithEmptyName_ShouldFailValidation(string invalidName, string expectedErrorCode)
    {
        // Arrange
        var plan = SaasPlan.GetStandardPlans()[0];

        // Act
        var result = plan.UpdateDetails(
            invalidName,
            "Desc",
            100m,
            100,
            100,
            5,
            3,
            false,
            false,
            false,
            false,
            true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error!.Code);
    }

    [Fact]
    public void UpdateDetails_WithNegativePriceOrNegativeBoxes_ShouldFailValidation()
    {
        // Arrange
        var plan = SaasPlan.GetStandardPlans()[0];

        // Act & Assert (preço negativo)
        var priceResult = plan.UpdateDetails("Nome", "Desc", -10m, 100, 100, 5, 3, false, false, false, false, true);
        Assert.False(priceResult.IsSuccess);
        Assert.Equal("saas_plan.price.invalid", priceResult.Error!.Code);

        // Act & Assert (boxes negativo)
        var boxResult = plan.UpdateDetails("Nome", "Desc", 100m, 100, 100, 5, -2, false, false, false, false, true);
        Assert.False(boxResult.IsSuccess);
        Assert.Equal("saas_plan.boxes.invalid", boxResult.Error!.Code);
    }

    [Fact]
    public void StandardPlans_ShouldHaveMaxBoxesConfigured()
    {
        // Act
        var standardPlans = SaasPlan.GetStandardPlans();

        // Assert
        Assert.Equal(3, standardPlans.Count);
        var basic = standardPlans.First(p => p.Tier == SaasPlanTier.Basic);
        var pro = standardPlans.First(p => p.Tier == SaasPlanTier.Pro);
        var enterprise = standardPlans.First(p => p.Tier == SaasPlanTier.Enterprise);

        Assert.True(basic.MaxBoxes > 0);
        Assert.True(pro.MaxBoxes > basic.MaxBoxes);
        Assert.Equal(0, enterprise.MaxBoxes); // 0 = ilimitado
    }
}
