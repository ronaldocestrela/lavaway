using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public interface IAdministrativeAuditEventRepository
{
    Task AddAsync(AdministrativeAuditEvent auditEvent, CancellationToken ct = default);
    Task<AdministrativeAuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AdministrativeAuditEvent>> SearchAsync(AuditQueryFilter filter, CancellationToken ct = default);
}
