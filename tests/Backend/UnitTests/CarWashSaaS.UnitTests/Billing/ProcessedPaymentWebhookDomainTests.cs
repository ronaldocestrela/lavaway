using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.UnitTests.Billing;

public class ProcessedPaymentWebhookDomainTests
{
    [Fact]
    public void Create_Should_Instantiate_With_RawPayload_When_Provided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var rawPayload = "{\"id\":\"evt_123\",\"payer\":{\"name\":\"João Silva\",\"cpf\":\"12345678900\",\"ip\":\"192.168.1.1\"}}";

        // Act
        var result = ProcessedPaymentWebhook.Create(
            tenantId,
            "PagarMe",
            "evt_123",
            "tx_456",
            "Processed",
            payloadHash: "hash_test",
            notes: "Pagamento aprovado",
            rawPayload: rawPayload);

        // Assert
        Assert.True(result.IsSuccess);
        var webhook = result.Value!;
        Assert.Equal(tenantId, webhook.TenantId);
        Assert.Equal("PagarMe", webhook.Provider);
        Assert.Equal("evt_123", webhook.EventId);
        Assert.Equal("tx_456", webhook.TxId);
        Assert.Equal(rawPayload, webhook.RawPayload);
        Assert.False(webhook.IsPurged);
        Assert.Null(webhook.PurgedAtUtc);
    }

    [Fact]
    public void PurgePayload_Should_Discard_RawPayload_And_Mark_IsPurged_Preserving_Metadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var rawPayload = "{\"id\":\"evt_123\",\"payer\":{\"name\":\"João Silva\",\"cpf\":\"12345678900\",\"ip\":\"192.168.1.1\"}}";
        var webhook = ProcessedPaymentWebhook.Create(
            tenantId,
            "MercadoPago",
            "mp_evt_999",
            "tx_pix_888",
            "Processed",
            payloadHash: "sha256_abc",
            notes: "Conciliado com sucesso",
            rawPayload: rawPayload).Value!;

        var purgeTime = DateTimeOffset.UtcNow;

        // Act
        var purgeResult = webhook.PurgePayload(purgeTime);

        // Assert
        Assert.True(purgeResult.IsSuccess);
        Assert.Null(webhook.RawPayload);
        Assert.True(webhook.IsPurged);
        Assert.Equal(purgeTime, webhook.PurgedAtUtc);
        // Metadados fiscais e de conciliação permanecem intactos
        Assert.Equal(tenantId, webhook.TenantId);
        Assert.Equal("MercadoPago", webhook.Provider);
        Assert.Equal("mp_evt_999", webhook.EventId);
        Assert.Equal("tx_pix_888", webhook.TxId);
        Assert.Equal("Processed", webhook.Status);
        Assert.Contains("[Higienizado - LGPD Art. 16]", webhook.Notes);
    }

    [Fact]
    public void PurgePayload_Should_Be_Idempotent_When_Already_Purged()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var webhook = ProcessedPaymentWebhook.Create(
            tenantId,
            "MercadoPago",
            "mp_evt_111",
            "tx_pix_222",
            "Processed",
            rawPayload: "{\"sensitive\":\"data\"}").Value!;

        var firstPurge = DateTimeOffset.UtcNow.AddMinutes(-10);
        webhook.PurgePayload(firstPurge);

        // Act
        var secondPurgeTime = DateTimeOffset.UtcNow;
        var secondResult = webhook.PurgePayload(secondPurgeTime);

        // Assert
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstPurge, webhook.PurgedAtUtc);
        Assert.Null(webhook.RawPayload);
        Assert.True(webhook.IsPurged);
    }
}
