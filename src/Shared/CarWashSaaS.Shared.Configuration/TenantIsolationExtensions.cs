using System.Linq.Expressions;
using System.Reflection;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CarWashSaaS.Shared.Configuration;

public static class TenantIsolationExtensions
{
    public static void ApplyTenantQueryFilters(this ModelBuilder modelBuilder, DbContext dbContext)
    {
        var currentTenantId = dbContext.GetType().GetProperty(nameof(CurrentTenantAccessor.TenantId), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("A tenant-aware DbContext must expose its current TenantId.");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(entityType => !entityType.IsOwned() && typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType)))
        {
            var entity = Expression.Parameter(entityType.ClrType, "entity");
            var tenantId = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(Guid)],
                entity,
                Expression.Constant(nameof(IMustHaveTenant.TenantId)));
            var currentTenant = Expression.Property(Expression.Constant(dbContext), currentTenantId);
            var hasTenant = Expression.Property(currentTenant, nameof(Nullable<Guid>.HasValue));
            var matchesTenant = Expression.Equal(Expression.Convert(tenantId, typeof(Guid?)), currentTenant);
            var filter = Expression.Lambda(Expression.AndAlso(hasTenant, matchesTenant), entity);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    public static void ValidateTenantWrites(this ChangeTracker changeTracker, ICurrentTenantAccessor currentTenantAccessor)
    {
        var tenantEntries = changeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is IMustHaveTenant)
            .ToArray();

        if (tenantEntries.Length == 0)
        {
            return;
        }

        if (currentTenantAccessor.TenantId is not Guid currentTenantId)
        {
            throw new InvalidOperationException("A tenant must be resolved before saving tenant-owned data.");
        }

        foreach (var entry in tenantEntries)
        {
            var tenantEntity = (IMustHaveTenant)entry.Entity;
            if (entry.State == EntityState.Added && tenantEntity.TenantId == Guid.Empty)
            {
                tenantEntity.TenantId = currentTenantId;
            }
            else if (tenantEntity.TenantId != currentTenantId)
            {
                throw new InvalidOperationException("Tenant-owned data cannot be written for a different tenant.");
            }
        }
    }
}