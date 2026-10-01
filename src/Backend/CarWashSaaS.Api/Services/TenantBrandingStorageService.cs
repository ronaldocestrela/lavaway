using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Http;

namespace CarWashSaaS.Api.Services;

public sealed class TenantBrandingStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".svg"
    };

    private readonly string _storageRootPath;

    public TenantBrandingStorageService(string? storageRootPath = null)
    {
        _storageRootPath = storageRootPath ?? Path.Combine(AppContext.BaseDirectory, "Storage");
        Directory.CreateDirectory(_storageRootPath);
    }

    public Result<string> SaveLogo(Guid tenantId, IFormFile file)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<string>.Failure(new Error("store_profile.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (file is null)
        {
            return Result<string>.Failure(new Error("store_profile.logo.required", "A logo file is required.", ErrorType.Validation));
        }

        if (file.Length <= 0)
        {
            return Result<string>.Failure(new Error("store_profile.logo.empty", "The uploaded logo is empty.", ErrorType.Validation));
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            return Result<string>.Failure(new Error("store_profile.logo.too_large", "The logo must be 2 MB or smaller.", ErrorType.Validation));
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Result<string>.Failure(new Error("store_profile.logo.extension.invalid", "Logo must be a PNG, JPG, JPEG, WEBP or SVG file.", ErrorType.Validation));
        }

        var safeFileName = $"{Guid.CreateVersion7():N}{extension}";
        var relativePath = TenantStoragePathBuilder.BuildPath(tenantId, "branding", safeFileName);
        var physicalPath = Path.Combine(_storageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(physicalPath);
        file.CopyTo(stream);

        return Result<string>.Success($"/storage/{relativePath.Replace('\\', '/')}" );
    }
}
