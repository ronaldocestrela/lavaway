using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class StaticWhatsAppPairingProvider : IWhatsAppPairingProvider, IWhatsAppHealthCheckProvider
{
    public Task<Result<(string ProviderSessionId, string QrCodeValue)>> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(Result<(string ProviderSessionId, string QrCodeValue)>.Success(
            ($"session-{Guid.CreateVersion7()}", $"qr-{Guid.CreateVersion7()}")));
    }

    public Task<Result> DisconnectAsync(string providerSessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Success());
    }

    public Task<Result<WhatsAppProviderHealthState>> CheckHealthAsync(string providerSessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Result<WhatsAppProviderHealthState>.Success(new WhatsAppProviderHealthState(
            IsReachable: true,
            State: "open",
            Details: "Static provider is online.")));
    }
}

