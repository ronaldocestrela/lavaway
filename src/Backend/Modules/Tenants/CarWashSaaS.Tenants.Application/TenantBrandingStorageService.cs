using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Tenants.Application;

public sealed class TenantBrandingStorageService(ITenantObjectStorage objectStorage)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".svg"
    };

    public async Task<Result<string>> SaveLogoAsync(
        Guid tenantId,
        string fileName,
        long fileLength,
        Stream content,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<string>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(fileName) || content is null || fileLength <= 0)
        {
            return Result<string>.Failure(new Error("store_profile.logo.empty", "The logo file is required and cannot be empty.", ErrorType.Validation));
        }

        if (fileLength > 2 * 1024 * 1024)
        {
            return Result<string>.Failure(new Error("store_profile.logo.too_large", "The logo must be 2 MB or smaller.", ErrorType.Validation));
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Result<string>.Failure(new Error("store_profile.logo.extension.invalid", "Logo must be a PNG, JPG, JPEG, WEBP or SVG file.", ErrorType.Validation));
        }

        var safeFileName = $"{Guid.CreateVersion7():N}{extension.ToLowerInvariant()}";
        await objectStorage.PutAsync(tenantId, "branding", safeFileName, content, ContentTypeFor(extension), ct);
        return Result<string>.Success($"/tenants/profile/logo/{safeFileName}");
    }

    public Task<StoredObject?> GetLogoAsync(Guid tenantId, string fileName, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName.Replace('\\', '/')), StringComparison.Ordinal) ||
            !AllowedExtensions.Contains(Path.GetExtension(fileName)))
        {
            return Task.FromResult<StoredObject?>(null);
        }

        return objectStorage.GetAsync(tenantId, "branding", fileName, ct);
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}