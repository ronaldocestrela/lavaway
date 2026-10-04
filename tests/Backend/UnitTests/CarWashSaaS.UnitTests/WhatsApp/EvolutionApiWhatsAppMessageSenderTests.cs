using System.Net;
using System.Text;
using CarWashSaaS.WhatsApp.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class EvolutionApiWhatsAppMessageSenderTests
{
    [Fact]
    public async Task SendTextMessageAsync_Should_Succeed_When_Evolution_Returns_200()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"key\":{\"id\":\"EVOLUTION-MSG-12345\"}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var sender = new EvolutionApiWhatsAppMessageSender(client, "test-api-key", "lavaway");

        var result = await sender.SendTextMessageAsync(tenantId, "5511999998888", "Olá!");

        Assert.True(result.IsSuccess);
        Assert.Equal("EVOLUTION-MSG-12345", result.Value);
    }

    [Fact]
    public async Task SendTextMessageAsync_Should_Return_Failure_When_Evolution_Returns_400()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"Number does not exist\"}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var sender = new EvolutionApiWhatsAppMessageSender(client, "test-api-key", "lavaway");

        var result = await sender.SendTextMessageAsync(tenantId, "123", "Olá!");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.provider.error", result.Error!.Code);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
