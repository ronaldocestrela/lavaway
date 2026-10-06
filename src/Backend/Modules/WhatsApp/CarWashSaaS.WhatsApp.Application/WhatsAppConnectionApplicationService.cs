using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class WhatsAppConnectionApplicationService(
    IWhatsAppConnectionRepository repository,
    IWhatsAppPairingProvider? pairingProvider = null)
{
    private readonly IWhatsAppPairingProvider _pairingProvider = pairingProvider ?? new StaticWhatsAppPairingProvider();

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

        connection.MarkDisconnected();
        await repository.UpdateAsync(connection, ct);

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

        if (string.Equals(providerState, "open", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerState, "connected", StringComparison.OrdinalIgnoreCase))
        {
            connection.MarkConnected();
        }
        else if (string.Equals(providerState, "close", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(providerState, "closed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(providerState, "disconnected", StringComparison.OrdinalIgnoreCase))
        {
            connection.MarkDisconnected();
        }
        else if (!string.Equals(providerState, "connecting", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WhatsAppConnection>.Failure(new Error("whatsapp.provider_state.invalid", "The provider connection state is not supported.", ErrorType.Validation));
        }

        await repository.UpdateAsync(connection, ct);
        return Result<WhatsAppConnection>.Success(connection);
    }
}
