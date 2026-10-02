using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.WhatsApp.Infrastructure;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CarWashSaaS.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string CollectionName = "SQL Server tenant isolation";

    private MsSqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("CarWash_Test_only_123!")
            .Build();

        await _container.StartAsync();

        await using var tenants = new TenantsDbContext(CreateTenantsOptions(), new CurrentTenantAccessor());
        await tenants.Database.MigrateAsync();

        await using var identity = new IdentityModuleDbContext(CreateIdentityOptions(), new CurrentTenantAccessor());
        await identity.Database.MigrateAsync();

        await using var yardOperations = new YardOperationsDbContext(CreateYardOperationsOptions(), new CurrentTenantAccessor());
        await yardOperations.Database.MigrateAsync();

        await using var whatsApp = new WhatsAppDbContext(CreateWhatsAppOptions(), new CurrentTenantAccessor());
        await whatsApp.Database.MigrateAsync();
    }

    public DbContextOptions<TenantsDbContext> CreateTenantsOptions() =>
        new DbContextOptionsBuilder<TenantsDbContext>()
            .UseSqlServer(_container!.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .Options;

    public DbContextOptions<IdentityModuleDbContext> CreateIdentityOptions() =>
        new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer(_container!.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

    public DbContextOptions<YardOperationsDbContext> CreateYardOperationsOptions() =>
        new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer(_container!.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard"))
            .Options;

    public DbContextOptions<WhatsAppDbContext> CreateWhatsAppOptions() =>
        new DbContextOptionsBuilder<WhatsAppDbContext>()
            .UseSqlServer(_container!.GetConnectionString(), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp"))
            .Options;

    public Task DisposeAsync() => _container is null ? Task.CompletedTask : _container.DisposeAsync().AsTask();
}

[CollectionDefinition(SqlServerFixture.CollectionName)]
public sealed class SqlServerTenantIsolationCollection : ICollectionFixture<SqlServerFixture>;