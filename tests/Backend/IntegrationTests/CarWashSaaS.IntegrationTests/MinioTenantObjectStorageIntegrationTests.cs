using CarWashSaaS.Api.Services;
using Minio;
using Testcontainers.Minio;

namespace CarWashSaaS.IntegrationTests;

[Collection(MinioStorageFixture.CollectionName)]
public sealed class MinioTenantObjectStorageIntegrationTests(MinioStorageFixture fixture)
{
    [Fact]
    public async Task PutAndGetAsync_ShouldStoreAndRetrieveObject_WithCorrectContentType()
    {
        var bucketName = $"test-bucket-{Guid.NewGuid():N}";
        var storage = fixture.CreateStorage(bucketName);
        var tenantId = Guid.NewGuid();
        var fileName = "avatar.png";
        var fileBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(fileBytes);

        await storage.PutAsync(tenantId, "avatars", fileName, stream, "image/png");

        var stored = await storage.GetAsync(tenantId, "avatars", fileName);

        Assert.NotNull(stored);
        Assert.Equal("image/png", stored!.ContentType);
        using var resultStream = new MemoryStream();
        await stored.Content.CopyToAsync(resultStream);
        Assert.Equal(fileBytes, resultStream.ToArray());
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenObjectDoesNotExist()
    {
        var bucketName = $"test-bucket-{Guid.NewGuid():N}";
        var storage = fixture.CreateStorage(bucketName);
        var tenantId = Guid.NewGuid();

        var stored = await storage.GetAsync(tenantId, "docs", "non-existent.pdf");

        Assert.Null(stored);
    }

    [Fact]
    public async Task Storage_ShouldEnforceStrictTenantIsolation()
    {
        var bucketName = $"test-bucket-{Guid.NewGuid():N}";
        var storage = fixture.CreateStorage(bucketName);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var fileName = "contract.pdf";
        var tenantABytes = new byte[] { 1, 2, 3, 4, 5 };
        using var streamA = new MemoryStream(tenantABytes);

        // Tenant A stores a private file
        await storage.PutAsync(tenantA, "contracts", fileName, streamA, "application/pdf");

        // Tenant B attempts to read Tenant A's file with the same category and fileName
        var tenantBStored = await storage.GetAsync(tenantB, "contracts", fileName);
        Assert.Null(tenantBStored);

        // Tenant A can successfully read their own file
        var tenantAStored = await storage.GetAsync(tenantA, "contracts", fileName);
        Assert.NotNull(tenantAStored);
        using var tenantAResult = new MemoryStream();
        await tenantAStored!.Content.CopyToAsync(tenantAResult);
        Assert.Equal(tenantABytes, tenantAResult.ToArray());
    }

    [Fact]
    public async Task TenantBrandingStorageService_WithMinio_ShouldIsolateLogos_BetweenTenants()
    {
        var bucketName = $"test-branding-{Guid.NewGuid():N}";
        var storage = fixture.CreateStorage(bucketName);
        var brandingService = new CarWashSaaS.Tenants.Application.TenantBrandingStorageService(storage);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var logoBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var streamA = new MemoryStream(logoBytes);

        var saveResult = await brandingService.SaveLogoAsync(tenantA, "logo.png", logoBytes.Length, streamA);
        Assert.True(saveResult.IsSuccess);
        Assert.StartsWith("/tenants/profile/logo/", saveResult.Value);
        var safeFileName = Path.GetFileName(saveResult.Value!);

        // Tenant B cannot access Tenant A's logo
        var tenantBLogo = await brandingService.GetLogoAsync(tenantB, safeFileName);
        Assert.Null(tenantBLogo);

        // Tenant A can access their logo
        var tenantALogo = await brandingService.GetLogoAsync(tenantA, safeFileName);
        Assert.NotNull(tenantALogo);
        Assert.Equal("image/png", tenantALogo!.ContentType);
        using var readStream = new MemoryStream();
        await tenantALogo.Content.CopyToAsync(readStream);
        Assert.Equal(logoBytes, readStream.ToArray());
    }
}

public sealed class MinioStorageFixture : IAsyncLifetime
{
    public const string CollectionName = "MinIO tenant object storage";

    private readonly MinioContainer _container = new MinioBuilder("cgr.dev/chainguard/minio:latest")
        .WithUsername("lavaway_minio")
        .WithPassword("Lavaway_Minio_Pass_123!")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public MinioTenantObjectStorage CreateStorage(string bucketName)
    {
        var endpoint = _container.GetConnectionString().Replace("http://", "").Replace("https://", "").TrimEnd('/');
        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(_container.GetAccessKey(), _container.GetSecretKey())
            .WithSSL(false)
            .Build();

        return new MinioTenantObjectStorage(client, bucketName);
    }
}

[CollectionDefinition(MinioStorageFixture.CollectionName)]
public sealed class MinioStorageCollection : ICollectionFixture<MinioStorageFixture>;
