using System.Net;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class RegisterWorkOrderPaymentModalTests : BunitContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class MockCashierHttpHandler : HttpMessageHandler
    {
        public CashTransactionDto? SettleResponse { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = JsonSerializer.Serialize(SettleResponse ?? new CashTransactionDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                CashTransactionTypeConstants.Income,
                PaymentMethodConstants.Cash,
                150.00m,
                "Baixa OS",
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                "Operador",
                null), JsonOptions);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private void SetupServices(MockCashierHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new CashierApiClient(httpClient));
        Services.AddSingleton<IToastService, ToastService>();
    }

    [Fact]
    public void WhenClosed_ShouldNotRenderBackdrop()
    {
        SetupServices(new MockCashierHttpHandler());

        var cut = Render<RegisterWorkOrderPaymentModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.WorkOrderId, Guid.NewGuid()));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void WhenOpen_ShouldRenderDetailsAndTotalAmount()
    {
        SetupServices(new MockCashierHttpHandler());

        var cut = Render<RegisterWorkOrderPaymentModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, Guid.NewGuid())
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.CustomerName, "Julio Cesar")
            .Add(p => p.TotalAmount, 200.00m));

        Assert.Contains("ABC1D23", cut.Markup);
        Assert.Contains("Julio Cesar", cut.Markup);
        Assert.Contains("R$", cut.Markup);
        Assert.Contains("200,00", cut.Markup);
    }

    [Fact]
    public void WhenCashMethodSelected_RendersCashReceivedAndChangeCalculation()
    {
        SetupServices(new MockCashierHttpHandler());

        var cut = Render<RegisterWorkOrderPaymentModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, Guid.NewGuid())
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.TotalAmount, 100.00m));

        // Click Dinheiro
        var buttons = cut.FindAll(".method-btn");
        var cashButton = buttons.First(b => b.TextContent.Contains("Dinheiro"));
        cashButton.Click();

        Assert.Contains("Valor em Dinheiro Recebido", cut.Markup);
    }
}
