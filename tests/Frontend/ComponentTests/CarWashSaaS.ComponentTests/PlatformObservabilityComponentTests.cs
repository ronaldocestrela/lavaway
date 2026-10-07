using System.Reflection;
using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Web.Pages;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PlatformObservabilityComponentTests : BunitContext
{
    [Fact]
    public void WebhookPayloadInspectionModal_ShouldRenderDetails_WhenOpen_AndTriggerClose()
    {
        var log = new PlatformWebhookLogDto(
            Id: Guid.NewGuid(),
            Provider: "Pix (MercadoPago)",
            EventType: "payment.received",
            TenantId: Guid.NewGuid(),
            TenantName: "Lava Jato Central",
            Status: "Processed",
            TxId: "tx_mock_123456",
            PayloadHash: "sha256_mock_hash",
            Notes: "Liquidado com sucesso via webhook",
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            ProcessedAtUtc: DateTimeOffset.UtcNow);

        var closeTriggered = false;

        var cut = Render<WebhookPayloadInspectionModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Log, log)
            .Add(p => p.OnClose, () => closeTriggered = true));

        Assert.Contains("Inspeção de Evento de Webhook", cut.Markup);
        Assert.Contains("Pix (MercadoPago)", cut.Markup);
        Assert.Contains("payment.received", cut.Markup);
        Assert.Contains("Lava Jato Central", cut.Markup);
        Assert.Contains("tx_mock_123456", cut.Markup);
        Assert.Contains("Liquidado com sucesso via webhook", cut.Markup);

        var closeButton = cut.Find("button.bg-slate-100");
        closeButton.Click();

        Assert.True(closeTriggered);
    }

    [Fact]
    public void WebhookPayloadInspectionModal_ShouldNotRender_WhenIsOpenIsFalse()
    {
        var cut = Render<WebhookPayloadInspectionModal>(parameters => parameters
            .Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void WhatsAppFailureDetailsModal_ShouldRenderDetails_WhenOpen_AndTriggerClose()
    {
        var failure = new PlatformWhatsAppFailureDto(
            MessageId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            TenantName: "Estética Car Wash VIP",
            RecipientPhoneMasked: "+55 (11) ****-9876",
            BodyPreview: "Seu veículo modelo Corolla está com os serviços finalizados!",
            FailureReason: "CONNECTION_DROPPED: Falha temporária no socket",
            AttemptCount: 3,
            CreatedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-15),
            LastAttemptAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));

        var closeTriggered = false;

        var cut = Render<WhatsAppFailureDetailsModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Failure, failure)
            .Add(p => p.OnClose, () => closeTriggered = true));

        Assert.Contains("Diagnóstico de Falha de Mensageria", cut.Markup);
        Assert.Contains("Estética Car Wash VIP", cut.Markup);
        Assert.Contains("+55 (11) ****-9876", cut.Markup);
        Assert.Contains("CONNECTION_DROPPED: Falha temporária no socket", cut.Markup);
        Assert.Contains("Seu veículo modelo Corolla está com os serviços finalizados!", cut.Markup);
        Assert.Contains("3 tentativa(s)", cut.Markup);

        var closeButton = cut.Find("button.bg-slate-100");
        closeButton.Click();

        Assert.True(closeTriggered);
    }

    [Fact]
    public void PlatformObservabilityPage_MustHaveAuthorizeAttributeWithProperRoles()
    {
        var authAttr = typeof(PlatformObservabilityPage).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
        Assert.Equal("SuperAdmin,PlatformAuditor,PlatformSupport,PlatformBillingAdmin", authAttr.Roles);
    }
}
