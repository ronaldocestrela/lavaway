using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class PlatformWhatsAppHealthApplicationService(
    IWhatsAppConnectionRepository repository,
    WhatsAppConnectionApplicationService connectionService,
    IWhatsAppConnectionIncidentRepository? incidentRepository = null,
    IGlobalTenantLookup? tenantLookup = null)
{
    public async Task<Result<PlatformWhatsAppInstancesOverviewDto>> GetOverviewAsync(
        GetPlatformWhatsAppInstancesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var all = await repository.ListAllConnectionsAsync(ct);

        var total = all.Count;
        var connected = all.Count(c => c.Status == WhatsAppConnectionStatus.Connected);
        var disconnected = all.Count(c => c.Status == WhatsAppConnectionStatus.Disconnected);
        var connecting = all.Count(c => c.Status == WhatsAppConnectionStatus.Connecting);
        var alertsActive = all.Count(c => c.HasActiveAlert);

        var summary = new WhatsAppInstanceHealthSummaryDto(
            TotalInstances: total,
            ConnectedCount: connected,
            DisconnectedCount: disconnected,
            ConnectingCount: connecting,
            AlertsActiveCount: alertsActive);

        var filtered = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            if (normalizedStatus == "connected")
            {
                filtered = filtered.Where(c => c.Status == WhatsAppConnectionStatus.Connected);
            }
            else if (normalizedStatus == "disconnected")
            {
                filtered = filtered.Where(c => c.Status == WhatsAppConnectionStatus.Disconnected);
            }
            else if (normalizedStatus == "connecting")
            {
                filtered = filtered.Where(c => c.Status == WhatsAppConnectionStatus.Connecting);
            }
            else if (normalizedStatus == "alert")
            {
                filtered = filtered.Where(c => c.HasActiveAlert);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(c =>
                c.ProviderSessionId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.TenantId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        var totalFiltered = filteredList.Count;

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);
        var pagedConnections = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = new List<PlatformWhatsAppInstanceItemDto>(pagedConnections.Count);
        foreach (var c in pagedConnections)
        {
            var tenantName = $"Estabelecimento {c.TenantId.ToString()[..8]}";
            if (tenantLookup is not null)
            {
                var summaryResult = await tenantLookup.GetSummaryAsync(c.TenantId, ct);
                if (summaryResult.IsSuccess)
                {
                    tenantName = summaryResult.Value!.TradeName ?? summaryResult.Value.Name;
                }
            }

            items.Add(new PlatformWhatsAppInstanceItemDto(
                TenantId: c.TenantId,
                TenantName: tenantName,
                ProviderSessionId: c.ProviderSessionId,
                Status: c.Status.ToString().ToLowerInvariant(),
                LastConnectedAtUtc: c.LastConnectedAtUtc,
                LastDisconnectedAtUtc: c.LastDisconnectedAtUtc,
                DisconnectReason: c.DisconnectReason,
                HasActiveAlert: c.HasActiveAlert,
                AlertCount: c.AlertCount,
                LastAlertSentAtUtc: c.LastAlertSentAtUtc,
                UpdatedAtUtc: c.UpdatedAt));
        }

        var pagedResult = new PagedResult<PlatformWhatsAppInstanceItemDto>(
            items,
            totalFiltered,
            page,
            pageSize);

        return Result<PlatformWhatsAppInstancesOverviewDto>.Success(
            new PlatformWhatsAppInstancesOverviewDto(summary, pagedResult));
    }

    public async Task<Result<ProbeWhatsAppInstanceResultDto>> RunProbeAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await connectionService.CheckTenantHealthAsync(tenantId, ct);
    }

    public async Task<Result> TriggerManualAlertAsync(
        Guid tenantId,
        TriggerWhatsAppAlertRequest request,
        CancellationToken ct = default)
    {
        return await connectionService.SendManualReconnectAlertAsync(tenantId, request.CustomNote, ct);
    }

    public async Task<Result<IReadOnlyList<WhatsAppConnectionIncidentDto>>> ListRecentIncidentsAsync(
        int count = 50,
        CancellationToken ct = default)
    {
        if (incidentRepository is null)
        {
            return Result<IReadOnlyList<WhatsAppConnectionIncidentDto>>.Success(Array.Empty<WhatsAppConnectionIncidentDto>());
        }

        var incidents = await incidentRepository.ListRecentGlobalAsync(count, ct);
        var dtos = incidents.Select(i => new WhatsAppConnectionIncidentDto(
            i.Id,
            i.TenantId,
            i.ProviderSessionId,
            i.Type.ToString(),
            i.Reason,
            i.AlertDispatched,
            i.RecipientEmail,
            i.OccurredAtUtc)).ToList();

        return Result<IReadOnlyList<WhatsAppConnectionIncidentDto>>.Success(dtos);
    }
}
