namespace CarWashSaaS.Shared.Contracts;

public sealed record LoginRequest(string Email, string Password, Guid? TenantId = null);
