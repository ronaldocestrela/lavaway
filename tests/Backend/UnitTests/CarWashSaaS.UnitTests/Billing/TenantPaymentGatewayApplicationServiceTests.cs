using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.UnitTests.Billing;

public sealed class TenantPaymentGatewayApplicationServiceTests
{
    private sealed class FakePaymentConfigRepo : ITenantPaymentGatewayConfigRepository
    {
        public readonly Dictionary<Guid, TenantPaymentGatewayConfig> Storage = new();

        public Task<TenantPaymentGatewayConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        {
            Storage.TryGetValue(tenantId, out var config);
            return Task.FromResult(config);
        }

        public Task AddAsync(TenantPaymentGatewayConfig config, CancellationToken ct = default)
        {
            Storage[config.TenantId] = config;
            return Task.CompletedTask;
        }

        public void Update(TenantPaymentGatewayConfig config)
        {
            Storage[config.TenantId] = config;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class FakeEncryptor : IPaymentCredentialsEncryptor
    {
        public string Encrypt(string plainText) => $"ENC:{plainText}";
        public string Decrypt(string cipherText) => cipherText.StartsWith("ENC:") ? cipherText[4..] : cipherText;
    }

    [Fact]
    public async Task GetConfigAsync_WhenNoneExists_ShouldReturnDefaultWithCorrectWebhookUrl()
    {
        var repo = new FakePaymentConfigRepo();
        var service = new TenantPaymentGatewayApplicationService(
            repo,
            new FakeEncryptor(),
            new HttpClient());

        var tenantId = Guid.NewGuid();
        var result = await service.GetConfigAsync(tenantId, "https://api.lavaway.com.br");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentGatewayProviderConstants.PagarMe, result.Value.Provider);
        Assert.False(result.Value.HasSecretKey);
        Assert.Equal($"https://api.lavaway.com.br/billing/webhooks/pagarme/{tenantId}", result.Value.WebhookUrl);
    }

    [Fact]
    public async Task SaveConfigAsync_ShouldEncryptSecretKeyAndMaskInResponse()
    {
        var repo = new FakePaymentConfigRepo();
        var service = new TenantPaymentGatewayApplicationService(
            repo,
            new FakeEncryptor(),
            new HttpClient());

        var tenantId = Guid.NewGuid();
        var request = new SaveTenantPaymentGatewayConfigRequest(
            Provider: PaymentGatewayProviderConstants.PagarMe,
            PagarMePublicKey: "pk_test_abcdef123456",
            PagarMeSecretKey: "sk_test_super_secret_key_12345",
            PagarMeWebhookSecret: "wh_secret_xyz",
            IsActive: true);

        var result = await service.SaveConfigAsync(tenantId, request, "https://api.lavaway.com.br");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentGatewayProviderConstants.PagarMe, result.Value.Provider);
        Assert.True(result.Value.HasSecretKey);
        Assert.True(result.Value.HasWebhookSecret);
        Assert.Contains("••••••••", result.Value.PagarMeSecretKeyMasked!);
        Assert.DoesNotContain("super_secret_key", result.Value.PagarMeSecretKeyMasked!);

        var stored = repo.Storage[tenantId];
        Assert.Equal("ENC:sk_test_super_secret_key_12345", stored.PagarMeSecretKeyEncrypted);
    }

    [Fact]
    public async Task TestConnection_WithSimulatedProvider_ShouldReturnSuccess()
    {
        var repo = new FakePaymentConfigRepo();
        var service = new TenantPaymentGatewayApplicationService(
            repo,
            new FakeEncryptor(),
            new HttpClient());

        var tenantId = Guid.NewGuid();
        var testRequest = new TestTenantGatewayConnectionRequest(PaymentGatewayProviderConstants.Simulated);

        var result = await service.TestConnectionAsync(tenantId, testRequest);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Contains("Simulado", result.Value.Message);
    }
}
