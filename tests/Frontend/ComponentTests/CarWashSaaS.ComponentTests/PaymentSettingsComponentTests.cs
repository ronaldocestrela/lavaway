using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Pages;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PaymentSettingsComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin@lavaway.com"),
                new Claim(ClaimTypes.Role, "Administrator"),
                new Claim("role", "Administrator")
            ], "TestAuth");

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private sealed class MockPaymentHttpHandler : HttpMessageHandler
    {
        public TenantPaymentGatewayConfigDto ConfigResponse { get; set; } = new(
            TenantId: Guid.NewGuid(),
            Provider: PaymentGatewayProviderConstants.PagarMe,
            PagarMePublicKey: "pk_test_123",
            PagarMeSecretKeyMasked: "sk_test_••••••••1234",
            HasSecretKey: true,
            PagarMeWebhookSecretMasked: null,
            HasWebhookSecret: false,
            IsActive: true,
            WebhookUrl: "https://api.lavaway.com.br/billing/webhooks/pagarme/11111111-1111-1111-1111-111111111111",
            LastTestedAtUtc: DateTimeOffset.UtcNow,
            LastTestSuccess: true,
            LastTestMessage: "Conexão validada com sucesso.");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.EndsWith("billing/gateway-config", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(ConfigResponse, JsonOptions);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (request.Method == HttpMethod.Put && path.EndsWith("billing/gateway-config", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(ConfigResponse, JsonOptions);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("billing/gateway-config/test", StringComparison.OrdinalIgnoreCase))
            {
                var testResult = new TestTenantGatewayConnectionResultDto(true, "Conexão bem sucedida.", DateTimeOffset.UtcNow);
                var json = JsonSerializer.Serialize(testResult, JsonOptions);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private void SetupServices(MockPaymentHttpHandler handler)
    {
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        Services.AddAuthorizationCore();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        Services.AddSingleton(new BillingApiClient(httpClient));
        Services.AddSingleton<IToastService, ToastService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void PaymentSettingsPage_WhenLoaded_ShouldRenderTitleAndPagarMeOptions()
    {
        var handler = new MockPaymentHttpHandler();
        SetupServices(handler);

        var cut = Render<PaymentSettingsPage>();

        Assert.Contains("Emissor de Cobrança e Pix", cut.Markup);
        Assert.Contains("Pagar.me (Stone Co.)", cut.Markup);
        Assert.Contains("Mercado Pago", cut.Markup);
        Assert.Contains("Ambiente Simulado", cut.Markup);
        Assert.Contains("Chave Secreta da API (Secret Key)", cut.Markup);
        Assert.Contains("Chave ativa gravada: sk_test_••••••••1234", cut.Markup);
        Assert.Contains("https://api.lavaway.com.br/billing/webhooks/pagarme/11111111-1111-1111-1111-111111111111", cut.Markup);
    }

    [Fact]
    public void PaymentSettingsPage_WhenTestingConnection_ShouldDisplayFeedback()
    {
        var handler = new MockPaymentHttpHandler();
        SetupServices(handler);

        var cut = Render<PaymentSettingsPage>();

        var testBtn = cut.Find("button.button-outline");
        testBtn.Click();

        Assert.Contains("Conexão bem sucedida.", cut.Markup);
    }
}
