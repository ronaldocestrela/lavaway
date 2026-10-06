using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class StaticWhatsAppPairingProvider : IWhatsAppPairingProvider
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
}
