using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public class AfterSalesDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private WorkOrder CreateTestWorkOrder()
    {
        var itemResult = WorkOrderItem.Create(_tenantId, Guid.NewGuid(), "Lavagem Completa", 60m, 45, 1, ServiceCategoryConstants.LavagemCompleta);
        var orderResult = WorkOrder.Create(_tenantId, _customerId, _vehicleId, [itemResult.Value!]);
        return orderResult.Value!;
    }

    [Fact]
    public void RegisterPickup_Should_Fail_When_WorkOrder_Is_Not_ReadyForPickup()
    {
        var order = CreateTestWorkOrder();
        Assert.Equal(WorkOrderStatus.Waiting, order.Status);

        var result = order.RegisterPickup();

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.not_ready_for_pickup", result.Error?.Code);
        Assert.Null(order.PickedUpAtUtc);
    }

    [Fact]
    public void RegisterPickup_Should_Succeed_When_WorkOrder_Is_ReadyForPickup()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);
        order.ChangeStatus(WorkOrderStatus.ReadyForPickup);

        var pickupTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var result = order.RegisterPickup(pickupTime, "Entregue com chave na mão");

        Assert.True(result.IsSuccess);
        Assert.Equal(pickupTime, order.PickedUpAtUtc);
        Assert.Contains(order.StatusHistory, h => h.Notes != null && h.Notes.Contains("Entregue com chave"));
    }

    [Fact]
    public void RegisterPickup_Should_Fail_When_Already_PickedUp()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);
        order.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        order.RegisterPickup();

        var secondResult = order.RegisterPickup();

        Assert.False(secondResult.IsSuccess);
        Assert.Equal("work_order.already_picked_up", secondResult.Error?.Code);
    }

    [Fact]
    public void RecordSatisfactionSurveySent_Should_Fail_Before_Pickup()
    {
        var order = CreateTestWorkOrder();

        var result = order.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.survey_before_pickup", result.Error?.Code);
    }

    [Fact]
    public void RecordSatisfactionSurveySent_Should_Succeed_After_Pickup()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);
        order.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        order.RegisterPickup();

        var sentAt = DateTimeOffset.UtcNow;
        var result = order.RecordSatisfactionSurveySent(sentAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(sentAt, order.SurveySentAtUtc);
    }

    [Fact]
    public void RecordSatisfactionSurveySent_Should_Fail_When_Already_Sent()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);
        order.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        order.RegisterPickup();
        order.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow);

        var secondResult = order.RecordSatisfactionSurveySent(DateTimeOffset.UtcNow);

        Assert.False(secondResult.IsSuccess);
        Assert.Equal("work_order.survey_already_sent", secondResult.Error?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void RecordSatisfactionRating_Should_Fail_For_Invalid_Star_Ratings(int invalidRating)
    {
        var order = CreateTestWorkOrder();

        var result = order.RecordSatisfactionRating(invalidRating);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.survey_rating_invalid", result.Error?.Code);
        Assert.Null(order.SurveyRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void RecordSatisfactionRating_Should_Succeed_For_Valid_Ratings(int validRating)
    {
        var order = CreateTestWorkOrder();
        var respondedAt = DateTimeOffset.UtcNow;

        var result = order.RecordSatisfactionRating(validRating, "Ótimo atendimento!", respondedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(validRating, order.SurveyRating);
        Assert.Equal("Ótimo atendimento!", order.SurveyFeedback);
        Assert.Equal(respondedAt, order.SurveyRespondedAtUtc);
    }

    [Fact]
    public void ReactivationCampaignRule_Should_Create_And_Update_Successfully()
    {
        var ruleResult = ReactivationCampaignRule.Create(
            _tenantId,
            15,
            "Régua de 15 Dias",
            "Olá {cliente}, seu veículo {veiculo} está com saudades!",
            isEnabled: true,
            promotionalOffer: "10% OFF");

        Assert.True(ruleResult.IsSuccess);
        var rule = ruleResult.Value!;
        Assert.Equal(_tenantId, rule.TenantId);
        Assert.Equal(15, rule.DaysInactive);
        Assert.Equal("Régua de 15 Dias", rule.Title);
        Assert.True(rule.IsEnabled);
        Assert.Equal("10% OFF", rule.PromotionalOffer);

        var updateResult = rule.Update(
            "Régua 15D Atualizada",
            "Nova mensagem {cliente}",
            isEnabled: false,
            promotionalOffer: null);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Régua 15D Atualizada", rule.Title);
        Assert.False(rule.IsEnabled);
        Assert.Null(rule.PromotionalOffer);
    }

    [Fact]
    public void ReactivationCampaignLog_Should_Validate_Required_Fields()
    {
        var validResult = ReactivationCampaignLog.Create(
            _tenantId,
            Guid.NewGuid(),
            _customerId,
            "11999998888",
            15,
            "key-123");

        Assert.True(validResult.IsSuccess);
        Assert.Equal("11999998888", validResult.Value!.CustomerPhone);
        Assert.Equal("key-123", validResult.Value!.IdempotencyKey);

        var invalidResult = ReactivationCampaignLog.Create(
            _tenantId,
            Guid.Empty,
            _customerId,
            "11999998888",
            15,
            "key-123");

        Assert.False(invalidResult.IsSuccess);
        Assert.Equal("campaign_log.required_fields", invalidResult.Error?.Code);
    }
}
