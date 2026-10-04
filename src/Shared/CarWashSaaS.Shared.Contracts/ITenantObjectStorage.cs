namespace CarWashSaaS.Shared.Contracts;

public interface ITenantObjectStorage
{
    Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default);

    Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default);
}

public sealed record StoredObject(Stream Content, string ContentType);
