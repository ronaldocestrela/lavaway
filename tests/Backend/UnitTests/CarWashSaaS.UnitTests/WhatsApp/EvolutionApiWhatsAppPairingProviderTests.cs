using System.Net;
using System.Text;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class EvolutionApiWhatsAppPairingProviderTests
{
    [Fact]
    public async Task GeneratePairingAsync_Should_Use_Evolution_Api_Response_To_Produce_QrCode_And_Instance_Name()
    {
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"qrcode\":{\"code\":\"2@raw-pairing-code\",\"base64\":\"data:image/png;base64,test-qr\"},\"instance\":{\"name\":\"tenant-123\"}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(
            client,
            "test-key",
            "lavaway",
            "http://host.docker.internal:5225/whatsapp/webhooks/evolution");

        var result = await provider.GeneratePairingAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.True(result.IsSuccess);
        Assert.Equal("lavaway-11111111111111111111111111111111", result.Value.ProviderSessionId);
        Assert.Equal("2@raw-pairing-code", result.Value.QrCodeValue);
        Assert.NotNull(handler.LastRequestBody);
        using var requestJson = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody);
        var requestRoot = requestJson.RootElement;
        Assert.Equal("WHATSAPP-BAILEYS", requestRoot.GetProperty("integration").GetString());
        Assert.True(requestRoot.GetProperty("qrcode").GetBoolean());
        var webhook = requestRoot.GetProperty("webhook");
        Assert.True(webhook.GetProperty("enabled").GetBoolean());
        Assert.Equal("http://host.docker.internal:5225/whatsapp/webhooks/evolution", webhook.GetProperty("url").GetString());
        Assert.False(webhook.GetProperty("byEvents").GetBoolean());
        Assert.Equal(4, webhook.GetProperty("events").GetArrayLength());
        Assert.False(requestRoot.TryGetProperty("Integration", out _));
        Assert.False(requestRoot.TryGetProperty("webhookUrl", out _));
    }

    [Fact]
    public async Task GeneratePairingAsync_Should_Return_Failure_When_Network_Fails()
    {
        var handler = new ExceptionHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await provider.GeneratePairingAsync(tenantId);

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.network_error", result.Error!.Code);
    }

    [Fact]
    public async Task GeneratePairingAsync_WhenInstanceAlreadyExists_ShouldConnectViaGetAndReturnBase64QrCode()
    {
        var handler = new DynamicHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("instance/create"))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"status\":403,\"error\":\"Forbidden\",\"response\":{\"message\":[\"This name is already in use.\"]}}", Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("instance/connect/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"pairingCode\":null,\"code\":\"2@raw-pairing-code\",\"base64\":\"data:image/png;base64,real-base64-qr\",\"count\":1}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await provider.GeneratePairingAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("lavaway-11111111111111111111111111111111", result.Value.ProviderSessionId);
        Assert.Equal("2@raw-pairing-code", result.Value.QrCodeValue);
    }

    [Fact]
    public async Task GeneratePairingAsync_WhenCreateReturnsNestedQrCodeObject_ShouldExtractBase64QrCode()
    {
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"instance\":{\"instanceName\":\"lavaway-11111111111111111111111111111111\"},\"qrcode\":{\"pairingCode\":null,\"code\":\"2@baileys-code\",\"base64\":\"data:image/png;base64,created-base64-qr\",\"count\":1}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await provider.GeneratePairingAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("lavaway-11111111111111111111111111111111", result.Value.ProviderSessionId);
        Assert.Equal("2@baileys-code", result.Value.QrCodeValue);
    }

    [Fact]
    public async Task GeneratePairingAsync_Should_Return_Failure_When_Evolution_Returns_No_Real_QrCode()
    {
        var handler = new DynamicHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"instance\":{\"instanceName\":\"lavaway-11111111111111111111111111111111\"},\"qrcode\":null}", Encoding.UTF8, "application/json")
            });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080/") };
        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");

        var result = await provider.GeneratePairingAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.qr_code_unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task GeneratePairingAsync_Should_Not_Connect_When_Create_Fails_With_Invalid_Instance_Payload()
    {
        var handler = new DynamicHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"Bad Request\",\"message\":\"Invalid integration\"}", Encoding.UTF8, "application/json")
            });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080/") };
        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");

        var result = await provider.GeneratePairingAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.http_error", result.Error!.Code);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Logout_Tenant_Instance_In_Evolution_Api()
    {
        var handler = new DynamicHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await provider.DisconnectAsync("lavaway-11111111111111111111111111111111");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal("/instance/logout/lavaway-11111111111111111111111111111111", handler.LastRequestPath);
        Assert.Equal("test-key", handler.LastApiKey);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Return_Failure_When_Evolution_Api_Rejects_Logout()
    {
        var handler = new DynamicHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"message\":\"Logout failed\"}", Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");

        var result = await provider.DisconnectAsync("lavaway-11111111111111111111111111111111");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.http_error", result.Error!.Code);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Return_Failure_When_Network_Fails()
    {
        var handler = new ExceptionHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");

        var result = await provider.DisconnectAsync("lavaway-11111111111111111111111111111111");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.network_error", result.Error!.Code);
    }

    [Fact]
    public void DependencyInjection_Should_Resolve_EvolutionApiWhatsAppPairingProvider_Without_Ambiguity()
    {
        var services = new ServiceCollection();
        var configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            ["WhatsApp:EvolutionApi:BaseUrl"] = "http://localhost:8080/",
            ["WhatsApp:EvolutionApi:ApiKey"] = "test-api-key",
            ["WhatsApp:EvolutionApi:InstanceNamePrefix"] = "lavaway",
            ["WhatsApp:EvolutionApi:WebhookUrl"] = "http://host.docker.internal:5225/whatsapp/webhooks/evolution"
        });

        services.AddSingleton<IConfiguration>(configuration);
        using var provider = services.BuildServiceProvider();

        using var httpClient = new HttpClient();
        var factory = ActivatorUtilities.CreateFactory(typeof(EvolutionApiWhatsAppPairingProvider), [typeof(HttpClient)]);
        var instance = factory(provider, [httpClient]);

        Assert.NotNull(instance);
        Assert.IsType<EvolutionApiWhatsAppPairingProvider>(instance);
    }

    private sealed class StubConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var v) ? v : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return response;
        }
    }

    private sealed class DynamicHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastRequestPath { get; private set; }
        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestPath = request.RequestUri?.AbsolutePath;
            LastApiKey = request.Headers.TryGetValues("apikey", out var values) ? values.Single() : null;
            return Task.FromResult(handler(request));
        }
    }

    private sealed class ExceptionHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
