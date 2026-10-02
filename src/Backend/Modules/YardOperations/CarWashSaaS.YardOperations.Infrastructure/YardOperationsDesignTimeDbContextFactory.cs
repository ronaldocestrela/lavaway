using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<YardOperationsDbContext>
{
    public YardOperationsDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.LoadFromRepository();
        var connectionString = DotEnvConfiguration.GetRequiredConnectionString();

        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard"))
            .Options;

        return new YardOperationsDbContext(options, new CurrentTenantAccessor());
    }
}
