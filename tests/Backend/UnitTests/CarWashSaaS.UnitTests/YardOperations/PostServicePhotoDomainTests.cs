using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class PostServicePhotoDomainTests
{
    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _customerId = Guid.CreateVersion7();
    private readonly Guid _vehicleId = Guid.CreateVersion7();

    [Theory]
    [InlineData("Polimento", "Polimento Técnico", true)]
    [InlineData("Vitrificação", "Vitrificação Cerâmica 9H", true)]
    [InlineData("Higienização", "Higienização Completa de Bancos", true)]
    [InlineData("Estética", "Detalhamento de Pintura", true)]
    [InlineData("Lavagem", "Lavagem Simples", false)]
    [InlineData("Ducha", "Ducha Rápida", false)]
    public void EligibilityRule_ShouldCorrectlyIdentifyEligibility(string category, string name, bool expected)
    {
        var item = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), name, 100m, 60, 1, category).Value!;
        var isEligible = PostServiceEligibilityRule.IsEligible(item);

        Assert.Equal(expected, isEligible);
    }

    [Fact]
    public void AddPostServicePhoto_ShouldFail_WhenStatusIsWaiting()
    {
        var eligibleItem = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), "Polimento Técnico", 300m, 120, 1, "Polimento").Value!;
        var workOrder = WorkOrder.Create(_tenantId, _customerId, _vehicleId, [eligibleItem]).Value!;

        Assert.Equal(WorkOrderStatus.Waiting, workOrder.Status);

        var result = workOrder.AddPostServicePhoto(
            eligibleItem.Id,
            Guid.CreateVersion7(),
            InspectionPhotoCategory.Front,
            "Capô Polido",
            "post-service-photos/after.jpg",
            "after.jpg",
            "image/jpeg",
            1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.invalid_status_for_post_service_photos", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void AddPostServicePhoto_ShouldFail_WhenNoEligibleServicesInWorkOrder()
    {
        var simpleItem = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), "Ducha Rápida", 40m, 20, 1, "Ducha").Value!;
        var workOrder = WorkOrder.Create(_tenantId, _customerId, _vehicleId, [simpleItem]).Value!;
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);

        var result = workOrder.AddPostServicePhoto(
            simpleItem.Id,
            null,
            InspectionPhotoCategory.LeftSide,
            "Lateral Limpa",
            "post-service-photos/after.jpg",
            "after.jpg",
            "image/jpeg",
            1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.no_eligible_services", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void AddPostServicePhoto_ShouldFail_WhenSpecifiedItemIsNotEligible()
    {
        var eligibleItem = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), "Vitrificação 9H", 500m, 180, 1, "Vitrificação").Value!;
        var simpleItem = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), "Ducha", 30m, 15, 1, "Ducha").Value!;
        var workOrder = WorkOrder.Create(_tenantId, _customerId, _vehicleId, [eligibleItem, simpleItem]).Value!;
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);

        var result = workOrder.AddPostServicePhoto(
            simpleItem.Id,
            null,
            InspectionPhotoCategory.LeftSide,
            "Ducha",
            "post-service-photos/after.jpg",
            "after.jpg",
            "image/jpeg",
            1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.item_not_eligible", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void AddPostServicePhoto_ShouldSucceed_AndSupportRemoval()
    {
        var eligibleItem = WorkOrderItem.Create(_tenantId, Guid.CreateVersion7(), "Vitrificação 9H", 500m, 180, 1, "Vitrificação").Value!;
        var workOrder = WorkOrder.Create(_tenantId, _customerId, _vehicleId, [eligibleItem]).Value!;
        workOrder.ChangeStatus(WorkOrderStatus.InWashing);
        workOrder.ChangeStatus(WorkOrderStatus.Finishing);

        var beforePhotoId = Guid.CreateVersion7();
        var addResult = workOrder.AddPostServicePhoto(
            eligibleItem.Id,
            beforePhotoId,
            InspectionPhotoCategory.Front,
            "Capô com Vitrificação 9H",
            "post-service-photos/after.jpg",
            "after.jpg",
            "image/jpeg",
            2048,
            "Brilho espelhado e proteção hidrofóbica");

        Assert.True(addResult.IsSuccess);
        Assert.Single(workOrder.PostServicePhotos);

        var photo = workOrder.PostServicePhotos.First();
        Assert.Equal(eligibleItem.Id, photo.WorkOrderItemId);
        Assert.Equal(beforePhotoId, photo.BeforeInspectionPhotoId);
        Assert.Equal(InspectionPhotoCategory.Front, photo.Category);
        Assert.Equal("Capô com Vitrificação 9H", photo.Title);
        Assert.Equal("Brilho espelhado e proteção hidrofóbica", photo.Notes);
        Assert.Equal(_tenantId, photo.TenantId);

        // Remove
        var removeResult = workOrder.RemovePostServicePhoto(photo.Id);
        Assert.True(removeResult.IsSuccess);
        Assert.Empty(workOrder.PostServicePhotos);

        // Remove non-existent
        var removeAgain = workOrder.RemovePostServicePhoto(photo.Id);
        Assert.False(removeAgain.IsSuccess);
        Assert.Equal("post_service_photo.not_found", removeAgain.Error!.Code);
    }
}
