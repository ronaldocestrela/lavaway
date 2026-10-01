using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class WhatsAppDbContext(
    DbContextOptions<WhatsAppDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor) : DbContext(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    public DbSet<WhatsAppConnection> WhatsAppConnections => Set<WhatsAppConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var connection = modelBuilder.Entity<WhatsAppConnection>();
        connection.ToTable("WhatsAppConnections", "whatsapp");
        connection.HasKey(value => value.Id);
        connection.Property(value => value.Id).ValueGeneratedNever();
        connection.Property(value => value.TenantId).IsRequired();
        connection.Property(value => value.ProviderSessionId).HasMaxLength(200).IsRequired();
        connection.Property(value => value.QrCodeValue).HasMaxLength(2000).IsRequired();
        connection.Property(value => value.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        connection.Property(value => value.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        connection.Property(value => value.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();

        modelBuilder.ApplyTenantQueryFilters(this);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
