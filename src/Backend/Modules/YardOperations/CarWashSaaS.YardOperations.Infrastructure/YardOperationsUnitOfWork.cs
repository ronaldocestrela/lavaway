using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsUnitOfWork(YardOperationsDbContext dbContext) : IUnitOfWork
{
    private const string VehiclePlateIndexName = "IX_Vehicles_TenantId_Plate";
    private const string BookingProtocolIndexName = "IX_Bookings_TenantId_Protocol";

    public async Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return Result<int>.Success(await dbContext.SaveChangesAsync(ct));
        }
        catch (DbUpdateException exception) when (IsDuplicatePlate(exception))
        {
            return Result<int>.Failure(new Error(
                "vehicle.plate.duplicate",
                "A vehicle with this plate already exists for this tenant.",
                ErrorType.Conflict));
        }
        catch (DbUpdateException exception) when (IsDuplicateBookingProtocol(exception))
        {
            return Result<int>.Failure(new Error(
                "booking.protocol.duplicate",
                "A booking with this protocol already exists for this tenant.",
                ErrorType.Conflict));
        }
    }

    private static bool IsDuplicatePlate(DbUpdateException exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException is { Number: 2601 or 2627 } &&
            sqlException.Message.Contains(VehiclePlateIndexName, StringComparison.Ordinal);
    }

    private static bool IsDuplicateBookingProtocol(DbUpdateException exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException is { Number: 2601 or 2627 } &&
            sqlException.Message.Contains(BookingProtocolIndexName, StringComparison.Ordinal);
    }
}
