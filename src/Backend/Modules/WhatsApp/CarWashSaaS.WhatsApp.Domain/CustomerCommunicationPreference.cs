using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class CustomerCommunicationPreference : IMustHaveTenant
{
    private CustomerCommunicationPreference()
    {
    }

    private CustomerCommunicationPreference(Guid id, Guid tenantId, string normalizedPhone, bool isOptedIn, string? reason)
    {
        Id = id;
        TenantId = tenantId;
        NormalizedPhone = normalizedPhone;
        IsOptedIn = isOptedIn;
        Reason = reason;
        OptedOutAtUtc = isOptedIn ? null : DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string NormalizedPhone { get; private set; } = string.Empty;
    public bool IsOptedIn { get; private set; }
    public DateTimeOffset? OptedOutAtUtc { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<CustomerCommunicationPreference> Create(Guid tenantId, string phone, bool isOptedIn = true, string? reason = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerCommunicationPreference>.Failure(new Error("whatsapp.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var cleaned = OutboundWhatsAppMessage.CleanPhoneNumber(phone);
        if (cleaned.Length < 10)
        {
            return Result<CustomerCommunicationPreference>.Failure(new Error("whatsapp.phone.invalid", "A valid phone number is required.", ErrorType.Validation));
        }

        return Result<CustomerCommunicationPreference>.Success(new CustomerCommunicationPreference(
            Guid.CreateVersion7(),
            tenantId,
            cleaned,
            isOptedIn,
            reason));
    }

    public Result<CustomerCommunicationPreference> OptOut(string reason)
    {
        IsOptedIn = false;
        Reason = reason;
        OptedOutAtUtc = DateTimeOffset.UtcNow;
        UpdatedAt = OptedOutAtUtc.Value;
        return Result<CustomerCommunicationPreference>.Success(this);
    }

    public Result<CustomerCommunicationPreference> OptIn()
    {
        IsOptedIn = true;
        Reason = "Client opted-in";
        OptedOutAtUtc = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<CustomerCommunicationPreference>.Success(this);
    }
}
