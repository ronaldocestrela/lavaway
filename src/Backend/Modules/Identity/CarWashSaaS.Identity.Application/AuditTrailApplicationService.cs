using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class AuditTrailApplicationService(
    IAdministrativeAuditEventRepository auditEventRepository)
{
    public async Task<Result<Guid>> RecordEventAsync(
        RecordAuditEventRequest request,
        CancellationToken ct = default)
    {
        var domainEventResult = AdministrativeAuditEvent.Create(
            request.ActorId,
            request.ActorEmail,
            request.ActorRole,
            request.ActorRealm,
            request.Action,
            request.TargetType,
            request.TargetId,
            request.TenantId,
            request.IpAddress,
            request.UserAgent,
            request.DetailsJson,
            request.Outcome,
            request.ErrorMessage);

        if (!domainEventResult.IsSuccess)
        {
            return Result<Guid>.Failure(domainEventResult.Error!);
        }

        var domainEvent = domainEventResult.Value!;
        await auditEventRepository.AddAsync(domainEvent, ct);

        return Result<Guid>.Success(domainEvent.Id);
    }

    public async Task<Result<PagedResult<AuditEventDto>>> SearchAuditEventsAsync(
        AuditQueryFilter filter,
        CancellationToken ct = default)
    {
        var safeFilter = filter with
        {
            Page = filter.Page < 1 ? 1 : filter.Page,
            PageSize = filter.PageSize < 1 ? 20 : (filter.PageSize > 100 ? 100 : filter.PageSize)
        };

        var pagedEntities = await auditEventRepository.SearchAsync(safeFilter, ct);

        var dtos = pagedEntities.Items.Select(MapToDto).ToArray();

        var result = new PagedResult<AuditEventDto>(
            dtos,
            pagedEntities.TotalCount,
            pagedEntities.Page,
            pagedEntities.PageSize);

        return Result<PagedResult<AuditEventDto>>.Success(result);
    }

    public async Task<Result<AuditEventDto>> GetAuditEventByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result<AuditEventDto>.Failure(new Error(
                "audit.id.required",
                "Audit event ID cannot be empty.",
                ErrorType.Validation));
        }

        var entity = await auditEventRepository.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return Result<AuditEventDto>.Failure(new Error(
                "audit.event.not_found",
                $"Audit event with ID '{id}' was not found.",
                ErrorType.NotFound));
        }

        return Result<AuditEventDto>.Success(MapToDto(entity));
    }

    private static AuditEventDto MapToDto(AdministrativeAuditEvent entity)
    {
        return new AuditEventDto(
            entity.Id,
            entity.TimestampUtc,
            entity.ActorId,
            entity.ActorEmail,
            entity.ActorRole,
            entity.ActorRealm,
            entity.Action,
            entity.TargetType,
            entity.TargetId,
            entity.TenantId,
            entity.IpAddress,
            entity.UserAgent,
            entity.DetailsJson,
            entity.Outcome,
            entity.ErrorMessage);
    }
}
