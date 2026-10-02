using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardOperationsUnitOfWork(YardOperationsDbContext dbContext) : IUnitOfWork
{
    private const string VehiclePlateIndexName = "IX_Vehicles_TenantId_Plate";

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
    }

    private static bool IsDuplicatePlate(DbUpdateException exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException is { Number: 2601 or 2627 } &&
            sqlException.Message.Contains(VehiclePlateIndexName, StringComparison.Ordinal);
    }
}
