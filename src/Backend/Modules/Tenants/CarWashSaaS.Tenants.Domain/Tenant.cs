using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Tenants.Domain;

public sealed class Tenant
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<Tenant> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Tenant>.Failure(new Error("tenant.name.required", "Tenant name is required.", ErrorType.Validation));
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > 200)
        {
            return Result<Tenant>.Failure(new Error("tenant.name.too_long", "Tenant name cannot exceed 200 characters.", ErrorType.Validation));
        }

        return Result<Tenant>.Success(new Tenant(Guid.CreateVersion7(), normalizedName));
    }
}
