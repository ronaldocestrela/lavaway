using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public sealed record WhatsAppProviderHealthState(
    bool IsReachable,
    string State,
    string? Details = null);

public interface IWhatsAppHealthCheckProvider
{
    Task<Result<WhatsAppProviderHealthState>> CheckHealthAsync(string providerSessionId, CancellationToken ct = default);
}
