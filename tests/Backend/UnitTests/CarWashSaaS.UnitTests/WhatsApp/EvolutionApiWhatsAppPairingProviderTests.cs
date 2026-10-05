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
                Content = new StringContent("{\"qrcode\":\"data:image/png;base64,test-qr\",\"instance\":{\"name\":\"tenant-123\"}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");

        var result = await provider.GeneratePairingAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal("lavaway-11111111111111111111111111111111", result.ProviderSessionId);
        Assert.Equal("data:image/png;base64,test-qr", result.QrCodeValue);
    }

    [Fact]
    public async Task GeneratePairingAsync_Should_Return_Fallback_When_Network_Fails()
    {
        var handler = new ExceptionHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };

        var provider = new EvolutionApiWhatsAppPairingProvider(client, "test-key", "lavaway");
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await provider.GeneratePairingAsync(tenantId);

        Assert.Equal("lavaway-11111111111111111111111111111111", result.ProviderSessionId);
        Assert.StartsWith("evolution:lavaway-11111111111111111111111111111111:", result.QrCodeValue);
    }

    [Fact]
    public void DependencyInjection_Should_Resolve_EvolutionApiWhatsAppPairingProvider_Without_Ambiguity()
    {
        var services = new ServiceCollection();
        var configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            ["WhatsApp:EvolutionApi:BaseUrl"] = "http://localhost:8080/",
            ["WhatsApp:EvolutionApi:ApiKey"] = "test-api-key",
            ["WhatsApp:EvolutionApi:InstanceNamePrefix"] = "lavaway"
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class ExceptionHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}

