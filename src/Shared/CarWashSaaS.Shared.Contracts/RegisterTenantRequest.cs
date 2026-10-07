namespace CarWashSaaS.Shared.Contracts;

public sealed record RegisterTenantRequest(
    string StoreName,
    string AdminName,
    string Email,
    string Password,
    string Phone,
    string? City = null,
    string? State = null,
    SaasPlanTier PlanTier = SaasPlanTier.Pro);
