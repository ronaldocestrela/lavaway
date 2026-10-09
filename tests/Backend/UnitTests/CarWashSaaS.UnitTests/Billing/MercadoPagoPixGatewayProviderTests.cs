using System.Net;
using System.Text.Json;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Infrastructure.Gateways;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class MercadoPagoPixGatewayProviderTests
{
    [Theory]
    [InlineData("(11) 98765-4321", "cliente11987654321@lavaway.com.br")]
    [InlineData("+55 (11) 99999-8888", "cliente5511999998888@lavaway.com.br")]
    [InlineData("11987654321", "cliente11987654321@lavaway.com.br")]
    [InlineData("  (21) 91234-5678  ", "cliente21912345678@lavaway.com.br")]
    public void BuildPayerInfo_WithFormattedPhone_ShouldExtractDigitsAndCreateValidEmail(string phone, string expectedEmail)
    {
        var workOrderId = Guid.NewGuid();
        var (email, firstName) = MercadoPagoPixGatewayProvider.BuildPayerInfo("João Silva", phone, workOrderId);

        Assert.Equal(expectedEmail, email);
        Assert.Equal("João Silva", firstName);
    }

    [Fact]
    public void BuildPayerInfo_WithEmptyPhone_ShouldFallbackToWorkOrderIdEmail()
    {
        var workOrderId = Guid.Parse("12345678-abcd-ef01-2345-6789abcdef01");
        var (email, firstName) = MercadoPagoPixGatewayProvider.BuildPayerInfo(null, "", workOrderId);

        Assert.Equal("cliente.os12345678@lavaway.com.br", email);
        Assert.Equal("Cliente", firstName);
    }

    [Fact]
    public async Task CreateImmediateChargeAsync_ShouldSendSanitizedEmailInPayerPayload()
    {
        string? capturedPayloadJson = null;

        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Content is not null)
            {
                capturedPayloadJson = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var responseJson = """
            {
                "id": 9876543210,
                "status": "pending",
                "date_of_expiration": "2026-10-09T00:00:00.000Z",
                "point_of_interaction": {
                    "transaction_data": {
                        "qr_code": "00020126580014br.gov.bcb.pix...",
                        "qr_code_base64": "iVBORw0KGgoAAAANSUhEUgAA..."
                    }
                }
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var provider = new MercadoPagoPixGatewayProvider(httpClient, "TEST_ACCESS_TOKEN");

        var workOrderId = Guid.NewGuid();
        var chargeRequest = new PixGatewayChargeRequest(
            TenantId: Guid.NewGuid(),
            WorkOrderId: workOrderId,
            Amount: 75.50m,
            Description: "Lavagem Completa",
            CustomerName: "Carlos Pereira",
            CustomerPhone: "+55 (11) 98765-4321",
            Expiration: TimeSpan.FromMinutes(30));

        var result = await provider.CreateImmediateChargeAsync(chargeRequest);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("9876543210", result.Value.TxId);
        Assert.Equal("00020126580014br.gov.bcb.pix...", result.Value.CopyPasteKey);

        Assert.NotNull(capturedPayloadJson);
        using var doc = JsonDocument.Parse(capturedPayloadJson);
        var payerEmail = doc.RootElement.GetProperty("payer").GetProperty("email").GetString();
        var payerFirstName = doc.RootElement.GetProperty("payer").GetProperty("first_name").GetString();

        Assert.Equal("cliente5511987654321@lavaway.com.br", payerEmail);
        Assert.Equal("Carlos Pereira", payerFirstName);
        Assert.DoesNotContain(" ", payerEmail);
        Assert.DoesNotContain("(", payerEmail);
        Assert.DoesNotContain(")", payerEmail);
        Assert.DoesNotContain("+", payerEmail);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handlerFunc(request, cancellationToken);
    }
}
