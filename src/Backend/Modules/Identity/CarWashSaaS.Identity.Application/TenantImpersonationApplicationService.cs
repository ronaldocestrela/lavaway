using System.Text.Json;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Application;

public sealed class TenantImpersonationApplicationService(
    IGlobalTenantLookup tenantLookup,
    AuditTrailApplicationService auditTrailService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<Result<ImpersonationSessionDto>> StartImpersonationAsync(
        Guid tenantId,
        StartImpersonationRequest request,
        Guid actorId,
        string actorEmail,
        string actorRole,
        string actorRealm,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<ImpersonationSessionDto>.Failure(new Error(
                "impersonation.tenant_id.required",
                "Identificador de tenant inválido.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result<ImpersonationSessionDto>.Failure(new Error(
                "impersonation.reason.required",
                "Justificativa técnica para diagnóstico do tenant é obrigatória.",
                ErrorType.Validation));
        }

        // Verifica se o operador de plataforma tem a permissão de ImpersonateTenant
        if (Enum.TryParse<PlatformRole>(actorRole, out var role))
        {
            if (!PlatformRolePermissions.HasPermission(role, PlatformPermission.ImpersonateTenant))
            {
                await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                    ActorId: actorId,
                    ActorEmail: actorEmail,
                    ActorRole: actorRole,
                    ActorRealm: actorRealm,
                    Action: PlatformActionConstants.TenantImpersonated,
                    TargetType: PlatformTargetTypeConstants.Tenant,
                    TargetId: tenantId.ToString(),
                    TenantId: tenantId,
                    IpAddress: ipAddress,
                    UserAgent: userAgent,
                    DetailsJson: JsonSerializer.Serialize(new { Reason = request.Reason, TicketReference = request.TicketReference, DeniedReason = "Missing ImpersonateTenant permission" }, JsonOptions),
                    Outcome: "Failure",
                    ErrorMessage: "Permissão de impersonation negada para o operador."), ct);

                return Result<ImpersonationSessionDto>.Failure(new Error(
                    "impersonation.permission_denied",
                    "Operador não possui permissão para impersonate de tenants.",
                    ErrorType.Unauthorized));
            }
        }

        var tenantSummaryResult = await tenantLookup.GetSummaryAsync(tenantId, ct);
        if (!tenantSummaryResult.IsSuccess)
        {
            return Result<ImpersonationSessionDto>.Failure(tenantSummaryResult.Error ?? new Error(
                "impersonation.tenant_not_found",
                $"Tenant com ID '{tenantId}' não foi localizado no sistema.",
                ErrorType.NotFound));
        }

        var tenantSummary = tenantSummaryResult.Value!;

        var startedAt = DateTimeOffset.UtcNow;
        var details = new
        {
            tenantId,
            tenantName = tenantSummary.Name,
            tradeName = tenantSummary.TradeName,
            status = tenantSummary.Status.ToString(),
            ticketReference = request.TicketReference,
            reason = request.Reason.Trim(),
            startedAtUtc = startedAt
        };

        await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
            ActorId: actorId,
            ActorEmail: actorEmail,
            ActorRole: actorRole,
            ActorRealm: actorRealm,
            Action: PlatformActionConstants.TenantImpersonated,
            TargetType: PlatformTargetTypeConstants.Tenant,
            TargetId: tenantId.ToString(),
            TenantId: tenantId,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            DetailsJson: JsonSerializer.Serialize(details, JsonOptions),
            Outcome: "Success"), ct);

        var sessionDto = new ImpersonationSessionDto(
            TenantId: tenantId,
            TenantName: tenantSummary.Name,
            OperatorEmail: actorEmail,
            Reason: request.Reason.Trim(),
            TicketReference: request.TicketReference?.Trim(),
            StartedAtUtc: startedAt);

        return Result<ImpersonationSessionDto>.Success(sessionDto);
    }

    public async Task<Result> EndImpersonationAsync(
        Guid tenantId,
        EndImpersonationRequest request,
        Guid actorId,
        string actorEmail,
        string actorRole,
        string actorRealm,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error(
                "impersonation.tenant_id.required",
                "Identificador de tenant inválido.",
                ErrorType.Validation));
        }

        var endedAt = DateTimeOffset.UtcNow;
        var details = new
        {
            tenantId,
            endedAtUtc = endedAt,
            notes = request.Notes?.Trim()
        };

        await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
            ActorId: actorId,
            ActorEmail: actorEmail,
            ActorRole: actorRole,
            ActorRealm: actorRealm,
            Action: PlatformActionConstants.TenantImpersonationEnded,
            TargetType: PlatformTargetTypeConstants.Tenant,
            TargetId: tenantId.ToString(),
            TenantId: tenantId,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            DetailsJson: JsonSerializer.Serialize(details, JsonOptions),
            Outcome: "Success"), ct);

        return Result.Success();
    }
}
