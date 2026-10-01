using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;

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
        Assert.Equal(tenantId, result!.TenantId);
        Assert.Equal("work-order.ready", result.EventType);
        Assert.Equal("payload-123", result.Payload);
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
}
