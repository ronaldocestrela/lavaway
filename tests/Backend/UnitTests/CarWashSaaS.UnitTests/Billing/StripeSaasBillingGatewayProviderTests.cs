using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class StripeSaasBillingGatewayProviderTests
{
    private readonly StripeBillingOptions _options = new()
    {
        SecretKey = "sk_test_mock_12345",
        WebhookSecret = "whsec_mock_12345",
        DefaultSuccessUrl = "https://lavaway.com/success",
        DefaultCancelUrl = "https://lavaway.com/cancel",
        Currency = "brl"
    };

    [Fact]
    public async Task CreateSubscriptionCheckoutSessionAsync_WithEmptyTenantId_ShouldReturnValidationError()
    {
        var provider = new StripeSaasBillingGatewayProvider(
            Options.Create(_options),
            NullLogger<StripeSaasBillingGatewayProvider>.Instance);

        var plan = SaasPlan.Create(SaasPlanTier.Pro, "Pro", "Plano Pro", 149m, 600, 1500, true, true, true, true, 10, 5).Value!;

        var result = await provider.CreateSubscriptionCheckoutSessionAsync(
            Guid.Empty,
            "Lava Jato",
            "contato@lavaway.com",
            plan,
            "https://lavaway.com/success",
            "https://lavaway.com/cancel");

        Assert.False(result.IsSuccess);
        Assert.Equal("stripe.tenant.required", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task CreateSubscriptionCheckoutSessionAsync_WithMissingSecretKey_ShouldReturnConflictError()
    {
        var invalidOptions = new StripeBillingOptions { SecretKey = null };
        var provider = new StripeSaasBillingGatewayProvider(
            Options.Create(invalidOptions),
            NullLogger<StripeSaasBillingGatewayProvider>.Instance);

        var plan = SaasPlan.Create(SaasPlanTier.Pro, "Pro", "Plano Pro", 149m, 600, 1500, true, true, true, true, 10, 5).Value!;

        var result = await provider.CreateSubscriptionCheckoutSessionAsync(
            Guid.NewGuid(),
            "Lava Jato",
            "contato@lavaway.com",
            plan,
            "https://lavaway.com/success",
            "https://lavaway.com/cancel");

        Assert.False(result.IsSuccess);
        Assert.Equal("stripe.config.missing_secret", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task CreateCustomerPortalSessionAsync_WithEmptyCustomerId_ShouldReturnValidationError()
    {
        var provider = new StripeSaasBillingGatewayProvider(
            Options.Create(_options),
            NullLogger<StripeSaasBillingGatewayProvider>.Instance);

        var result = await provider.CreateCustomerPortalSessionAsync(
            string.Empty,
            "https://lavaway.com/return");

        Assert.False(result.IsSuccess);
        Assert.Equal("stripe.customer_id.required", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_WithEmptySubscriptionId_ShouldReturnValidationError()
    {
        var provider = new StripeSaasBillingGatewayProvider(
            Options.Create(_options),
            NullLogger<StripeSaasBillingGatewayProvider>.Instance);

        var plan = SaasPlan.Create(SaasPlanTier.Enterprise, "Enterprise", "Plano Enterprise", 299m, 0, 0, true, true, true, true, 999, 0).Value!;

        var result = await provider.ChangeSubscriptionPlanAsync(
            string.Empty,
            plan);

        Assert.False(result.IsSuccess);
        Assert.Equal("stripe.subscription_id.required", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData("", "sig_123", "secret_123")]
    [InlineData("payload", "", "secret_123")]
    [InlineData("payload", "sig_123", "")]
    public void VerifyWebhookSignature_WithInvalidParameters_ShouldReturnFalse(string payload, string signatureHeader, string secret)
    {
        var provider = new StripeSaasBillingGatewayProvider(
            Options.Create(new StripeBillingOptions()),
            NullLogger<StripeSaasBillingGatewayProvider>.Instance);

        var isValid = provider.VerifyWebhookSignature(payload, signatureHeader, secret);

        Assert.False(isValid);
    }
}
