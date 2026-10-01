using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

public sealed class ModuleModelTests
{
    [Fact]
    public void TenantsContext_ShouldMapTenantToTenantsSchema()
    {
        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new TenantsDbContext(options);
        var tenant = context.Model.FindEntityType(typeof(Tenant))!;

        Assert.Equal("Tenants", tenant.GetTableName());
        Assert.Equal("tenants", tenant.GetSchema());
        Assert.Equal("uniqueidentifier", tenant.FindProperty(nameof(Tenant.Id))!.GetColumnType());
    }

    [Fact]
    public void IdentityContext_ShouldUseGuidKeysAndDedicatedSchema()
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new IdentityModuleDbContext(options);
        var user = context.Model.FindEntityType(typeof(ApplicationUser))!;

        Assert.Equal("identity", context.Model.GetDefaultSchema());
        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.Equal("uniqueidentifier", user.FindProperty(nameof(ApplicationUser.Id))!.GetColumnType());
        Assert.True(user.FindProperty(nameof(ApplicationUser.TenantId))!.IsNullable is false);
    }

    [Fact]
    public void YardOperationsContext_ShouldEnforceTenantScopedRelationshipsAndPlateUniqueness()
    {
        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new YardOperationsDbContext(options);
        var vehicle = context.Model.FindEntityType(typeof(Vehicle))!;
        var workOrder = context.Model.FindEntityType(typeof(WorkOrder))!;

        Assert.Equal("yard", vehicle.GetSchema());
        Assert.Contains(vehicle.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Vehicle.TenantId), nameof(Vehicle.Plate)]));
        Assert.Contains(vehicle.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Vehicle.TenantId), nameof(Vehicle.CustomerId)]));
        Assert.Contains(workOrder.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(WorkOrder.TenantId), nameof(WorkOrder.VehicleId)]));
        Assert.Contains(context.Model.GetEntityTypes(), entityType => entityType.ClrType == typeof(ServicePrice));
        Assert.Contains(context.Model.GetEntityTypes(), entityType => entityType.ClrType == typeof(WorkOrderItem));
    }
}