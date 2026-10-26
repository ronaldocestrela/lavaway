using CarWashSaaS.Shared.Contracts;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace CarWashSaaS.Api.Logging;

public sealed class PiiDataMaskingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Secret",
        "SecretKey",
        "ApiKey",
        "AccessToken",
        "WebhookSecret",
        "Token",
        "Authorization"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (property.Value is ScalarValue scalar && scalar.Value is string stringValue)
            {
                // Se a chave for declaradamente um segredo/senha, mascara por completo
                if (SensitivePropertyNames.Contains(property.Key))
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(PiiRegexMasker.MaskSecretKey(stringValue))));
                    continue;
                }

                // Se contiver PII no texto (CPF, Telefone, Email, API Key), sanitiza
                var masked = PiiRegexMasker.MaskAllPiiInText(stringValue);
                if (!string.Equals(masked, stringValue, StringComparison.Ordinal))
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(masked)));
                }
            }
        }
    }
}

public static class PiiDataMaskingEnricherExtensions
{
    public static Serilog.LoggerConfiguration WithPiiMasking(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        return enrichmentConfiguration.With<PiiDataMaskingEnricher>();
    }
}
