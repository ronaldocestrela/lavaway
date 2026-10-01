using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CarWashSaaS")
            ?? "Server=localhost;Database=CarWashSaaS;Integrated Security=True;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

        return new IdentityModuleDbContext(options);
    }
}