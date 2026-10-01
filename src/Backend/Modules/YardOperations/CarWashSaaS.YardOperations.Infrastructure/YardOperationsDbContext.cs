using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsDbContext(DbContextOptions<YardOperationsDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YardOperationsDbContext).Assembly);
    }
}