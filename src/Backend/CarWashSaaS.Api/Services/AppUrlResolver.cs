using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.Api.Services;

public sealed class AppUrlResolver(
    IConfiguration configuration,
    IHttpContextAccessor? httpContextAccessor = null) : IAppUrlResolver
{
    public const string DefaultAppUrl = "https://app.lavaway.com.br";

    public string GetBaseUrl()
    {
        // 1. Contexto HTTP da requisição ativa (ex.: Origin do SPA Blazor, Referer ou Host da chamada)
        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext is not null)
        {
            var origin = httpContext.Request.Headers.Origin.ToString();
            if (!string.IsNullOrWhiteSpace(origin) &&
                Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
                (originUri.Scheme == Uri.UriSchemeHttp || originUri.Scheme == Uri.UriSchemeHttps))
            {
                return origin.TrimEnd('/');
            }

            var referer = httpContext.Request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer) &&
                Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                (refererUri.Scheme == Uri.UriSchemeHttp || refererUri.Scheme == Uri.UriSchemeHttps))
            {
                var portPart = refererUri.IsDefaultPort ? string.Empty : $":{refererUri.Port}";
                return $"{refererUri.Scheme}://{refererUri.Host}{portPart}";
            }

            if (httpContext.Request.Host.HasValue)
            {
                var scheme = httpContext.Request.Scheme;
                if (string.IsNullOrWhiteSpace(scheme))
                {
                    scheme = "https";
                }

                return $"{scheme}://{httpContext.Request.Host.Value}".TrimEnd('/');
            }
        }

        // 2. Configurações explícitas (App:BaseUrl, APP_URL, AppUrl, Cors:AllowedOrigins)
        var configured = configuration["App:BaseUrl"]
            ?? configuration["APP_URL"]
            ?? configuration["AppUrl"];

        if (!string.IsNullOrWhiteSpace(configured) &&
            Uri.TryCreate(configured, UriKind.Absolute, out var parsedConfig) &&
            (parsedConfig.Scheme == Uri.UriSchemeHttp || parsedConfig.Scheme == Uri.UriSchemeHttps))
        {
            return configured.TrimEnd('/');
        }

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (corsOrigins is { Length: > 0 })
        {
            var firstValid = corsOrigins.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o) && o != "*");
            if (!string.IsNullOrWhiteSpace(firstValid) &&
                Uri.TryCreate(firstValid, UriKind.Absolute, out var corsUri) &&
                (corsUri.Scheme == Uri.UriSchemeHttp || corsUri.Scheme == Uri.UriSchemeHttps))
            {
                return firstValid.TrimEnd('/');
            }
        }

        // 3. Fallback padrão seguro
        return DefaultAppUrl;
    }

    public string GetDisplayDomain()
    {
        var baseUrl = GetBaseUrl();
        return ExtractDisplayDomain(baseUrl);
    }

    public static string ExtractDisplayDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "app.lavaway.com.br";
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        var trimmed = url.Trim().TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[8..];
        }
        else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "app.lavaway.com.br" : trimmed.TrimEnd('/');
    }
}

