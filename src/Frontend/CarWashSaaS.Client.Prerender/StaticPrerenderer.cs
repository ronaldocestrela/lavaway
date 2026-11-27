using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using CarWashSaaS.Client.Web.Layout;
using CarWashSaaS.Client.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace CarWashSaaS.Client.Prerender;

public sealed record PrerenderRoute(
    string Route,
    string OutputRelativePath,
    Type ComponentType,
    string Title,
    string Description,
    string CanonicalPath,
    bool NoIndex = false,
    IDictionary<string, object?>? Parameters = null);

public static partial class StaticPrerenderer
{
    public const string AppContentStartMarker = "<!--app-content-start-->";
    public const string AppContentEndMarker = "<!--app-content-end-->";
    public const string DefaultCanonicalOrigin = "https://lavaae.app";

    public static IReadOnlyList<PrerenderRoute> GetPublicRoutes() =>
    [
        new(
            Route: "/",
            OutputRelativePath: "prerendered/index.html",
            ComponentType: typeof(Home),
            Title: "Lava Aê | O Sistema Operacional para Lava-Jatos e Estética Automotiva",
            Description: "Software em nuvem para gestão de lava-jatos e estética automotiva: reconhecimento de placas LPR, avisos de WhatsApp ilimitados sem custo por disparo, checklist digital com fotos, controle de pátio e clube de assinaturas.",
            CanonicalPath: "/"),
        new(
            Route: "/privacidade",
            OutputRelativePath: "privacidade/index.html",
            ComponentType: typeof(PrivacyPolicyPage),
            Title: "Política de Privacidade e LGPD | Lava Aê",
            Description: "Saiba como o Lava Aê trata e protege dados pessoais em conformidade com a LGPD (Lei nº 13.709/2018), incluindo cookies, vistorias e direitos dos titulares.",
            CanonicalPath: "/privacidade"),
        new(
            Route: "/privacy",
            OutputRelativePath: "privacy/index.html",
            ComponentType: typeof(PrivacyPolicyPage),
            Title: "Política de Privacidade e LGPD | Lava Aê",
            Description: "Saiba como o Lava Aê trata e protege dados pessoais em conformidade com a LGPD (Lei nº 13.709/2018), incluindo cookies, vistorias e direitos dos titulares.",
            CanonicalPath: "/privacidade"),
        new(
            Route: "/termos",
            OutputRelativePath: "termos/index.html",
            ComponentType: typeof(TermsOfServicePage),
            Title: "Termos de Uso do Serviço | Lava Aê",
            Description: "Termos contratuais de licenciamento SaaS, responsabilidades, níveis de serviço e condições de uso da plataforma Lava Aê para lava-jatos e estética automotiva.",
            CanonicalPath: "/termos"),
        new(
            Route: "/terms",
            OutputRelativePath: "terms/index.html",
            ComponentType: typeof(TermsOfServicePage),
            Title: "Termos de Uso do Serviço | Lava Aê",
            Description: "Termos contratuais de licenciamento SaaS, responsabilidades, níveis de serviço e condições de uso da plataforma Lava Aê para lava-jatos e estética automotiva.",
            CanonicalPath: "/termos"),
        new(
            Route: "/dpo",
            OutputRelativePath: "dpo/index.html",
            ComponentType: typeof(DpoChannelPage),
            Title: "Canal do Encarregado de Proteção de Dados (DPO) | Lava Aê",
            Description: "Canal oficial de atendimento do Encarregado pelo Tratamento de Dados Pessoais (DPO) do Lava Aê nos termos do Art. 41 da LGPD.",
            CanonicalPath: "/dpo"),
        new(
            Route: "/canal-dpo",
            OutputRelativePath: "canal-dpo/index.html",
            ComponentType: typeof(DpoChannelPage),
            Title: "Canal do Encarregado de Proteção de Dados (DPO) | Lava Aê",
            Description: "Canal oficial de atendimento do Encarregado pelo Tratamento de Dados Pessoais (DPO) do Lava Aê nos termos do Art. 41 da LGPD.",
            CanonicalPath: "/dpo"),
        new(
            Route: "/authentication/login",
            OutputRelativePath: "authentication/login/index.html",
            ComponentType: typeof(Authentication),
            Title: "Acessar Conta | Lava Aê",
            Description: "Acesse o painel operacional do Lava Aê para gerenciar recepção, pátio, vistorias, WhatsApp e caixa do seu lava-jato.",
            CanonicalPath: "/authentication/login",
            Parameters: new Dictionary<string, object?> { ["Action"] = "login" }),
        new(
            Route: "/login",
            OutputRelativePath: "login/index.html",
            ComponentType: typeof(Authentication),
            Title: "Acessar Conta | Lava Aê",
            Description: "Acesse o painel operacional do Lava Aê para gerenciar recepção, pátio, vistorias, WhatsApp e caixa do seu lava-jato.",
            CanonicalPath: "/authentication/login",
            Parameters: new Dictionary<string, object?> { ["Action"] = "login" })
    ];

