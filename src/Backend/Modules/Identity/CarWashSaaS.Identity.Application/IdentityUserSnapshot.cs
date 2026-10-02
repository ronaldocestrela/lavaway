using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.Identity.Application;

public sealed record IdentityUserSnapshot(
    Guid Id,
    Guid TenantId,
    string Email,
    ShopRole Role);
