using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class VehicleInspectionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _workOrderId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    [Fact]
    public void Create_Should_Succeed_And_Initialize_Draft_Status_With_UuidV7()
    {
        var result = VehicleInspection.Create(
            _tenantId,
            _workOrderId,
            _vehicleId,
            fuelLevel: "Half",
            odometerKm: 45200,
            notes: "Pequena marca no para-choque dianteiro.");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(_tenantId, result.Value.TenantId);
        Assert.Equal(_workOrderId, result.Value.WorkOrderId);
        Assert.Equal(_vehicleId, result.Value.VehicleId);
        Assert.Equal("Half", result.Value.FuelLevel);
        Assert.Equal(45200, result.Value.OdometerKm);
        Assert.Equal(InspectionStatus.Draft, result.Value.Status);
        Assert.NotNull(result.Value.ChecklistItems);
        Assert.NotEmpty(result.Value.ChecklistItems); // Padrão com itens padrão inicializados
    }

    [Fact]
    public void Create_Should_Fail_When_Required_Ids_Are_Empty()
    {
        var result = VehicleInspection.Create(Guid.Empty, _workOrderId, _vehicleId);
        Assert.False(result.IsSuccess);
        Assert.Equal("inspection.owner.required", result.Error?.Code);
    }

    [Fact]
    public void AddDamage_Should_Add_Damage_When_Coordinates_Are_Valid()
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;

        var addResult = inspection.AddDamage(
            DamageType.Scratch,
            VehicleView.LeftSide,
            coordinateX: 25.5m,
            coordinateY: 48.0m,
            DamageSeverity.Medium,
            "Risco superficial na porta traseira esquerda");

        Assert.True(addResult.IsSuccess);
        Assert.Single(inspection.Damages);
        var damage = inspection.Damages.First();
        Assert.Equal(DamageType.Scratch, damage.Type);
        Assert.Equal(VehicleView.LeftSide, damage.View);
        Assert.Equal(25.5m, damage.CoordinateX);
        Assert.Equal(48.0m, damage.CoordinateY);
        Assert.Equal(DamageSeverity.Medium, damage.Severity);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(101, 50)]
    [InlineData(50, -0.5)]
    [InlineData(50, 100.1)]
    public void AddDamage_Should_Fail_When_Coordinates_Are_Out_Of_Range(decimal x, decimal y)
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;

        var addResult = inspection.AddDamage(
            DamageType.Dent,
            VehicleView.Front,
            x,
            y,
            DamageSeverity.Low);

        Assert.False(addResult.IsSuccess);
        Assert.Equal("inspection.coordinates.invalid", addResult.Error?.Code);
    }

    [Fact]
    public void RemoveDamage_Should_Remove_Existing_Damage()
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        var addResult = inspection.AddDamage(DamageType.Dent, VehicleView.Front, 50m, 50m, DamageSeverity.Low);
        var damageId = addResult.Value!.Id;

        var removeResult = inspection.RemoveDamage(damageId);

        Assert.True(removeResult.IsSuccess);
        Assert.Empty(inspection.Damages);
    }

    [Fact]
    public void Complete_Should_Fail_When_Required_Perimeter_Photos_Are_Missing()
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        // Anexa apenas 2 fotos
        inspection.AddPhoto(InspectionPhotoCategory.Front, "path/front.jpg", "front.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.Rear, "path/rear.jpg", "rear.jpg", "image/jpeg", 1024);

        var completeResult = inspection.Complete();

        Assert.False(completeResult.IsSuccess);
        Assert.Equal("inspection.required_photos_missing", completeResult.Error?.Code);
    }

    [Fact]
    public void Complete_Should_Succeed_When_All_Four_Perimeter_Photos_Exist()
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        inspection.AddPhoto(InspectionPhotoCategory.Front, "path/front.jpg", "front.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.Rear, "path/rear.jpg", "rear.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.LeftSide, "path/left.jpg", "left.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.RightSide, "path/right.jpg", "right.jpg", "image/jpeg", 1024);

        var completeResult = inspection.Complete();

        Assert.True(completeResult.IsSuccess);
        Assert.Equal(InspectionStatus.Completed, inspection.Status);
        Assert.NotNull(inspection.CompletedAtUtc);
    }

    [Fact]
    public void Modifications_Should_Fail_When_Inspection_Is_Completed()
    {
        var inspection = VehicleInspection.Create(_tenantId, _workOrderId, _vehicleId).Value!;
        inspection.AddPhoto(InspectionPhotoCategory.Front, "path/front.jpg", "front.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.Rear, "path/rear.jpg", "rear.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.LeftSide, "path/left.jpg", "left.jpg", "image/jpeg", 1024);
        inspection.AddPhoto(InspectionPhotoCategory.RightSide, "path/right.jpg", "right.jpg", "image/jpeg", 1024);
        inspection.Complete();

        var addDamageResult = inspection.AddDamage(DamageType.Scratch, VehicleView.Front, 10m, 10m, DamageSeverity.Low);
        Assert.False(addDamageResult.IsSuccess);
        Assert.Equal("inspection.already_completed", addDamageResult.Error?.Code);

        var addPhotoResult = inspection.AddPhoto(InspectionPhotoCategory.Interior, "path/in.jpg", "in.jpg", "image/jpeg", 1024);
        Assert.False(addPhotoResult.IsSuccess);
        Assert.Equal("inspection.already_completed", addPhotoResult.Error?.Code);
    }
}
