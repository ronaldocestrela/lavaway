using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppPairingProvider
{
    Task<Result<(string ProviderSessionId, string QrCodeValue)>> GeneratePairingAsync(Guid tenantId, CancellationToken ct = default);
}
