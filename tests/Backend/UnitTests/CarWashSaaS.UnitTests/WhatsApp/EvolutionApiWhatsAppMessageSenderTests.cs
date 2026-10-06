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

    [Fact]
    public async Task SendMediaMessageAsync_Should_Succeed_When_Evolution_Returns_200()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"key\":{\"id\":\"EVOLUTION-MEDIA-999\"}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var sender = new EvolutionApiWhatsAppMessageSender(client, "test-api-key", "lavaway");

        var result = await sender.SendMediaMessageAsync(
            tenantId,
            "5511999998888",
            "data:application/pdf;base64,JVBERi0x...",
            "document",
            "application/pdf",
            "comprovante.pdf",
            "Aqui está seu comprovante!");

        Assert.True(result.IsSuccess);
        Assert.Equal("EVOLUTION-MEDIA-999", result.Value);
    }


    [Fact]
    public async Task SendTextMessageAsync_Should_Send_Correct_Evolution_Payload_And_Normalize_Phone()
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

        // Testar com número de 11 dígitos sem DDI
        var result = await sender.SendTextMessageAsync(tenantId, "11999998888", "Olá!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequestBody);

        using var jsonDoc = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!);
        var root = jsonDoc.RootElement;

        // Confere se o número foi normalizado para 5511999998888
        Assert.Equal("5511999998888", root.GetProperty("number").GetString());
        // Confere se o payload contém textMessage.text conforme exigido pelo Evolution API v1.8.2
        Assert.True(root.TryGetProperty("textMessage", out var textMsgElement));
        Assert.Equal("Olá!", textMsgElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendMediaMessageAsync_Should_Send_Correct_Evolution_MediaPayload()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"key\":{\"id\":\"EVOLUTION-MEDIA-999\"}}", Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://evolution.example.com/")
        };

        var sender = new EvolutionApiWhatsAppMessageSender(client, "test-api-key", "lavaway");

        var result = await sender.SendMediaMessageAsync(
            tenantId,
            "11999998888",
            "data:application/pdf;base64,JVBERi0x...",
            "document",
            "application/pdf",
            "comprovante.pdf",
            "Aqui está seu comprovante!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequestBody);

        using var jsonDoc = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!);
        var root = jsonDoc.RootElement;

        Assert.Equal("5511999998888", root.GetProperty("number").GetString());
        Assert.Equal("document", root.GetProperty("mediatype").GetString());
        Assert.Equal("Aqui está seu comprovante!", root.GetProperty("caption").GetString());
        Assert.Equal("comprovante.pdf", root.GetProperty("fileName").GetString());
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return response;
        }
    }
}
