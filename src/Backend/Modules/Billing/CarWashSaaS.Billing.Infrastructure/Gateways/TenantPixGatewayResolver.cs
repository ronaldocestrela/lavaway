using CarWashSaaS.Billing.Application;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Infrastructure.Gateways;

public sealed class TenantPixGatewayResolver(
    ITenantPaymentGatewayConfigRepository configRepository,
    IPaymentCredentialsEncryptor encryptor,
    HttpClient httpClient,
    IConfiguration configuration,
    ILoggerFactory loggerFactory) : ITenantPixGatewayResolver
{
    public async Task<IPixGatewayProvider> ResolveForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId != Guid.Empty)
        {
            var config = await configRepository.GetByTenantIdAsync(tenantId, ct);
            if (config is not null && config.IsActive)
            {
                if (string.Equals(config.Provider, PaymentGatewayProviderConstants.PagarMe, StringComparison.OrdinalIgnoreCase))
                {
                    var secretKey = encryptor.Decrypt(config.PagarMeSecretKeyEncrypted ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(secretKey))
                    {
                        var logger = loggerFactory.CreateLogger<PagarMePixGatewayProvider>();
                        return new PagarMePixGatewayProvider(httpClient, secretKey, config.PagarMePublicKey, logger);
                    }
                }
                else if (string.Equals(config.Provider, PaymentGatewayProviderConstants.MercadoPago, StringComparison.OrdinalIgnoreCase))
                {
                    var mpLogger = loggerFactory.CreateLogger<MercadoPagoPixGatewayProvider>();
                    return new MercadoPagoPixGatewayProvider(httpClient, configuration, mpLogger);
                }
                else if (string.Equals(config.Provider, PaymentGatewayProviderConstants.Simulated, StringComparison.OrdinalIgnoreCase))
                {
                    return new SimulatedPixGatewayProvider();
                }
            }
        }

        // Fallback: se houver Mercado Pago configurado globalmente em appsettings, usa ele; senão simulado
        var globalMpToken = configuration["Billing:MercadoPago:AccessToken"];
        if (!string.IsNullOrWhiteSpace(globalMpToken))
        {
            return new MercadoPagoPixGatewayProvider(httpClient, configuration, loggerFactory.CreateLogger<MercadoPagoPixGatewayProvider>());
        }

        return new SimulatedPixGatewayProvider();
    }
}
