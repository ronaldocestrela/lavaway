using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class WhatsAppDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WhatsAppDbContext>
{
    public WhatsAppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CarWashSaaS_ConnectionString")
            ?? "Server=(localdb)\\mssqllocaldb;Database=CarWashSaaS;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<WhatsAppDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp"))
            .Options;

        return new WhatsAppDbContext(options, new CurrentTenantAccessor());
    }
}
