using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class TenantsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TenantsDbContext>
{
    public TenantsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CarWashSaaS")
            ?? "Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .Options;

        return new TenantsDbContext(options);
    }
}