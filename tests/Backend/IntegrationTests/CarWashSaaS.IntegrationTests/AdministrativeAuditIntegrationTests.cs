using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.IntegrationTests;

[Collection(SqlServerFixture.CollectionName)]
public sealed class AdministrativeAuditIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AuditEvent_AddAndSearch_ShouldPersistAndFilterCorrectly()
    {
        var accessor = new CurrentTenantAccessor();
        await using var dbContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
        var repository = new AdministrativeAuditEventRepository(dbContext);

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var uniqueAction = $"Action_{Guid.NewGuid():N}";

        var evt1 = AdministrativeAuditEvent.Create(
            actorId: actorId,
            actorEmail: "superadmin@lavaway.com",
            actorRole: "SuperAdmin",
            actorRealm: "Platform",
            action: uniqueAction,
            targetType: PlatformTargetTypeConstants.Tenant,
            targetId: tenantId.ToString(),
            tenantId: tenantId,
            outcome: "Success",
            detailsJson: "{\"field\":\"value\"}").Value!;

        var evt2 = AdministrativeAuditEvent.Create(
            actorId: actorId,
            actorEmail: "superadmin@lavaway.com",
            actorRole: "SuperAdmin",
            actorRealm: "Platform",
            action: uniqueAction,
            targetType: PlatformTargetTypeConstants.Tenant,
            targetId: tenantId.ToString(),
            tenantId: tenantId,
            outcome: "Failure",
            errorMessage: "Simulation error").Value!;

        await repository.AddAsync(evt1);
        await repository.AddAsync(evt2);

        // Search by unique action
        var searchResult = await repository.SearchAsync(new AuditQueryFilter(Action: uniqueAction, Page: 1, PageSize: 10));
        Assert.Equal(2, searchResult.TotalCount);
        Assert.Equal(2, searchResult.Items.Count);

        // Filter by outcome = Failure
        var failureResult = await repository.SearchAsync(new AuditQueryFilter(Action: uniqueAction, Outcome: "Failure"));
        Assert.Single(failureResult.Items);
        Assert.Equal("Simulation error", failureResult.Items.First().ErrorMessage);

        // GetById
        var fetched = await repository.GetByIdAsync(evt1.Id);
        Assert.NotNull(fetched);
        Assert.Equal(evt1.Id, fetched.Id);
        Assert.Equal("superadmin@lavaway.com", fetched.ActorEmail);
    }

    [Fact]
    public async Task AuditEvent_Immutability_ShouldPreventModificationsAndDeletions()
    {
        var accessor = new CurrentTenantAccessor();
        await using var dbContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);

        var evt = AdministrativeAuditEvent.Create(
            actorId: Guid.NewGuid(),
            actorEmail: "auditor@lavaway.com",
            actorRole: "PlatformAuditor",
            actorRealm: "Platform",
            action: PlatformActionConstants.SensitiveDataExported,
            targetType: PlatformTargetTypeConstants.System,
            targetId: "All").Value!;

        await dbContext.AdministrativeAuditEvents.AddAsync(evt);
        await dbContext.SaveChangesAsync();

        // 1. Attempt update
        await using var updateContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
        var toModify = await updateContext.AdministrativeAuditEvents.FirstAsync(e => e.Id == evt.Id);

        // Use reflection to modify private setter
        var prop = typeof(AdministrativeAuditEvent).GetProperty(nameof(AdministrativeAuditEvent.ActorEmail));
        prop?.SetValue(toModify, "hacked@domain.com");
        updateContext.Entry(toModify).State = EntityState.Modified;

        var updateEx = await Assert.ThrowsAsync<InvalidOperationException>(() => updateContext.SaveChangesAsync());
        Assert.Contains("append-only", updateEx.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Attempt delete
        await using var deleteContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
        var toDelete = await deleteContext.AdministrativeAuditEvents.FirstAsync(e => e.Id == evt.Id);
        deleteContext.AdministrativeAuditEvents.Remove(toDelete);

        var deleteEx = await Assert.ThrowsAsync<InvalidOperationException>(() => deleteContext.SaveChangesAsync());
        Assert.Contains("append-only", deleteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlatformUser_AddAndVerify_ShouldPersistAndRejectDuplicateEmail()
    {
        var accessor = new CurrentTenantAccessor();
        await using var dbContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
        var repo = new PlatformUserRepository(dbContext);

        var uniqueEmail = $"operator_{Guid.NewGuid():N}@lavaway.com";
        var password = "SecurePassword123!";
        var passwordHash = repo.HashPassword(password);

        var user = PlatformUser.Create(
            uniqueEmail,
            "Operador de Teste",
            PlatformRole.PlatformSupport,
            passwordHash).Value!;

        await repo.AddAsync(user);

        // Verify lookup
        var loaded = await repo.FindByEmailAsync(uniqueEmail);
        Assert.NotNull(loaded);
        Assert.Equal("Operador de Teste", loaded.FullName);
        Assert.Equal(PlatformRole.PlatformSupport, loaded.Role);
        Assert.True(repo.VerifyPassword(loaded, password));
        Assert.False(repo.VerifyPassword(loaded, "WrongPass!"));

        // Verify duplicate email constraint
        await using var duplicateContext = new IdentityModuleDbContext(fixture.CreateIdentityOptions(), accessor);
        var duplicateRepo = new PlatformUserRepository(duplicateContext);
        var duplicateUser = PlatformUser.Create(
            uniqueEmail,
            "Outro Operador",
            PlatformRole.PlatformSupport,
            passwordHash).Value!;

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => duplicateRepo.AddAsync(duplicateUser));
    }
}
