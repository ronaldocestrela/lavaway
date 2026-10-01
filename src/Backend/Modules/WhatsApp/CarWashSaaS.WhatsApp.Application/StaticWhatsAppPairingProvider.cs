namespace CarWashSaaS.WhatsApp.Application;

public sealed class StaticWhatsAppPairingProvider : IWhatsAppPairingProvider
{
    public Task<(string ProviderSessionId, string QrCodeValue)> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(($"session-{Guid.CreateVersion7()}", $"qr-{Guid.CreateVersion7()}"));
    }
}
