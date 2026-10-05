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
    public DbSet<OutboundWhatsAppMessage> OutboundWhatsAppMessages => Set<OutboundWhatsAppMessage>();
    public DbSet<WhatsAppDeliveryAttempt> WhatsAppDeliveryAttempts => Set<WhatsAppDeliveryAttempt>();
    public DbSet<TenantWhatsAppQuota> TenantWhatsAppQuotas => Set<TenantWhatsAppQuota>();
    public DbSet<CustomerCommunicationPreference> CustomerCommunicationPreferences => Set<CustomerCommunicationPreference>();

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

        var message = modelBuilder.Entity<OutboundWhatsAppMessage>();
        message.ToTable("WhatsAppMessages", "whatsapp");
        message.HasKey(value => value.Id);
        message.Property(value => value.Id).ValueGeneratedNever();
        message.Property(value => value.TenantId).IsRequired();
        message.Property(value => value.RecipientPhone).HasMaxLength(32).IsRequired();
        message.Property(value => value.Body).HasMaxLength(4096).IsRequired();
        message.Property(value => value.IdempotencyKey).HasMaxLength(128).IsRequired();
        message.Property(value => value.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        message.Property(value => value.ProviderMessageId).HasMaxLength(256);
        message.Property(value => value.FailureReason).HasMaxLength(1024);
        message.Property(value => value.AttemptCount).IsRequired();
        message.Property(value => value.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        message.Property(value => value.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        message.Property(value => value.SentAtUtc).HasColumnType("datetimeoffset");
        message.Property(value => value.DeliveredAtUtc).HasColumnType("datetimeoffset");
        message.Property(value => value.ReadAtUtc).HasColumnType("datetimeoffset");
        message.Property(value => value.MediaType).HasMaxLength(32);
        message.Property(value => value.MediaUrlOrBase64);
        message.Property(value => value.MediaMimeType).HasMaxLength(64);
        message.Property(value => value.MediaFileName).HasMaxLength(256);
        message.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique();
        message.HasIndex(value => new { value.TenantId, value.CreatedAt });
        message.HasMany(value => value.DeliveryAttempts)
            .WithOne()
            .HasForeignKey(value => value.OutboundWhatsAppMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        var attempt = modelBuilder.Entity<WhatsAppDeliveryAttempt>();
        attempt.ToTable("WhatsAppDeliveryAttempts", "whatsapp");
        attempt.HasKey(value => value.Id);
        attempt.Property(value => value.Id).ValueGeneratedNever();
        attempt.Property(value => value.AttemptNumber).IsRequired();
        attempt.Property(value => value.AttemptedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        attempt.Property(value => value.IsSuccess).IsRequired();
        attempt.Property(value => value.ErrorCode).HasMaxLength(64);
        attempt.Property(value => value.ErrorMessage).HasMaxLength(1024);
        attempt.Property(value => value.HttpStatusCode);

        var quota = modelBuilder.Entity<TenantWhatsAppQuota>();
        quota.ToTable("TenantWhatsAppQuotas", "whatsapp");
        quota.HasKey(value => value.Id);
        quota.Property(value => value.Id).ValueGeneratedNever();
        quota.Property(value => value.TenantId).IsRequired();
        quota.Property(value => value.MaxMessagesPerMinute).IsRequired();
        quota.Property(value => value.MaxMessagesPerDay).IsRequired();
        quota.Property(value => value.SentInCurrentMinute).IsRequired();
        quota.Property(value => value.SentToday).IsRequired();
        quota.Property(value => value.CurrentMinuteWindowUtc).HasColumnType("datetimeoffset").IsRequired();
        quota.Property(value => value.CurrentDayWindowUtc).HasColumnType("datetimeoffset").IsRequired();
        quota.Property(value => value.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        quota.HasIndex(value => value.TenantId).IsUnique();

        var pref = modelBuilder.Entity<CustomerCommunicationPreference>();
        pref.ToTable("CustomerCommunicationPreferences", "whatsapp");
        pref.HasKey(value => value.Id);
        pref.Property(value => value.Id).ValueGeneratedNever();
        pref.Property(value => value.TenantId).IsRequired();
        pref.Property(value => value.NormalizedPhone).HasMaxLength(32).IsRequired();
        pref.Property(value => value.IsOptedIn).IsRequired();
        pref.Property(value => value.OptedOutAtUtc).HasColumnType("datetimeoffset");
        pref.Property(value => value.Reason).HasMaxLength(512);
        pref.Property(value => value.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        pref.HasIndex(value => new { value.TenantId, value.NormalizedPhone }).IsUnique();

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
