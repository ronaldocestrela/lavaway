namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppPairingProvider
{
    Task<(string ProviderSessionId, string QrCodeValue)> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default);
}
