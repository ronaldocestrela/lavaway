using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Domain;

public sealed class ChatbotConversationSession : IMustHaveTenant
{
    private ChatbotConversationSession()
    {
    }

    private ChatbotConversationSession(
        Guid id,
        Guid tenantId,
        string customerPhone,
        string? customerName)
    {
        Id = id;
        TenantId = tenantId;
        CustomerPhone = customerPhone;
        CustomerName = customerName;
        CurrentStep = ChatbotStep.Greeting;
        LastInteractionAtUtc = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string CustomerPhone { get; private set; } = string.Empty;
    public string? CustomerName { get; private set; }
    public ChatbotStep CurrentStep { get; private set; }
    public Guid? SelectedServiceId { get; private set; }
    public string? SelectedServiceName { get; private set; }
    public string? SelectedVehicleSize { get; private set; }
    public decimal? SelectedPrice { get; private set; }
    public DateOnly? SelectedDate { get; private set; }
    public TimeOnly? SelectedTime { get; private set; }
    public string? VehiclePlate { get; private set; }
    public Guid? TargetBookingId { get; private set; }
    public DateTimeOffset LastInteractionAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<ChatbotConversationSession> Start(
        Guid tenantId,
        string customerPhone,
        string? customerName)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<ChatbotConversationSession>.Failure(new Error("session.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var normalizedPhone = NormalizePhone(customerPhone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return Result<ChatbotConversationSession>.Failure(new Error("session.phone.required", "Valid customer phone is required.", ErrorType.Validation));
        }

        return Result<ChatbotConversationSession>.Success(new ChatbotConversationSession(
            Guid.CreateVersion7(),
            tenantId,
            normalizedPhone,
            string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim()));
    }

    public void SetCustomerName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            CustomerName = name.Trim();
            Touch();
        }
    }

    public void MoveToStep(ChatbotStep nextStep)
    {
        CurrentStep = nextStep;
        Touch();
    }

    public void SelectService(Guid serviceId, string serviceName)
    {
        SelectedServiceId = serviceId;
        SelectedServiceName = serviceName.Trim();
        Touch();
    }

    public void SelectVehicleSize(string size, decimal price)
    {
        SelectedVehicleSize = size.Trim();
        SelectedPrice = price;
        Touch();
    }

    public void SelectDate(DateOnly date)
    {
        SelectedDate = date;
        Touch();
    }

    public void SelectTime(TimeOnly time)
    {
        SelectedTime = time;
        Touch();
    }

    public void SetVehiclePlate(string plate)
    {
        VehiclePlate = plate.Trim().ToUpperInvariant();
        Touch();
    }

    public void SetTargetBooking(Guid bookingId)
    {
        TargetBookingId = bookingId;
        Touch();
    }

    public void Complete()
    {
        CurrentStep = ChatbotStep.Completed;
        IsActive = false;
        Touch();
    }

    public void Reset()
    {
        CurrentStep = ChatbotStep.Menu;
        SelectedServiceId = null;
        SelectedServiceName = null;
        SelectedVehicleSize = null;
        SelectedPrice = null;
        SelectedDate = null;
        SelectedTime = null;
        VehiclePlate = null;
        TargetBookingId = null;
        IsActive = true;
        Touch();
    }

    public bool IsExpired(TimeSpan timeout)
    {
        return DateTimeOffset.UtcNow - LastInteractionAtUtc > timeout;
    }

    public void Touch()
    {
        LastInteractionAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
