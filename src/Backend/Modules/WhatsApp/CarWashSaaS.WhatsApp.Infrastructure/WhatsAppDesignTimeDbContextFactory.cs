using CarWashSaaS.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class WhatsAppDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WhatsAppDbContext>
{
    public WhatsAppDbContext CreateDbContext(string[] args)
    {
        DotEnvConfiguration.LoadFromRepository();
        var connectionString = DotEnvConfiguration.GetRequiredConnectionString();

        var options = new DbContextOptionsBuilder<WhatsAppDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp"))
            .Options;

        return new WhatsAppDbContext(options, new CurrentTenantAccessor());
    }
}
