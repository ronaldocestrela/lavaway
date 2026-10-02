using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Shared.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CarWashSaaS.Api.Services;

public sealed class MinioTenantObjectStorage(IMinioClient client, string bucketName) : ITenantObjectStorage
{
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public async Task PutAsync(Guid tenantId, string category, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var objectKey = TenantStoragePathBuilder.BuildPath(tenantId, category, fileName);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType), ct);
    }

    public async Task<StoredObject?> GetAsync(Guid tenantId, string category, string fileName, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var objectKey = TenantStoragePathBuilder.BuildPath(tenantId, category, fileName);

        var content = new MemoryStream();
        try
        {
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(content)), ct);
        }
        catch (ObjectNotFoundException)
        {
            content.Dispose();
            return null;
        }

        content.Position = 0;
        return new StoredObject(content, ContentTypeFor(objectKey));
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);
            if (!exists)
            {
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private static string ContentTypeFor(string objectKey) => Path.GetExtension(objectKey).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}