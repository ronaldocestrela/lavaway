using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;

namespace CarWashSaaS.UnitTests.Shared;

public sealed class TenantStorageAndQueueTests
{
    [Fact]
    public async Task EnqueueAsync_Should_Preserve_Tenant_And_Message_Data()
    {
        var queue = new InMemoryBackgroundQueue();
        var tenantId = Guid.NewGuid();

        await queue.EnqueueAsync(new TenantQueueMessage(tenantId, "work-order.ready", "payload-123"));

        var result = await queue.DequeueAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(tenantId, result!.Message.TenantId);
        Assert.Equal("work-order.ready", result.Message.EventType);
        Assert.Equal("payload-123", result.Message.Payload);
        await result.CompleteAsync();
    }

    [Fact]
    public async Task RetryAsync_ShouldMakeMessageAvailableAgainWithIncrementedDeliveryCount()
    {
        var queue = new InMemoryBackgroundQueue();
        var message = new TenantQueueMessage(Guid.NewGuid(), "test.retry", "payload");
        await queue.EnqueueAsync(message);

        var firstDelivery = await queue.DequeueAsync(CancellationToken.None);
        Assert.NotNull(firstDelivery);
        await firstDelivery!.RetryAsync();

        var retryDelivery = await queue.DequeueAsync(CancellationToken.None);
        Assert.NotNull(retryDelivery);
        Assert.Equal(message.MessageId, retryDelivery!.Message.MessageId);
        Assert.Equal(1, retryDelivery.DeliveryCount);
        await retryDelivery.CompleteAsync();
    }

    [Fact]
    public void BuildStoragePath_Should_Scope_Files_By_Tenant_And_Category()
    {
        var tenantId = Guid.NewGuid();

        var path = TenantStoragePathBuilder.BuildPath(tenantId, "photos", "before.jpg");

        Assert.StartsWith("tenants/", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/{tenantId}/", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/photos/", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("before.jpg", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveLogoAsync_ShouldStoreObjectWithinCurrentTenantNamespace()
    {
        var storage = new RecordingTenantObjectStorage();
        var service = new TenantBrandingStorageService(storage);
        var tenantId = Guid.NewGuid();
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await service.SaveLogoAsync(tenantId, "logo.png", content.Length, content);

        Assert.True(result.IsSuccess);
        Assert.StartsWith($"tenants/{tenantId}/branding/", storage.ObjectKey);
        Assert.Equal("image/png", storage.ContentType);
        Assert.StartsWith("/tenants/profile/logo/", result.Value);
    }

    [Fact]
    public async Task GetLogoAsync_ShouldUseTenantNamespaceAndRejectPathTraversal()
    {
        var storage = new RecordingTenantObjectStorage();
        var service = new TenantBrandingStorageService(storage);
        var tenantId = Guid.NewGuid();

        var missing = await service.GetLogoAsync(tenantId, "logo.png");
        var invalid = await service.GetLogoAsync(tenantId, "../logo.png");

        Assert.Null(missing);
        Assert.Null(invalid);
        Assert.Equal($"tenants/{tenantId}/branding/logo.png", storage.ReadObjectKey);
    }

    [Fact]
    public async Task TenantBrandingAuditQueueHandler_Should_Succeed_WhenTenantMatchesScope()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantBrandingAuditQueueHandler>.Instance;
        var handler = new TenantBrandingAuditQueueHandler(accessor, logger);
        var message = new TenantQueueMessage(tenantId, TenantBrandingAuditQueueHandler.EventName, "/tenants/profile/logo/logo.png");

        await handler.HandleAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task TenantBrandingAuditQueueHandler_Should_Throw_WhenTenantScopeMismatches()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantA);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantBrandingAuditQueueHandler>.Instance;
        var handler = new TenantBrandingAuditQueueHandler(accessor, logger);
        var message = new TenantQueueMessage(tenantB, TenantBrandingAuditQueueHandler.EventName, "/tenants/profile/logo/logo.png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(message, CancellationToken.None));
    }

    private sealed class RecordingTenantObjectStorage : ITenantObjectStorage
    {
        public string? ObjectKey { get; private set; }
        public string? ReadObjectKey { get; private set; }
        public string? ContentType { get; private set; }

        public Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
        {
            ObjectKey = $"tenants/{tenantId}/{category}/{fileName}";
            ContentType = contentType;
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
        {
            ReadObjectKey = $"tenants/{tenantId}/{category}/{fileName}";
            return Task.FromResult<StoredObject?>(null);
        }
    }
}
