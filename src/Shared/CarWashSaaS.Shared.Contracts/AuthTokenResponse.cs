namespace CarWashSaaS.Shared.Contracts;

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    string TokenType,
    Guid UserId,
    string Email,
    Guid TenantId,
    string Role,
    IReadOnlyCollection<string> Permissions);
