using Bunit;
using CarWashSaaS.Client.Components;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class AnonymizeCustomerModalTests : BunitContext
{
    [Fact]
    public void AnonymizeCustomerModal_ShouldRenderCustomerInfoAndWarning_WhenVisible()
    {
        var cut = Render<AnonymizeCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.CustomerName, "Ricardo Almeida")
            .Add(p => p.Phone, "(11) 98765-4321"));

        Assert.Contains("Anonimizar Titular", cut.Markup);
        Assert.Contains("Ricardo Almeida", cut.Find("#target-customer-name").TextContent);
        Assert.Contains("(11) 98765-4321", cut.Find("#target-customer-phone").TextContent);
        Assert.Contains("Ação Irreversível e Definitiva", cut.Markup);

        var confirmBtn = cut.Find("#btn-confirm-anonymize");
        Assert.True(confirmBtn.HasAttribute("disabled"));
    }

    [Fact]
    public void AnonymizeCustomerModal_ShouldKeepConfirmDisabled_UntilReasonAndConsentGiven()
    {
        var cut = Render<AnonymizeCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.CustomerName, "Ricardo Almeida")
            .Add(p => p.Phone, "(11) 98765-4321"));

        var confirmBtn = cut.Find("#btn-confirm-anonymize");
        Assert.True(confirmBtn.HasAttribute("disabled"));

        // Digita apenas o motivo sem marcar o checkbox
        var reasonInput = cut.Find("#anonymize-reason");
        reasonInput.Input("Solicitação formal do titular via canal DPO");

        confirmBtn = cut.Find("#btn-confirm-anonymize");
        Assert.True(confirmBtn.HasAttribute("disabled"));

        // Marca o checkbox de consentimento
        var chk = cut.Find("#chk-confirm-anonymize");
        chk.Change(true);

        confirmBtn = cut.Find("#btn-confirm-anonymize");
        Assert.False(confirmBtn.HasAttribute("disabled"));
    }

    [Fact]
    public void AnonymizeCustomerModal_ShouldTriggerOnConfirm_WithEnteredReason()
    {
        string? confirmedReason = null;

        var cut = Render<AnonymizeCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.CustomerName, "Ricardo Almeida")
            .Add(p => p.Phone, "(11) 98765-4321")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create<string>(this, reason => confirmedReason = reason)));

        cut.Find("#anonymize-reason").Input("Exercício do Art. 18, VI da LGPD");
        cut.Find("#chk-confirm-anonymize").Change(true);

        var confirmBtn = cut.Find("#btn-confirm-anonymize");
        confirmBtn.Click();

        Assert.Equal("Exercício do Art. 18, VI da LGPD", confirmedReason);
    }

    [Fact]
    public void AnonymizeCustomerModal_ShouldDisplayErrorMessage_WhenProvided()
    {
        var cut = Render<AnonymizeCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.CustomerName, "Ricardo Almeida")
            .Add(p => p.ErrorMessage, "Não foi possível concluir a operação de anonimização."));

        var alert = cut.Find("#anonymize-error-alert");
        Assert.Contains("Não foi possível concluir a operação de anonimização.", alert.TextContent);
    }
}
