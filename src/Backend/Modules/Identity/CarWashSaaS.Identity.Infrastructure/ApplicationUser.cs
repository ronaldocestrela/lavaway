using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Identity;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>, IMustHaveTenant
{
    public Guid TenantId { get; set; }
}