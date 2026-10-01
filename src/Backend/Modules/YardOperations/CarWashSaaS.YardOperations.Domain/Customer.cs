using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed class Customer : IMustHaveTenant
{
    private Customer()
    {
    }

    private Customer(Guid id, Guid tenantId, string name, string phone, string normalizedPhone)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Phone = phone;
        NormalizedPhone = normalizedPhone;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string NormalizedPhone { get; private set; } = string.Empty;

    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty)
            .Where(char.IsAsciiDigit)
            .ToArray());

        return digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 12 or 13
            ? digits[2..]
            : digits;
    }

    public static Result<Customer> Create(Guid tenantId, string name, string phone)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Customer>.Failure(new Error("customer.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Result<Customer>.Failure(new Error("customer.name.invalid", "A customer name of up to 200 characters is required.", ErrorType.Validation));
        }

        var normalizedPhone = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 32 || normalizedPhone.Length == 0)
        {
            return Result<Customer>.Failure(new Error("customer.phone.invalid", "A customer phone number of up to 32 characters is required.", ErrorType.Validation));
        }

        return Result<Customer>.Success(new Customer(Guid.CreateVersion7(), tenantId, name.Trim(), phone.Trim(), normalizedPhone));
    }
}