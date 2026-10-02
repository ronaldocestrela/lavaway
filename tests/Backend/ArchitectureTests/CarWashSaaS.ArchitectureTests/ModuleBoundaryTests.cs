using System.Reflection;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;

namespace CarWashSaaS.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void DomainAssemblies_ShouldNotReferenceFrameworkOrInfrastructurePackages()
    {
        var domainAssemblies = new[]
        {
            typeof(Tenant).Assembly,
            typeof(CarWashSaaS.Identity.Domain.ShopRole).Assembly,
            typeof(Customer).Assembly
        };

        foreach (var assembly in domainAssemblies)
        {
            var forbiddenReference = assembly.GetReferencedAssemblies().FirstOrDefault(reference =>
                reference.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

            Assert.Null(forbiddenReference);
        }
    }

    [Fact]
    public void ModuleInfrastructureAssemblies_ShouldNotReferenceOtherModuleInfrastructure()
    {
        var moduleAssemblies = new[]
        {
            typeof(TenantsDbContext).Assembly,
            typeof(IdentityModuleDbContext).Assembly,
            typeof(YardOperationsDbContext).Assembly
        };
        var moduleInfrastructureNames = new HashSet<string>(
        [
            typeof(TenantsDbContext).Assembly.GetName().Name!,
            typeof(IdentityModuleDbContext).Assembly.GetName().Name!,
            typeof(YardOperationsDbContext).Assembly.GetName().Name!
        ], StringComparer.Ordinal);

        foreach (var assembly in moduleAssemblies)
        {
            var crossModuleReference = assembly.GetReferencedAssemblies()
                .FirstOrDefault(reference => moduleInfrastructureNames.Contains(reference.Name!) &&
                    reference.Name != assembly.GetName().Name);

            Assert.Null(crossModuleReference);
        }
    }

    [Fact]
    public void TenantOwnedRecords_ShouldImplementSharedTenantContract()
    {
        var tenantOwnedTypes = new[]
        {
            typeof(ApplicationUser),
            typeof(CarWashSaaS.Identity.Domain.RefreshToken),
            typeof(Customer),
            typeof(Vehicle),
            typeof(Service),
            typeof(ServicePrice),
            typeof(WorkOrder),
            typeof(WorkOrderItem)
        };

        Assert.All(tenantOwnedTypes, type => Assert.True(typeof(IMustHaveTenant).IsAssignableFrom(type), type.FullName));
    }

    [Fact]
    public void AggregateRoots_ShouldExposeGuidIdentifiers()
    {
        var aggregateRoots = new[]
        {
            typeof(Tenant),
            typeof(ApplicationUser),
            typeof(CarWashSaaS.Identity.Domain.RefreshToken),
            typeof(Customer),
            typeof(Vehicle),
            typeof(Service),
            typeof(WorkOrder)
        };

        Assert.All(aggregateRoots, type =>
        {
            var idProperty = type.GetProperty("Id");
            Assert.NotNull(idProperty);
            Assert.Equal(typeof(Guid), idProperty.PropertyType);
        });
    }
}