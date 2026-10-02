using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

public sealed class ModuleModelTests
{
    [Fact]
    public void TenantOwnedEntities_ShouldHaveGlobalQueryFilters()
    {
        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new YardOperationsDbContext(options, new CurrentTenantAccessor());
        var tenantOwnedEntities = context.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType));

        Assert.NotEmpty(tenantOwnedEntities);
        Assert.All(tenantOwnedEntities, entityType => Assert.NotEmpty(entityType.GetDeclaredQueryFilters()));
    }

    [Fact]
    public void IdentityContext_ShouldFilterUsersAndRefreshTokensButKeepRolesGlobal()
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new IdentityModuleDbContext(options, new CurrentTenantAccessor());
        var user = context.Model.FindEntityType(typeof(ApplicationUser))!;
        var refreshToken = context.Model.FindEntityType(typeof(CarWashSaaS.Identity.Domain.RefreshToken))!;
        var role = context.Model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityRole<Guid>))!;

        Assert.NotEmpty(user.GetDeclaredQueryFilters());
        Assert.NotEmpty(refreshToken.GetDeclaredQueryFilters());
        Assert.Empty(role.GetDeclaredQueryFilters());
    }

    [Fact]
    public void SaveChanges_ShouldRejectTenantMismatchBeforeDatabaseAccess()
    {
        var currentTenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(currentTenantId);
        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new YardOperationsDbContext(options, accessor);
        context.Customers.Add(Customer.Create(Guid.NewGuid(), "Cross tenant", "555-0100").Value!);

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void SaveChanges_ShouldRejectTenantOwnedWritesWhenTenantIsUnresolved()
    {
        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new YardOperationsDbContext(options, new CurrentTenantAccessor());
        context.Customers.Add(Customer.Create(Guid.NewGuid(), "Unresolved tenant", "555-0102").Value!);

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void AddedTenantOwnedEntity_ShouldInheritCurrentTenantWhenTenantIsUnset()
    {
        var currentTenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(currentTenantId);
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new IdentityModuleDbContext(options, accessor);
        var user = new ApplicationUser { UserName = "new-user", TenantId = Guid.Empty };
        context.Users.Add(user);

        context.ChangeTracker.ValidateTenantWrites(accessor);

        Assert.Equal(currentTenantId, user.TenantId);
    }

    [Fact]
    public void TenantsContext_ShouldMapTenantToTenantsSchema()
    {
        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseSqlServer("Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new TenantsDbContext(options, new CurrentTenantAccessor());
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

        using var context = new IdentityModuleDbContext(options, new CurrentTenantAccessor());
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

        using var context = new YardOperationsDbContext(options, new CurrentTenantAccessor());
        var vehicle = context.Model.FindEntityType(typeof(Vehicle))!;
        var customer = context.Model.FindEntityType(typeof(Customer))!;
        var workOrder = context.Model.FindEntityType(typeof(WorkOrder))!;

        Assert.Equal("yard", vehicle.GetSchema());
        Assert.Contains(vehicle.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Vehicle.TenantId), nameof(Vehicle.Plate)]));
        Assert.Contains(vehicle.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Vehicle.TenantId), nameof(Vehicle.CustomerId)]));
        Assert.Contains(customer.GetIndexes(), index => !index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Customer.TenantId), nameof(Customer.NormalizedPhone)]));
        Assert.Contains(workOrder.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(WorkOrder.TenantId), nameof(WorkOrder.VehicleId)]));
        Assert.Contains(context.Model.GetEntityTypes(), entityType => entityType.ClrType == typeof(ServicePrice));
        Assert.Contains(context.Model.GetEntityTypes(), entityType => entityType.ClrType == typeof(WorkOrderItem));
    }
}
