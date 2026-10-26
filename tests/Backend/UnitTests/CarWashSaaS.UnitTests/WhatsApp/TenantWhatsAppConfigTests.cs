using CarWashSaaS.WhatsApp.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.WhatsApp;

public sealed class TenantWhatsAppConfigTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        var tenantId = Guid.NewGuid();
        var encryptedKey = "encryptedApiKeyBase64Value";
        var encryptedWebhookSecret = "encryptedWebhookSecretValue";
        var instanceName = "auto-detailing-sp";

        var result = TenantWhatsAppConfig.Create(
            tenantId,
            encryptedKey,
            encryptedWebhookSecret,
            instanceName,
            isActive: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(encryptedKey, result.Value.EvolutionApiKeyEncrypted);
        Assert.Equal(encryptedWebhookSecret, result.Value.WebhookSecretEncrypted);
        Assert.Equal(instanceName, result.Value.InstanceCustomName);
        Assert.True(result.Value.IsActive);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Create_WhenTenantIdIsEmpty_ShouldReturnFailure()
    {
        var result = TenantWhatsAppConfig.Create(
            Guid.Empty,
            "key",
            "secret",
            "inst");

        Assert.False(result.IsSuccess);
        Assert.Equal("whatsapp.tenant_required", result.Error!.Code);
    }

    [Fact]
    public void Update_ShouldModifyPropertiesAndSetUpdatedAt()
    {
        var tenantId = Guid.NewGuid();
        var config = TenantWhatsAppConfig.Create(tenantId, "oldKey", "oldSec", "oldInst").Value!;

        var updateResult = config.Update("newEncryptedKey", "newEncryptedSecret", "newInst", false);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("newEncryptedKey", config.EvolutionApiKeyEncrypted);
        Assert.Equal("newEncryptedSecret", config.WebhookSecretEncrypted);
        Assert.Equal("newInst", config.InstanceCustomName);
        Assert.False(config.IsActive);
        Assert.NotNull(config.UpdatedAtUtc);
    }

    [Fact]
    public void RecordTestResult_ShouldUpdateStatus()
    {
        var config = TenantWhatsAppConfig.Create(Guid.NewGuid(), "key", "sec", "inst").Value!;

        config.RecordTestResult(true, "Instância conectada");

        Assert.True(config.LastTestSuccess);
        Assert.Equal("Instância conectada", config.LastTestMessage);
        Assert.NotNull(config.LastTestedAtUtc);
    }
}
