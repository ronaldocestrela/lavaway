namespace CarWashSaaS.Shared.Contracts;

public interface IUserAccountProvisioner
{
    Task<Result<Guid>> ProvisionUserAsync(
        Guid tenantId,
        string email,
        string password,
        string shopRole,
        CancellationToken ct = default);

    Task<Result> UpdateUserAccessAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string? newPassword,
        string? shopRole,
        bool isActive,
        CancellationToken ct = default);

    Task<Result> SetUserActiveStatusAsync(
        Guid tenantId,
        Guid userId,
        bool isActive,
        CancellationToken ct = default);
}
