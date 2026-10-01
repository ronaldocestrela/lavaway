using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.LoadFromRepository();
        var connectionString = DotEnvConfiguration.GetRequiredConnectionString();

        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

        return new IdentityModuleDbContext(options);
    }
}