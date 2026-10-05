using System.Net;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class WorkOrderPixModalTests : BunitContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void WorkOrderPixModal_WhenClosed_ShouldNotRenderModalBackdrop()
    {
        SetupServices(new MockBillingHttpHandler());

        var cut = Render<WorkOrderPixModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.WorkOrderId, Guid.NewGuid()));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void WorkOrderPixModal_WhenOpen_ShouldRenderDetailsAndQrCode()
    {
        var workOrderId = Guid.NewGuid();
        var handler = new MockBillingHttpHandler
        {
            ChargeResponse = new WorkOrderPixChargeDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                workOrderId,
                150.00m,
                PixChargeStatusConstants.Pending,
                "TX123",
                "PHN2Zz48L3N2Zz4=", // base64 SVG
                "00020126580014br.gov.bcb.pixEMVTESTKEY",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow)
        };

        SetupServices(handler);

        var cut = Render<WorkOrderPixModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, workOrderId)
            .Add(p => p.Plate, "BRA2E19")
            .Add(p => p.CustomerName, "Lucas Mendes")
            .Add(p => p.TotalAmount, 150.00m));

        Assert.Contains("BRA2E19", cut.Markup);
        Assert.Contains("Lucas Mendes", cut.Markup);
        Assert.Contains("150,00", cut.Markup);
        Assert.Contains("00020126580014br.gov.bcb.pixEMVTESTKEY", cut.Markup);
        Assert.NotEmpty(cut.FindAll("img.qr-code-image"));
        Assert.Contains("Aguardando Pagamento", cut.Markup);
    }

    [Fact]
    public void WorkOrderPixModal_ClickCopy_ShouldInvokeClipboard()
    {
        var workOrderId = Guid.NewGuid();
        var handler = new MockBillingHttpHandler
        {
            ChargeResponse = new WorkOrderPixChargeDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                workOrderId,
                80m,
                PixChargeStatusConstants.Pending,
                "TX123",
                "PHN2Zz48L3N2Zz4=",
                "00020126580014br.gov.bcb.pixCOPYSAMPLE",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow)
        };

        SetupServices(handler);

        var cut = Render<WorkOrderPixModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, workOrderId)
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.CustomerName, "Juliana")
            .Add(p => p.TotalAmount, 80m));

        var copyBtn = cut.Find("button.btn-copy");
        copyBtn.Click();

        JSInterop.VerifyInvoke("navigator.clipboard.writeText");
    }

    [Fact]
    public void WorkOrderPixModal_ClickWhatsApp_ShouldDispatchAndDisplaySentStatus()
    {
        var workOrderId = Guid.NewGuid();
        var sentAt = DateTimeOffset.UtcNow;
        var handler = new MockBillingHttpHandler
        {
            ChargeResponse = new WorkOrderPixChargeDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                workOrderId,
                90m,
                PixChargeStatusConstants.Pending,
                "TX123",
                "PHN2Zz48L3N2Zz4=",
                "00020126580014br.gov.bcb.pixWASAMPLE",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow),
            WhatsAppSendResponse = new WorkOrderPixChargeDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                workOrderId,
                90m,
                PixChargeStatusConstants.Pending,
                "TX123",
                "PHN2Zz48L3N2Zz4=",
                "00020126580014br.gov.bcb.pixWASAMPLE",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow,
                WhatsAppSentAtUtc: sentAt)
        };

        SetupServices(handler);

        var cut = Render<WorkOrderPixModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, workOrderId)
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.CustomerName, "Juliana")
            .Add(p => p.TotalAmount, 90m));

        var waBtn = cut.Find("button.btn-whatsapp");
        waBtn.Click();

        Assert.Contains("Enviado em", cut.Markup);
    }

    [Fact]
    public void WorkOrderPixModal_WhenPaid_ShouldRenderSettledConfirmationBox_AndHideQrCode()
    {
        var workOrderId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;
        var handler = new MockBillingHttpHandler
        {
            ChargeResponse = new WorkOrderPixChargeDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                workOrderId,
                150.00m,
                PixChargeStatusConstants.Paid,
                "TX-PAID-999",
                "PHN2Zz48L3N2Zz4=",
                "00020126580014br.gov.bcb.pixEMVPAID",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow,
                PaidAtUtc: paidAt)
        };

        SetupServices(handler);

        var cut = Render<WorkOrderPixModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, workOrderId)
            .Add(p => p.Plate, "BRA2E19")
            .Add(p => p.CustomerName, "Lucas Mendes")
            .Add(p => p.TotalAmount, 150.00m));

        Assert.Contains("Pagamento Pix Confirmado!", cut.Markup);
        Assert.Contains("A Ordem de Serviço foi baixada e conciliada financeiramente.", cut.Markup);
        Assert.Contains("TX-PAID-999", cut.Markup);
        Assert.Contains("✅ Pago / Baixado", cut.Markup);
        Assert.Empty(cut.FindAll("img.qr-code-image"));
        Assert.Empty(cut.FindAll("button.btn-copy"));
    }

    private void SetupServices(HttpMessageHandler handler)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new BillingApiClient(client));
        Services.AddSingleton<IToastService, ToastService>();
    }

    private sealed class MockBillingHttpHandler : HttpMessageHandler
    {
        public WorkOrderPixChargeDto? ChargeResponse { get; set; }
        public WorkOrderPixChargeDto? WhatsAppSendResponse { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;

            if (uri.Contains("/send-whatsapp", StringComparison.OrdinalIgnoreCase))
            {
                var responseObj = WhatsAppSendResponse ?? ChargeResponse;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj, JsonOptions))
                });
            }

            if (uri.Contains("/pix", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ChargeResponse, JsonOptions))
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
