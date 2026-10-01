using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class TenantsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TenantsDbContext>
{
    public TenantsDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.LoadFromRepository();
        var connectionString = DotEnvConfiguration.GetRequiredConnectionString();

        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .Options;

        return new TenantsDbContext(options);
    }
}