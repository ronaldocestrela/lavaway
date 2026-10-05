using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsDbContext(
    DbContextOptions<YardOperationsDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor) : DbContext(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<YardCapacity> YardCapacities => Set<YardCapacity>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ReactivationCampaignRule> ReactivationCampaignRules => Set<ReactivationCampaignRule>();
    public DbSet<ReactivationCampaignLog> ReactivationCampaignLogs => Set<ReactivationCampaignLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YardOperationsDbContext).Assembly);
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
