using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class RedeemLoyaltyRewardModalTests : BunitContext
{
    [Fact]
    public void WhenClosed_ShouldNotRenderBackdrop()
    {
        var cut = Render<RedeemLoyaltyRewardModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.CustomerId, Guid.NewGuid()));

        Assert.Empty(cut.FindAll(".modal-backdrop"));
    }

    [Fact]
    public void WhenOpen_ShouldRenderRewardTitleAndCost()
    {
        var cut = Render<RedeemLoyaltyRewardModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CustomerId, Guid.NewGuid())
            .Add(p => p.CustomerName, "Julio Cesar")
            .Add(p => p.Balance, 10)
            .Add(p => p.TargetStamps, 10)
            .Add(p => p.RewardTitle, "Lavagem Completa Grátis"));

        Assert.Contains("Julio Cesar", cut.Markup);
        Assert.Contains("Lavagem Completa Grátis", cut.Markup);
        Assert.Contains("10 selos", cut.Markup);
    }

    [Fact]
    public void WhenBalanceInsufficient_ShouldDisplayWarningAndDisableButton()
    {
        var cut = Render<RedeemLoyaltyRewardModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CustomerId, Guid.NewGuid())
            .Add(p => p.CustomerName, "Carlos")
            .Add(p => p.Balance, 8)
            .Add(p => p.TargetStamps, 10)
            .Add(p => p.RewardTitle, "Lavagem Grátis"));

        Assert.Contains("Saldo insuficiente para resgate", cut.Markup);
        var submitBtn = cut.Find(".btn-primary");
        Assert.True(submitBtn.HasAttribute("disabled"));
    }

    [Fact]
    public void WhenBalanceSufficient_ClickingConfirm_InvokesCallback()
    {
        RedeemLoyaltyRewardRequest? capturedRequest = null;

        var cut = Render<RedeemLoyaltyRewardModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.CustomerId, Guid.NewGuid())
            .Add(p => p.CustomerName, "Mariana")
            .Add(p => p.Balance, 10)
            .Add(p => p.TargetStamps, 10)
            .Add(p => p.RewardTitle, "Lavagem Completa Grátis")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create<RedeemLoyaltyRewardRequest>(this, req => capturedRequest = req)));

        var submitBtn = cut.Find(".btn-primary");
        Assert.False(submitBtn.HasAttribute("disabled"));
        submitBtn.Click();

        Assert.NotNull(capturedRequest);
    }
}
