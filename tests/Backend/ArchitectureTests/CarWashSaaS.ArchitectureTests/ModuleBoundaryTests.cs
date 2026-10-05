using System.Reflection;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using CarWashSaaS.WhatsApp.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using NetArchTest.Rules;

namespace CarWashSaaS.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(Tenant).Assembly,
        typeof(ShopRole).Assembly,
        typeof(Customer).Assembly,
        typeof(WhatsAppConnection).Assembly
    ];

    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(StoreProfileApplicationService).Assembly,
        typeof(IdentityApplicationService).Assembly,
        typeof(CustomerVehicleApplicationService).Assembly,
        typeof(WhatsAppConnectionApplicationService).Assembly
    ];

    private static readonly Assembly[] InfrastructureAssemblies =
    [
        typeof(TenantsDbContext).Assembly,
        typeof(IdentityModuleDbContext).Assembly,
        typeof(YardOperationsDbContext).Assembly,
        typeof(WhatsAppDbContext).Assembly
    ];

    [Fact]
    public void DomainAssemblies_ShouldNotReferenceInfrastructure_OrPresentation_OrFrameworks()
    {
        var forbiddenDependencies = new[]
        {
            "CarWashSaaS.Api",
            "CarWashSaaS.Tenants.Infrastructure",
            "CarWashSaaS.Identity.Infrastructure",
            "CarWashSaaS.YardOperations.Infrastructure",
            "CarWashSaaS.WhatsApp.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore"
        };

        foreach (var assembly in DomainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Assembly {assembly.GetName().Name} has forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void DomainAssemblies_ShouldNotReferenceApplicationAssemblies()
    {
        var applicationNamespaces = new[]
        {
            "CarWashSaaS.Tenants.Application",
            "CarWashSaaS.Identity.Application",
            "CarWashSaaS.YardOperations.Application",
            "CarWashSaaS.WhatsApp.Application"
        };

        foreach (var assembly in DomainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(applicationNamespaces)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Domain assembly {assembly.GetName().Name} should not depend on application layers: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void ApplicationAssemblies_ShouldNotReferenceInfrastructureOrPresentation()
    {
        var forbiddenDependencies = new[]
        {
            "CarWashSaaS.Api",
            "CarWashSaaS.Tenants.Infrastructure",
            "CarWashSaaS.Identity.Infrastructure",
            "CarWashSaaS.YardOperations.Infrastructure",
            "CarWashSaaS.WhatsApp.Infrastructure"
        };

        foreach (var assembly in ApplicationAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Application assembly {assembly.GetName().Name} should not depend on infrastructure/API: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void ModuleInfrastructureAssemblies_ShouldNotReferenceOtherModuleInfrastructure()
    {
        var infrastructureNames = InfrastructureAssemblies
            .Select(a => a.GetName().Name!)
            .ToArray();

        foreach (var assembly in InfrastructureAssemblies)
        {
            var otherInfrastructures = infrastructureNames
                .Where(name => name != assembly.GetName().Name)
                .ToArray();

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherInfrastructures)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Module infrastructure {assembly.GetName().Name} must not reference other module infrastructures: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void TenantOwnedRecords_ShouldImplementSharedTenantContract()
    {
        var tenantOwnedTypes = new[]
        {
            typeof(StoreProfile),
            typeof(ApplicationUser),
            typeof(RefreshToken),
            typeof(Customer),
            typeof(Vehicle),
            typeof(Service),
            typeof(ServicePrice),
            typeof(YardCapacity),
            typeof(TeamMember),
            typeof(CommissionRule),
            typeof(WorkOrder),
            typeof(WorkOrderItem),
            typeof(WorkOrderStatusHistory),
            typeof(WhatsAppConnection),
            typeof(VehicleInspection),
            typeof(InspectionDamage),
            typeof(InspectionChecklistItem),
            typeof(InspectionPhoto),
            typeof(PostServicePhoto),
            typeof(Booking),
            typeof(ChatbotConversationSession)
        };

        Assert.All(tenantOwnedTypes, type =>
            Assert.True(typeof(IMustHaveTenant).IsAssignableFrom(type), $"{type.FullName} must implement IMustHaveTenant"));
    }

    [Fact]
    public void AggregateRoots_ShouldExposeGuidIdentifiers()
    {
        var aggregateRoots = new[]
        {
            typeof(Tenant),
            typeof(StoreProfile),
            typeof(ApplicationUser),
            typeof(RefreshToken),
            typeof(Customer),
            typeof(Vehicle),
            typeof(Service),
            typeof(YardCapacity),
            typeof(TeamMember),
            typeof(CommissionRule),
            typeof(WorkOrder),
            typeof(WhatsAppConnection),
            typeof(VehicleInspection),
            typeof(Booking),
            typeof(ChatbotConversationSession)
        };

        Assert.All(aggregateRoots, type =>
        {
            var idProperty = type.GetProperty("Id");
            Assert.NotNull(idProperty);
            Assert.Equal(typeof(Guid), idProperty.PropertyType);
        });
    }

    [Fact]
    public void RepositoryInterfaces_ShouldNotExposeIQueryable()
    {
        foreach (var assembly in ApplicationAssemblies)
        {
            var repositoryInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal));

            foreach (var repoInterface in repositoryInterfaces)
            {
                foreach (var method in repoInterface.GetMethods())
                {
                    var returnType = method.ReturnType;
                    var exposesQueryable = returnType.IsGenericType &&
                        returnType.GetGenericTypeDefinition() == typeof(IQueryable<>) ||
                        returnType == typeof(IQueryable);

                    Assert.False(
                        exposesQueryable,
                        $"Repository interface {repoInterface.Name}.{method.Name} must not expose IQueryable.");
                }
            }
        }
    }

    [Fact]
    public void ApplicationServices_PublicMethods_ShouldReturnResultPattern()
    {
        foreach (var assembly in ApplicationAssemblies)
        {
            var applicationServices = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ApplicationService", StringComparison.Ordinal));

            foreach (var service in applicationServices)
            {
                var publicMethods = service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName);

                foreach (var method in publicMethods)
                {
                    var returnType = method.ReturnType;
                    var unwrappedType = returnType;
                    if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        unwrappedType = returnType.GetGenericArguments()[0];
                    }

                    var isResult = unwrappedType == typeof(Result) ||
                        (unwrappedType.IsGenericType && unwrappedType.GetGenericTypeDefinition() == typeof(Result<>));

                    Assert.True(
                        isResult,
                        $"Application service method {service.Name}.{method.Name} must return Result or Result<T>. Found: {returnType.Name}");
                }
            }
        }
    }
}
