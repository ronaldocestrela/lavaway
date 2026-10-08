using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.Billing.Infrastructure;

public sealed class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.LoadFromRepository();
        var connectionString = DotEnvConfiguration.GetRequiredConnectionString();

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "billing"))
            .Options;

        return new BillingDbContext(options, new CurrentTenantAccessor());
    }
}
