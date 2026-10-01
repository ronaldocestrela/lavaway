using System.Text.RegularExpressions;

namespace CarWashSaaS.Shared.Configuration;

public static partial class TenantStoragePathBuilder
{
    public static string BuildPath(Guid tenantId, string category, string fileName)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? "general"
            : SanitizeSegment(category.Trim().Replace('\\', '/').Trim('/'));

        var normalizedFileName = string.IsNullOrWhiteSpace(fileName)
            ? "file"
            : Path.GetFileName(fileName.Trim().Replace('\\', '/'));

        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            throw new ArgumentException("A valid file name is required.", nameof(fileName));
        }

        var categoryPath = string.IsNullOrWhiteSpace(normalizedCategory)
            ? "general"
            : normalizedCategory;

        return $"tenants/{tenantId}/{categoryPath}/{normalizedFileName}";
    }

    private static string SanitizeSegment(string value)
    {
        var cleaned = Regex.Replace(value, @"[^a-zA-Z0-9._-]", "", RegexOptions.CultureInvariant);
        return string.IsNullOrWhiteSpace(cleaned) ? "general" : cleaned;
    }
}
