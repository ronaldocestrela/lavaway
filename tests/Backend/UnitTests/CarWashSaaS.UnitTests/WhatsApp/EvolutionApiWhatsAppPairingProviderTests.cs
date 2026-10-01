using System.Net;
using System.Text;
using CarWashSaaS.WhatsApp.Infrastructure;

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

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
