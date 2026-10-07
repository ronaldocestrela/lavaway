namespace CarWashSaaS.Shared.Contracts;

public sealed record GlobalTenantSummaryDto(
    Guid Id,
    string Name,
    TenantStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset? TrialEndsAtUtc,
    string? StatusReason,
    string? TradeName,
    string? LegalName,
    string? Cnpj,
    string? Phone,
    string? City,
    string? State);

public sealed record GetGlobalTenantsRequest(
    string? SearchTerm = null,
    TenantStatus? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record UpdateTenantStatusRequest(
    TenantStatus NewStatus,
    string? Reason = null,
    DateTimeOffset? TrialEndsAtUtc = null);

public sealed record StartImpersonationRequest(
    string Reason,
    string? TicketReference = null);

public sealed record ImpersonationSessionDto(
    Guid TenantId,
    string TenantName,
    string OperatorEmail,
    string Reason,
    string? TicketReference,
    DateTimeOffset StartedAtUtc);

public sealed record EndImpersonationRequest(
    string? Notes = null);
