using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantPaymentGatewayConfigDomainTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        var result = TenantPaymentGatewayConfig.Create(
            tenantId,
            PaymentGatewayProviderConstants.PagarMe,
            pagarMeSecretKeyEncrypted: "encrypted_secret_123",
            pagarMePublicKey: "pk_test_123",
            pagarMeWebhookSecretEncrypted: "webhook_secret_123",
            isActive: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(PaymentGatewayProviderConstants.PagarMe, result.Value.Provider);
        Assert.Equal("encrypted_secret_123", result.Value.PagarMeSecretKeyEncrypted);
        Assert.Equal("pk_test_123", result.Value.PagarMePublicKey);
        Assert.Equal("webhook_secret_123", result.Value.PagarMeWebhookSecretEncrypted);
        Assert.True(result.Value.IsActive);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldFail()
    {
        var result = TenantPaymentGatewayConfig.Create(
            Guid.Empty,
            PaymentGatewayProviderConstants.PagarMe);

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.tenant_required", result.Error?.Code);
    }

    [Fact]
    public void Create_WithInvalidProvider_ShouldFail()
    {
        var result = TenantPaymentGatewayConfig.Create(
            Guid.NewGuid(),
            "InvalidProvider");

        Assert.False(result.IsSuccess);
        Assert.Equal("billing.invalid_provider", result.Error?.Code);
    }

    [Fact]
    public void Update_ShouldModifyPropertiesAndKeepTimestamp()
    {
        var config = TenantPaymentGatewayConfig.Create(
            Guid.NewGuid(),
            PaymentGatewayProviderConstants.PagarMe,
            "old_secret",
            "old_pk",
            "old_wh",
            false).Value!;

        var updateResult = config.Update(
            PaymentGatewayProviderConstants.Simulated,
            "new_secret",
            "new_pk",
            "new_wh",
            true);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(PaymentGatewayProviderConstants.Simulated, config.Provider);
        Assert.Equal("new_secret", config.PagarMeSecretKeyEncrypted);
        Assert.Equal("new_pk", config.PagarMePublicKey);
        Assert.True(config.IsActive);
        Assert.NotNull(config.UpdatedAtUtc);
    }

    [Fact]
    public void RecordTestResult_ShouldUpdateStatusAndTimestamp()
    {
        var config = TenantPaymentGatewayConfig.Create(
            Guid.NewGuid(),
            PaymentGatewayProviderConstants.PagarMe).Value!;

        config.RecordTestResult(true, "Conexão validada com sucesso na API v5.");

        Assert.True(config.LastTestSuccess);
        Assert.Equal("Conexão validada com sucesso na API v5.", config.LastTestMessage);
        Assert.NotNull(config.LastTestedAtUtc);
    }
}