    public static async Task<int> PrerenderAllAsync(string wwwrootPath, string canonicalOrigin = DefaultCanonicalOrigin)
    {
        var templatePath = Path.Combine(wwwrootPath, "index.html");
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template index.html não encontrado em: {templatePath}", templatePath);
        }

        var templateHtml = await File.ReadAllTextAsync(templatePath, Encoding.UTF8);
        if (!templateHtml.Contains(AppContentStartMarker, StringComparison.Ordinal) ||
            !templateHtml.Contains(AppContentEndMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"index.html precisa conter os marcadores '{AppContentStartMarker}' e '{AppContentEndMarker}'.");
        }

        var normalizedOrigin = canonicalOrigin.TrimEnd('/');
        var routes = GetPublicRoutes();

        foreach (var route in routes)
        {
            var renderedContent = await RenderRouteHtmlAsync(route, normalizedOrigin);
            var fullDocument = ApplyRouteToTemplate(templateHtml, route, renderedContent, normalizedOrigin);

            var outputPath = Path.Combine(wwwrootPath, route.OutputRelativePath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await File.WriteAllTextAsync(outputPath, fullDocument, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"[Prerender] {route.Route} -> {route.OutputRelativePath} ({fullDocument.Length} bytes)");
        }

        return routes.Count;
    }

