using System.Text.RegularExpressions;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Domain;

public sealed partial class Booking : IMustHaveTenant
{
    private Booking()
    {
    }

    private Booking(
        Guid id,
        Guid tenantId,
        string protocol,
        Guid? customerId,
        string customerName,
        string customerPhone,
        string vehiclePlate,
        string? vehicleModel,
        VehicleSize vehicleSize,
        Guid serviceId,
        string serviceName,
        decimal estimatedPrice,
        DateOnly scheduledDate,
        TimeOnly scheduledTime,
        int estimatedDurationMinutes,
        BookingStatus status,
        BookingOrigin origin,
        string? notes)
    {
        Id = id;
        TenantId = tenantId;
        Protocol = protocol;
        CustomerId = customerId;
        CustomerName = customerName;
        CustomerPhone = customerPhone;
        VehiclePlate = vehiclePlate;
        VehicleModel = vehicleModel;
        VehicleSize = vehicleSize;
        ServiceId = serviceId;
        ServiceName = serviceName;
        EstimatedPrice = estimatedPrice;
        ScheduledDate = scheduledDate;
        ScheduledTime = scheduledTime;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        Status = status;
        Origin = origin;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    Guid IMustHaveTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    public string Protocol { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerPhone { get; private set; } = string.Empty;
    public string VehiclePlate { get; private set; } = string.Empty;
    public string? VehicleModel { get; private set; }
    public VehicleSize VehicleSize { get; private set; }
    public Guid ServiceId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public decimal EstimatedPrice { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public TimeOnly ScheduledTime { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingOrigin Origin { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public string? Notes { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<Booking> Create(
        Guid tenantId,
        string customerName,
        string customerPhone,
        string vehiclePlate,
        string? vehicleModel,
        VehicleSize vehicleSize,
        Guid serviceId,
        string serviceName,
        decimal estimatedPrice,
        DateOnly scheduledDate,
        TimeOnly scheduledTime,
        int estimatedDurationMinutes = 60,
        BookingOrigin origin = BookingOrigin.WhatsAppBot,
        Guid? customerId = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<Booking>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(customerName) || customerName.Trim().Length > 200)
        {
            return Result<Booking>.Failure(new Error("booking.customer_name.invalid", "Customer name of up to 200 characters is required.", ErrorType.Validation));
        }

        var normalizedPhone = NormalizePhone(customerPhone);
        if (string.IsNullOrWhiteSpace(normalizedPhone) || normalizedPhone.Length < 8 || normalizedPhone.Length > 20)
        {
            return Result<Booking>.Failure(new Error("booking.customer_phone.invalid", "A valid phone number is required.", ErrorType.Validation));
        }

        var normalizedPlate = NormalizePlate(vehiclePlate);
        if (string.IsNullOrWhiteSpace(normalizedPlate) || normalizedPlate.Length < 7 || normalizedPlate.Length > 10)
        {
            return Result<Booking>.Failure(new Error("booking.plate.invalid", "A valid vehicle plate (7 to 10 alphanumeric characters) is required.", ErrorType.Validation));
        }

        if (serviceId == Guid.Empty || string.IsNullOrWhiteSpace(serviceName))
        {
            return Result<Booking>.Failure(new Error("booking.service.required", "Service is required.", ErrorType.Validation));
        }

        if (estimatedPrice < 0)
        {
            return Result<Booking>.Failure(new Error("booking.price.invalid", "Estimated price cannot be negative.", ErrorType.Validation));
        }

        var duration = estimatedDurationMinutes <= 0 ? 60 : estimatedDurationMinutes;
        var id = Guid.CreateVersion7();
        var protocol = $"BK-{DateTime.UtcNow:yyMM}-{id.ToString("N")[..6].ToUpperInvariant()}";

        return Result<Booking>.Success(new Booking(
            id,
            tenantId,
            protocol,
            customerId,
            customerName.Trim(),
            normalizedPhone,
            normalizedPlate,
            string.IsNullOrWhiteSpace(vehicleModel) ? null : vehicleModel.Trim(),
            vehicleSize,
            serviceId,
            serviceName.Trim(),
            estimatedPrice,
            scheduledDate,
            scheduledTime,
            duration,
            BookingStatus.Scheduled,
            origin,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()));
    }

    public Result<Booking> Confirm()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Result<Booking>.Failure(new Error("booking.cancelled", "Cancelled bookings cannot be confirmed.", ErrorType.Conflict));
        }

        Status = BookingStatus.Confirmed;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<Booking>.Success(this);
    }

    public Result<Booking> MarkAsArrived(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
        {
            return Result<Booking>.Failure(new Error("booking.work_order.required", "Work order is required to mark as arrived.", ErrorType.Validation));
        }

        Status = BookingStatus.Arrived;
        WorkOrderId = workOrderId;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<Booking>.Success(this);
    }

    public Result<Booking> MarkAsCompleted()
    {
        Status = BookingStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<Booking>.Success(this);
    }

    public Result<Booking> Cancel(string? reason)
    {
        Status = BookingStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelado" : reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<Booking>.Success(this);
    }

    public Result<Booking> MarkAsNoShow()
    {
        Status = BookingStatus.NoShow;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result<Booking>.Success(this);
    }

    public void LinkCustomer(Guid customerId)
    {
        CustomerId = customerId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static string NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return string.Empty;
        return new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
