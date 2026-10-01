using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class CustomerVehicleSearchRepository(YardOperationsDbContext dbContext) : ICustomerVehicleSearchRepository
{
    public async Task<CustomerVehicleMatchDto?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(value => value.TenantId == tenantId && value.Id == customerId)
            .Select(value => new CustomerSearchRow(value.Id, value.Name, value.Phone))
            .SingleOrDefaultAsync(ct);

        if (customer is null)
        {
            return null;
        }

        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.TenantId == tenantId && vehicle.CustomerId == customerId)
            .OrderBy(vehicle => vehicle.Plate)
            .Select(vehicle => new VehicleSearchRow(vehicle.Id, vehicle.CustomerId, vehicle.Plate, vehicle.Size))
            .ToListAsync(ct);

        return ToMatch(customer, vehicles);
    }

    public async Task<IReadOnlyCollection<CustomerVehicleMatchDto>> SearchAsync(
        Guid tenantId,
        string? normalizedPlate,
        string? normalizedPhone,
        int limit,
        CancellationToken ct = default)
    {
        var customersQuery = dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.TenantId == tenantId);

        if (normalizedPhone is not null)
        {
            customersQuery = customersQuery.Where(customer => customer.NormalizedPhone == normalizedPhone);
        }

        if (normalizedPlate is not null)
        {
            customersQuery = customersQuery.Where(customer => dbContext.Vehicles.Any(vehicle =>
                vehicle.TenantId == tenantId &&
                vehicle.CustomerId == customer.Id &&
                vehicle.Plate == normalizedPlate));
        }

        var customers = await customersQuery
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Take(limit)
            .Select(customer => new CustomerSearchRow(customer.Id, customer.Name, customer.Phone))
            .ToListAsync(ct);

        if (customers.Count == 0)
        {
            return [];
        }

        var customerIds = customers.Select(customer => customer.Id).ToArray();
        var vehiclesQuery = dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.TenantId == tenantId && customerIds.Contains(vehicle.CustomerId));

        if (normalizedPlate is not null)
        {
            vehiclesQuery = vehiclesQuery.Where(vehicle => vehicle.Plate == normalizedPlate);
        }

        var vehicles = await vehiclesQuery
            .OrderBy(vehicle => vehicle.Plate)
            .Select(vehicle => new VehicleSearchRow(vehicle.Id, vehicle.CustomerId, vehicle.Plate, vehicle.Size))
            .ToListAsync(ct);

        var vehiclesByCustomer = vehicles
            .GroupBy(vehicle => vehicle.CustomerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<VehicleSummaryDto>)group
                    .Select(vehicle => new VehicleSummaryDto(vehicle.Id, vehicle.Plate, vehicle.Size.ToString()))
                    .ToArray());

        return customers.Select(customer => ToMatch(
            customer,
            vehiclesByCustomer.GetValueOrDefault(customer.Id, Array.Empty<VehicleSummaryDto>()))).ToArray();
    }

    private static CustomerVehicleMatchDto ToMatch(CustomerSearchRow customer, IReadOnlyCollection<VehicleSummaryDto> vehicles) =>
        new(customer.Id, customer.Name, customer.Phone, vehicles);

    private static CustomerVehicleMatchDto ToMatch(CustomerSearchRow customer, IReadOnlyCollection<VehicleSearchRow> vehicles) =>
        new(customer.Id, customer.Name, customer.Phone, vehicles
            .Select(vehicle => new VehicleSummaryDto(vehicle.Id, vehicle.Plate, vehicle.Size.ToString()))
            .ToArray());

    private sealed record CustomerSearchRow(Guid Id, string Name, string Phone);
    private sealed record VehicleSearchRow(Guid Id, Guid CustomerId, string Plate, VehicleSize Size);
}