using System.Text.Json;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class IdentityModuleDbContext(
    DbContextOptions<IdentityModuleDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<AdministrativeAuditEvent> AdministrativeAuditEvents => Set<AdministrativeAuditEvent>();
    public DbSet<CustomRole> CustomRoles => Set<CustomRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(value => value.TenantId).IsRequired();
            user.Property(value => value.CustomRoleId);
            user.HasIndex(value => value.TenantId);
            user.HasIndex(value => value.CustomRoleId);
        });

        builder.Entity<CustomRole>(role =>
        {
            role.ToTable("CustomRoles");
            role.HasKey(r => r.Id);
            role.Property(r => r.TenantId).IsRequired();
            role.Property(r => r.Name).HasMaxLength(100).IsRequired();
            role.Property(r => r.Description).HasMaxLength(500);
            role.Property(r => r.IsSystemRole).IsRequired();
            role.Property(r => r.CreatedAtUtc).IsRequired();
            role.Property(r => r.UpdatedAtUtc);

            var permissionsConverter = new ValueConverter<IReadOnlyCollection<ShopPermission>, string>(
                perms => JsonSerializer.Serialize(perms.Select(p => p.ToString()), (JsonSerializerOptions?)null),
                json => string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<ShopPermission>()
                    : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null)!
                        .Select(Enum.Parse<ShopPermission>)
                        .ToList());

            role.Property(r => r.Permissions)
                .HasConversion(permissionsConverter)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            role.HasIndex(r => r.TenantId);
            role.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(value => value.Id);
            token.Property(value => value.TenantId).IsRequired();
            token.Property(value => value.UserId).IsRequired();
            token.Property(value => value.TokenHash).HasMaxLength(256).IsRequired();
            token.Property(value => value.ReplacedByTokenHash).HasMaxLength(256);
            token.Property(value => value.ExpiresAtUtc).IsRequired();
            token.Property(value => value.CreatedAtUtc).IsRequired();
            token.HasIndex(value => value.TenantId);
            token.HasIndex(value => value.UserId);
            token.HasIndex(value => value.TokenHash).IsUnique();
        });

        builder.Entity<PlatformUser>(user =>
        {
            user.ToTable("PlatformUsers");
            user.HasKey(value => value.Id);
            user.Property(value => value.Email).HasMaxLength(256).IsRequired();
            user.Property(value => value.FullName).HasMaxLength(200).IsRequired();
            user.Property(value => value.Role).HasConversion<string>().HasMaxLength(64).IsRequired();
            user.Property(value => value.PasswordHash).IsRequired();
            user.Property(value => value.IsActive).IsRequired();
            user.Property(value => value.CreatedAtUtc).IsRequired();
            user.HasIndex(value => value.Email).IsUnique();
        });

        builder.Entity<AdministrativeAuditEvent>(audit =>
        {
            audit.ToTable("AdministrativeAuditEvents");
            audit.HasKey(value => value.Id);
            audit.Property(value => value.TimestampUtc).IsRequired();
            audit.Property(value => value.ActorId).IsRequired();
            audit.Property(value => value.ActorEmail).HasMaxLength(256).IsRequired();
            audit.Property(value => value.ActorRole).HasMaxLength(64).IsRequired();
            audit.Property(value => value.ActorRealm).HasMaxLength(32).IsRequired();
            audit.Property(value => value.Action).HasMaxLength(128).IsRequired();
            audit.Property(value => value.TargetType).HasMaxLength(128).IsRequired();
            audit.Property(value => value.TargetId).HasMaxLength(128).IsRequired();
            audit.Property(value => value.TenantId);
            audit.Property(value => value.IpAddress).HasMaxLength(64);
            audit.Property(value => value.UserAgent).HasMaxLength(512);
            audit.Property(value => value.DetailsJson).IsRequired();
            audit.Property(value => value.Outcome).HasMaxLength(32).IsRequired();
            audit.Property(value => value.ErrorMessage).HasMaxLength(1024);

            audit.HasIndex(value => value.TimestampUtc);
            audit.HasIndex(value => value.ActorId);
            audit.HasIndex(value => value.Action);
            audit.HasIndex(value => new { value.TargetType, value.TargetId });
            audit.HasIndex(value => value.TenantId);
        });

        builder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("b4f6a998-8bf9-4f27-9ed0-a3e82c62b501"),
                Name = ShopRole.Administrator.ToString(),
                NormalizedName = ShopRole.Administrator.ToString().ToUpperInvariant(),
                ConcurrencyStamp = "b4f6a998-8bf9-4f27-9ed0-a3e82c62b501"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("b4f6a998-8bf9-4f27-9ed0-a3e82c62b502"),
                Name = ShopRole.Receptionist.ToString(),
                NormalizedName = ShopRole.Receptionist.ToString().ToUpperInvariant(),
                ConcurrencyStamp = "b4f6a998-8bf9-4f27-9ed0-a3e82c62b502"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("b4f6a998-8bf9-4f27-9ed0-a3e82c62b503"),
                Name = ShopRole.Operator.ToString(),
                NormalizedName = ShopRole.Operator.ToString().ToUpperInvariant(),
                ConcurrencyStamp = "b4f6a998-8bf9-4f27-9ed0-a3e82c62b503"
            });

        builder.ApplyTenantQueryFilters(this);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreventAuditEventMutations();
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreventAuditEventMutations();
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreventAuditEventMutations()
    {
        var auditMutations = ChangeTracker.Entries<AdministrativeAuditEvent>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (auditMutations.Length > 0)
        {
            throw new InvalidOperationException("Administrative audit events are append-only and cannot be modified or deleted.");
        }
    }
}

