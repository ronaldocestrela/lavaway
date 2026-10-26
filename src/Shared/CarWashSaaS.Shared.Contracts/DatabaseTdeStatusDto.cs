namespace CarWashSaaS.Shared.Contracts;

public sealed record DatabaseTdeStatusDto(
    string DatabaseName,
    int EncryptionState,
    string EncryptionStateDescription,
    string? KeyAlgorithm,
    int? KeyLength,
    string? CertificateName,
    decimal PercentComplete,
    bool IsTdeActive,
    DateTimeOffset CheckedAtUtc);

public interface ITdeStatusChecker
{
    Task<Result<DatabaseTdeStatusDto>> CheckTdeStatusAsync(CancellationToken ct = default);
}
