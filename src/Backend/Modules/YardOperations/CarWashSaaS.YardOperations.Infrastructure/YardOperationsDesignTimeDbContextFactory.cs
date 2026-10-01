using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<YardOperationsDbContext>
{
    public YardOperationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CarWashSaaS")
            ?? "Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<YardOperationsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard"))
            .Options;

        return new YardOperationsDbContext(options);
    }
}