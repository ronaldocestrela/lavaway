using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class WhatsAppConnectionApplicationService(
    IWhatsAppConnectionRepository repository,
    IWhatsAppPairingProvider? pairingProvider = null,
    IWhatsAppHealthCheckProvider? healthCheckProvider = null,
    IWhatsAppHealthAlertSender? alertSender = null,
    IWhatsAppConnectionIncidentRepository? incidentRepository = null,
    ITenantNotificationContactLookup? contactLookup = null)
{
    private readonly IWhatsAppPairingProvider _pairingProvider = pairingProvider ?? new StaticWhatsAppPairingProvider();
    private readonly IWhatsAppHealthCheckProvider _healthCheckProvider = healthCheckProvider ??
        (pairingProvider as IWhatsAppHealthCheckProvider ?? new StaticWhatsAppPairingProvider());

    public async Task<Result<WhatsAppConnectionStatus>> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnectionStatus>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        var status = connection?.Status ?? WhatsAppConnectionStatus.Disconnected;

        return Result<WhatsAppConnectionStatus>.Success(status);
    }

    public async Task<Result<WhatsAppConnection?>> GetConnectionAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection?>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        return Result<WhatsAppConnection?>.Success(connection);
    }

    public async Task<Result<TenantWhatsAppHealthDetailDto>> GetHealthDetailsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantWhatsAppHealthDetailDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result<TenantWhatsAppHealthDetailDto>.Success(new TenantWhatsAppHealthDetailDto(
                Status: "disconnected",
                IsConnected: false,
                HasActiveAlert: false,
                DisconnectReason: "Nenhuma instância configurada.",
                LastConnectedAtUtc: null,
                LastDisconnectedAtUtc: null,
                LastAlertSentAtUtc: null,
                ReconnectInstructionsUrl: "/settings/whatsapp"));
        }

        var isConnected = connection.Status == WhatsAppConnectionStatus.Connected;
        var dto = new TenantWhatsAppHealthDetailDto(
            Status: connection.Status.ToString().ToLowerInvariant(),
            IsConnected: isConnected,
            HasActiveAlert: connection.HasActiveAlert,
            DisconnectReason: connection.DisconnectReason,
            LastConnectedAtUtc: connection.LastConnectedAtUtc,
            LastDisconnectedAtUtc: connection.LastDisconnectedAtUtc,
            LastAlertSentAtUtc: connection.LastAlertSentAtUtc,
            ReconnectInstructionsUrl: "/settings/whatsapp");

        return Result<TenantWhatsAppHealthDetailDto>.Success(dto);
    }

    public async Task<Result<WhatsAppConnection>> DisconnectAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.not_found", "No WhatsApp connection was found for this tenant.", ErrorType.NotFound));
        }

        var providerResult = await _pairingProvider.DisconnectAsync(connection.ProviderSessionId, ct);
        if (!providerResult.IsSuccess)
        {
            return Result<WhatsAppConnection>.Failure(providerResult.Error!);
        }

        connection.MarkDisconnected("Manual user disconnect");
        await repository.UpdateAsync(connection, ct);

        if (incidentRepository is not null)
        {
            var incident = WhatsAppConnectionIncident.Create(
                tenantId,
                connection.ProviderSessionId,
                WhatsAppIncidentType.Disconnected,
                "Manual user disconnect",
                alertDispatched: false,
                occurredAtUtc: DateTimeOffset.UtcNow);

            if (incident.IsSuccess)
            {
                await incidentRepository.AddAsync(incident.Value!, ct);
            }
        }

        return Result<WhatsAppConnection>.Success(connection);
    }

    public async Task<Result<WhatsAppConnection>> StartPairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var existingConnection = await repository.GetByTenantAsync(tenantId, ct);
        if (existingConnection is not null && existingConnection.Status == WhatsAppConnectionStatus.Connected)
        {
            return Result<WhatsAppConnection>.Success(existingConnection);
        }

        var pairingResult = await _pairingProvider.GeneratePairingAsync(tenantId, ct);
        if (!pairingResult.IsSuccess)
        {
            return Result<WhatsAppConnection>.Failure(pairingResult.Error!);
        }

        var pairing = pairingResult.Value;
        var connectionResult = WhatsAppConnection.Create(tenantId, pairing.ProviderSessionId, pairing.QrCodeValue);
        if (!connectionResult.IsSuccess)
        {
            return connectionResult;
        }

        if (existingConnection is not null)
        {
            var refreshed = existingConnection.RefreshSession(connectionResult.Value!.ProviderSessionId, connectionResult.Value.QrCodeValue);
            if (!refreshed.IsSuccess)
            {
                return refreshed;
            }

            await repository.UpdateAsync(existingConnection, ct);
            return Result<WhatsAppConnection>.Success(existingConnection);
        }

        await repository.AddAsync(connectionResult.Value!, ct);
        return connectionResult;
    }

    public async Task<Result<WhatsAppConnection>> RefreshPairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.not_found", "No WhatsApp pairing session was found for this tenant.", ErrorType.NotFound));
        }

        var pairingResult = await _pairingProvider.GeneratePairingAsync(tenantId, ct);
        if (!pairingResult.IsSuccess)
        {
            return Result<WhatsAppConnection>.Failure(pairingResult.Error!);
        }

        var pairing = pairingResult.Value;
        var refreshed = connection.RefreshSession(pairing.ProviderSessionId, pairing.QrCodeValue);
        if (!refreshed.IsSuccess)
        {
            return refreshed;
        }

        await repository.UpdateAsync(connection, ct);
        return Result<WhatsAppConnection>.Success(connection);
    }

    public async Task<Result<WhatsAppConnection>> ApplyProviderStatusAsync(
        Guid tenantId,
        string providerSessionId,
        string providerState,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_session.invalid", "A valid provider session is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.not_found", "No WhatsApp connection was found for this tenant.", ErrorType.NotFound));
        }

        if (!string.Equals(connection.ProviderSessionId, providerSessionId, StringComparison.Ordinal))
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_session.mismatch", "The provider event does not match the current WhatsApp session.", ErrorType.Conflict));
        }

        var previousStatus = connection.Status;

        if (string.Equals(providerState, "open", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerState, "connected", StringComparison.OrdinalIgnoreCase))
        {
            connection.MarkConnected();

            if (previousStatus != WhatsAppConnectionStatus.Connected && incidentRepository is not null)
            {
                var incident = WhatsAppConnectionIncident.Create(
                    tenantId,
                    providerSessionId,
                    WhatsAppIncidentType.Reconnected,
                    "Conexão restabelecida",
                    alertDispatched: false,
                    occurredAtUtc: DateTimeOffset.UtcNow);

                if (incident.IsSuccess)
                {
                    await incidentRepository.AddAsync(incident.Value!, ct);
                }
            }
        }
        else if (string.Equals(providerState, "close", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(providerState, "closed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(providerState, "disconnected", StringComparison.OrdinalIgnoreCase))
        {
            var disconnectReason = $"Provider reported state: {providerState}";
            connection.MarkDisconnected(disconnectReason);

            if (previousStatus == WhatsAppConnectionStatus.Connected)
            {
                var shouldSend = connection.ShouldSendAlert(TimeSpan.FromHours(1), DateTimeOffset.UtcNow);
                string? recipientEmail = null;
                var alertDispatched = false;

                if (shouldSend && alertSender is not null)
                {
                    var contactResult = contactLookup is not null
                        ? await contactLookup.GetContactAsync(tenantId, ct)
                        : Result<TenantNotificationContactDto>.Failure(new Error("contact.unavailable", "No contact lookup", ErrorType.NotFound));

                    recipientEmail = contactResult.IsSuccess ? contactResult.Value!.ContactEmail : $"admin-{tenantId:N}@lavaway.com";
                    var tenantName = contactResult.IsSuccess ? contactResult.Value!.TenantName : "Seu Lava-Jato";

                    var alertResult = await alertSender.SendDisconnectionAlertAsync(
                        recipientEmail,
                        tenantName,
                        "/settings/whatsapp",
                        disconnectReason,
                        ct);

                    if (alertResult.IsSuccess)
                    {
                        alertDispatched = true;
                        connection.RecordAlertDispatched();
                    }
                }

                if (incidentRepository is not null)
                {
                    var incident = WhatsAppConnectionIncident.Create(
                        tenantId,
                        providerSessionId,
                        WhatsAppIncidentType.Disconnected,
                        disconnectReason,
                        alertDispatched: alertDispatched,
                        recipientEmail: recipientEmail,
                        occurredAtUtc: DateTimeOffset.UtcNow);

                    if (incident.IsSuccess)
                    {
                        await incidentRepository.AddAsync(incident.Value!, ct);
                    }
                }
            }
        }
        else if (!string.Equals(providerState, "connecting", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_state.invalid", "The provider connection state is not supported.", ErrorType.Validation));
        }

        await repository.UpdateAsync(connection, ct);
        return Result<WhatsAppConnection>.Success(connection);
    }

    public async Task<Result<ProbeWhatsAppInstanceResultDto>> CheckTenantHealthAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<ProbeWhatsAppInstanceResultDto>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result<ProbeWhatsAppInstanceResultDto>.Failure(new Error("whatsapp.not_found", "No WhatsApp connection was found for this tenant.", ErrorType.NotFound));
        }

        var healthResult = await _healthCheckProvider.CheckHealthAsync(connection.ProviderSessionId, ct);
        if (!healthResult.IsSuccess)
        {
            return Result<ProbeWhatsAppInstanceResultDto>.Failure(healthResult.Error!);
        }

        var health = healthResult.Value!;
        var now = DateTimeOffset.UtcNow;

        if (health.IsReachable)
        {
            await ApplyProviderStatusAsync(tenantId, connection.ProviderSessionId, health.State, ct);
        }
        else
        {
            await ApplyProviderStatusAsync(tenantId, connection.ProviderSessionId, "disconnected", ct);
        }

        var message = health.IsReachable
            ? $"Instância respondendo com estado '{health.State}'."
            : $"Instância inacessível. Detalhes: {health.Details ?? "Sem resposta"}";

        return Result<ProbeWhatsAppInstanceResultDto>.Success(new ProbeWhatsAppInstanceResultDto(
            IsReachable: health.IsReachable,
            ProviderState: health.State,
            Message: message,
            CheckedAtUtc: now));
    }

    public async Task<Result> SendManualReconnectAlertAsync(Guid tenantId, string? customNote = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var connection = await repository.GetByTenantAsync(tenantId, ct);
        if (connection is null)
        {
            return Result.Failure(new Error("whatsapp.not_found", "No WhatsApp connection was found for this tenant.", ErrorType.NotFound));
        }

        var contactResult = contactLookup is not null
            ? await contactLookup.GetContactAsync(tenantId, ct)
            : Result<TenantNotificationContactDto>.Failure(new Error("contact.unavailable", "No contact lookup", ErrorType.NotFound));

        var recipientEmail = contactResult.IsSuccess ? contactResult.Value!.ContactEmail : $"admin-{tenantId:N}@lavaway.com";
        var tenantName = contactResult.IsSuccess ? contactResult.Value!.TenantName : "Seu Lava-Jato";

        var reason = !string.IsNullOrWhiteSpace(customNote)
            ? customNote.Trim()
            : (connection.DisconnectReason ?? "Alerta manual enviado pelo suporte da plataforma");

        if (alertSender is not null)
        {
            var alertResult = await alertSender.SendDisconnectionAlertAsync(
                recipientEmail,
                tenantName,
                "/settings/whatsapp",
                reason,
                ct);

            if (!alertResult.IsSuccess)
            {
                return alertResult;
            }
        }

        connection.RecordAlertDispatched();
        await repository.UpdateAsync(connection, ct);

        if (incidentRepository is not null)
        {
            var incident = WhatsAppConnectionIncident.Create(
                tenantId,
                connection.ProviderSessionId,
                WhatsAppIncidentType.ManualProbe,
                $"Alerta de reconexão disparado: {reason}",
                alertDispatched: true,
                recipientEmail: recipientEmail,
                occurredAtUtc: DateTimeOffset.UtcNow);

            if (incident.IsSuccess)
            {
                await incidentRepository.AddAsync(incident.Value!, ct);
            }
        }

        return Result.Success();
    }
}
