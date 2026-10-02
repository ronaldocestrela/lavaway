using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public interface IUnitOfWork
{
    Task<Result<int>> SaveChangesAsync(CancellationToken ct = default);
}
