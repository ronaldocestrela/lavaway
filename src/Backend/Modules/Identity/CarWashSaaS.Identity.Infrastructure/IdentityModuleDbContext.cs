using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class IdentityModuleDbContext(
    DbContextOptions<IdentityModuleDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(value => value.TenantId).IsRequired();
            user.HasIndex(value => value.TenantId);
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
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}