    public static async Task<string> RenderRouteHtmlAsync(PrerenderRoute route, string canonicalOrigin = DefaultCanonicalOrigin)
    {
        var services = BuildServiceProvider(canonicalOrigin, route.Route);
        await using var serviceProvider = services.BuildServiceProvider();
        await using var htmlRenderer = new HtmlRenderer(serviceProvider, NullLoggerFactory.Instance);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(PrerenderRoot.ComponentType)] = route.ComponentType,
                [nameof(PrerenderRoot.LayoutType)] = typeof(LandingLayout),
                [nameof(PrerenderRoot.Parameters)] = route.Parameters
            });

            var output = await htmlRenderer.RenderComponentAsync<PrerenderRoot>(parameters);
            return output.ToHtmlString();
        });
    }

    public static string ApplyRouteToTemplate(
        string templateHtml,
        PrerenderRoute route,
        string renderedAppContent,
        string canonicalOrigin = DefaultCanonicalOrigin)
    {
        var startIdx = templateHtml.IndexOf(AppContentStartMarker, StringComparison.Ordinal);
        var endIdx = templateHtml.IndexOf(AppContentEndMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
        {
            throw new InvalidOperationException("Marcadores de conteúdo do #app inválidos no template.");
        }

        var before = templateHtml[..startIdx];
        var after = templateHtml[(endIdx + AppContentEndMarker.Length)..];
        var html = string.Concat(before, renderedAppContent, after);

        var encodedTitle = EscapeHtml(route.Title);
        var encodedDescription = EscapeHtml(route.Description);
        var canonicalUrl = $"{canonicalOrigin.TrimEnd('/')}{route.CanonicalPath}";
        var encodedCanonicalUrl = EscapeHtml(canonicalUrl);

        html = TitleRegex().Replace(html, $"<title>{encodedTitle}</title>", 1);
        html = MetaNameTitleRegex().Replace(html, $"<meta name=\"title\" content=\"{encodedTitle}\" />", 1);
        html = MetaDescriptionRegex().Replace(html, $"<meta name=\"description\" content=\"{encodedDescription}\" />", 1);
        html = CanonicalRegex().Replace(html, $"<link rel=\"canonical\" href=\"{encodedCanonicalUrl}\" />", 1);
        html = OgUrlRegex().Replace(html, $"<meta property=\"og:url\" content=\"{encodedCanonicalUrl}\" />", 1);
        html = OgTitleRegex().Replace(html, $"<meta property=\"og:title\" content=\"{encodedTitle}\" />", 1);
        html = OgDescriptionRegex().Replace(html, $"<meta property=\"og:description\" content=\"{encodedDescription}\" />", 1);
        html = TwitterUrlRegex().Replace(html, $"<meta name=\"twitter:url\" content=\"{encodedCanonicalUrl}\" />", 1);
        html = TwitterTitleRegex().Replace(html, $"<meta name=\"twitter:title\" content=\"{encodedTitle}\" />", 1);
        html = TwitterDescriptionRegex().Replace(html, $"<meta name=\"twitter:description\" content=\"{encodedDescription}\" />", 1);

        if (route.NoIndex)
        {
            html = MetaRobotsRegex().Replace(html, "<meta name=\"robots\" content=\"noindex, nofollow\" />", 1);
        }

        return html;
    }

    private static string EscapeHtml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static IServiceCollection BuildServiceProvider(string canonicalOrigin, string currentRoute)
    {
        var services = new ServiceCollection();
        var baseUri = new Uri($"{canonicalOrigin.TrimEnd('/')}/");
        var currentUri = new Uri(baseUri, currentRoute.TrimStart('/')).ToString();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "/api/",
                ["Authentication:Authority"] = "",
                ["Authentication:UseOidc"] = "false"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IWebAssemblyHostEnvironment>(new PrerenderHostEnvironment(baseUri.ToString()));
        services.AddSingleton<NavigationManager>(new PrerenderNavigationManager(baseUri.ToString(), currentUri));
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddLogging();

        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<ITokenStorage, InMemoryTokenStorage>();
        services.AddScoped<ICookieConsentService, InMemoryCookieConsentService>();
        services.AddScoped<JwtAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider, UnauthenticatedAuthenticationStateProvider>();
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<ImpersonationSessionState>();
        services.AddScoped<TenantPlanStateService>();
        services.AddScoped<ReceptionSessionState>();

        services.AddScoped(_ => new HttpClient(new OfflineStubHttpMessageHandler()) { BaseAddress = baseUri });
        services.AddScoped<AuthApiClient>();
        services.AddScoped<SaasBillingApiClient>();

        return services;
    }

    [GeneratedRegex("<title>.*?</title>", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<meta\\s+name=\"title\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex MetaNameTitleRegex();

    [GeneratedRegex("<meta\\s+name=\"description\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex MetaDescriptionRegex();

    [GeneratedRegex("<link\\s+rel=\"canonical\"\\s+href=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex("<meta\\s+property=\"og:url\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex OgUrlRegex();

    [GeneratedRegex("<meta\\s+property=\"og:title\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex OgTitleRegex();

    [GeneratedRegex("<meta\\s+property=\"og:description\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex OgDescriptionRegex();

    [GeneratedRegex("<meta\\s+name=\"twitter:url\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex TwitterUrlRegex();

    [GeneratedRegex("<meta\\s+name=\"twitter:title\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex TwitterTitleRegex();

    [GeneratedRegex("<meta\\s+name=\"twitter:description\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex TwitterDescriptionRegex();

    [GeneratedRegex("<meta\\s+name=\"robots\"\\s+content=\"[^\"]*\"\\s*/?>", RegexOptions.Singleline)]
    private static partial Regex MetaRobotsRegex();

    private sealed class PrerenderHostEnvironment(string baseAddress) : IWebAssemblyHostEnvironment
    {
        public string Environment => "Production";
        public string BaseAddress => baseAddress;
    }

    private sealed class PrerenderNavigationManager : NavigationManager
    {
        public PrerenderNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            // No-op during static prerendering.
        }
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class OfflineStubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
