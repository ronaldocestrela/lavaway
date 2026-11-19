namespace CarWashSaaS.Shared.Contracts;

public interface IUserAccountProvisioner
{
    Task<Result<Guid>> ProvisionUserAsync(
        Guid tenantId,
        string email,
        string password,
        string shopRole,
        CancellationToken ct = default);

    Task<Result<Guid>> ProvisionUserAsync(
        Guid tenantId,
        string email,
        string password,
        string shopRole,
        Guid? customRoleId,
        CancellationToken ct = default)
        => ProvisionUserAsync(tenantId, email, password, shopRole, ct);

    Task<Result> UpdateUserAccessAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string? newPassword,
        string? shopRole,
        bool isActive,
        CancellationToken ct = default);

    Task<Result> UpdateUserAccessAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string? newPassword,
        string? shopRole,
        bool isActive,
        Guid? customRoleId,
        CancellationToken ct = default)
        => UpdateUserAccessAsync(tenantId, userId, email, newPassword, shopRole, isActive, ct);

    Task<Result> SetUserActiveStatusAsync(
        Guid tenantId,
        Guid userId,
        bool isActive,
        CancellationToken ct = default);
}
