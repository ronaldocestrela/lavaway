using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class BookingApplicationService(
    IBookingRepository bookingRepository,
    IServiceRepository serviceRepository,
    IYardCapacityRepository capacityRepository,
    ICustomerVehicleSearchRepository customerSearchRepository,
    IUnitOfWork unitOfWork) : ISchedulingBookingLookup
{
    private readonly BookingCapacityChecker _capacityChecker = new(capacityRepository, bookingRepository);

    public async Task<Result<IReadOnlyList<ServiceDto>>> GetAvailableServicesCatalogAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<ServiceDto>>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var services = await serviceRepository.ListByTenantAsync(tenantId, ct);
        var dtos = services.Select(s => new ServiceDto(
            s.Id,
            s.Name,
            s.Category,
            s.Prices.Select(p => new ServicePriceDto(
                p.VehicleSize.ToString(),
                p.Amount,
                p.EstimatedDurationMinutes)).ToArray())).ToArray();

        return Result<IReadOnlyList<ServiceDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<AvailableTimeSlotDto>>> GetAvailableTimeSlotsAsync(
        Guid tenantId,
        DateOnly date,
        Guid serviceId,
        string vehicleSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<AvailableTimeSlotDto>>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var service = await serviceRepository.GetByIdAsync(tenantId, serviceId, ct);
        if (service is null)
        {
            return Result<IReadOnlyList<AvailableTimeSlotDto>>.Failure(new Error("booking.service.not_found", "Service not found.", ErrorType.NotFound));
        }

        var parsedSize = ParseVehicleSize(vehicleSize);
        var price = service.Prices.FirstOrDefault(p => p.VehicleSize == parsedSize);
        var duration = price?.EstimatedDurationMinutes ?? 60;

        var slots = await _capacityChecker.GetAvailableSlotsAsync(tenantId, date, duration, ct);
        return Result<IReadOnlyList<AvailableTimeSlotDto>>.Success(slots);
    }

    public async Task<Result<BookingConfirmationDto>> CreateBookingFromChatbotAsync(
        Guid tenantId,
        CreateBookingFromChatbotRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var service = await serviceRepository.GetByIdAsync(tenantId, request.ServiceId, ct);
        if (service is null)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.service.not_found", "Selected service was not found.", ErrorType.NotFound));
        }

        var parsedSize = ParseVehicleSize(request.VehicleSize);
        var price = service.Prices.FirstOrDefault(p => p.VehicleSize == parsedSize);
        if (price is null)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.price.not_configured", "Price not configured for this vehicle size.", ErrorType.Validation));
        }

        var hasCapacity = await _capacityChecker.HasCapacityAsync(
            tenantId,
            request.ScheduledDate,
            request.ScheduledTime,
            price.EstimatedDurationMinutes,
            ct);

        if (!hasCapacity)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.capacity.exceeded", "No boxes available for the selected time slot.", ErrorType.Conflict));
        }

        var customerMatches = await customerSearchRepository.SearchAsync(tenantId, null, request.CustomerPhone, 1, ct);
        var existingCustomer = customerMatches.FirstOrDefault();

        var bookingResult = Booking.Create(
            tenantId,
            string.IsNullOrWhiteSpace(request.CustomerName) ? existingCustomer?.CustomerName ?? "Cliente WhatsApp" : request.CustomerName,
            request.CustomerPhone,
            request.VehiclePlate,
            request.VehicleModel,
            parsedSize,
            service.Id,
            service.Name,
            price.Amount,
            request.ScheduledDate,
            request.ScheduledTime,
            price.EstimatedDurationMinutes,
            BookingOrigin.WhatsAppBot,
            existingCustomer?.CustomerId);

        if (!bookingResult.IsSuccess)
        {
            return Result<BookingConfirmationDto>.Failure(bookingResult.Error!);
        }

        var booking = bookingResult.Value!;
        booking.Confirm();

        await bookingRepository.AddAsync(booking, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<BookingConfirmationDto>.Failure(saveResult.Error!);
        }

        var dto = new BookingConfirmationDto(
            booking.Id,
            booking.Protocol,
            booking.CustomerName,
            booking.ServiceName,
            booking.ScheduledDate,
            booking.ScheduledTime,
            booking.EstimatedPrice);

        return Result<BookingConfirmationDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<BookingSummaryDto>>> GetCustomerActiveBookingsAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<BookingSummaryDto>>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var bookings = await bookingRepository.GetActiveBookingsByPhoneAsync(tenantId, customerPhone, ct);
        var dtos = bookings.Select(MapToSummaryDto).ToArray();
        return Result<IReadOnlyList<BookingSummaryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<BookingSummaryDto>>> GetBookingsAsync(
        Guid tenantId,
        DateOnly? date = null,
        string? status = null,
        string? search = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<BookingSummaryDto>>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var bookings = await bookingRepository.GetByDateAsync(tenantId, targetDate, ct);

        var query = bookings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status.ToString().Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b =>
                b.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.VehiclePlate.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Protocol.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var dtos = query.OrderBy(b => b.ScheduledTime).Select(MapToSummaryDto).ToArray();
        return Result<IReadOnlyList<BookingSummaryDto>>.Success(dtos);
    }

    public async Task<Result<BookingSummaryDto>> GetBookingByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result<BookingSummaryDto>.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        return Result<BookingSummaryDto>.Success(MapToSummaryDto(booking));
    }

    public async Task<Result<BookingConfirmationDto>> CreateManualBookingAsync(
        Guid tenantId,
        CreateManualBookingRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        var service = await serviceRepository.GetByIdAsync(tenantId, request.ServiceId, ct);
        if (service is null)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.service.not_found", "Service was not found.", ErrorType.NotFound));
        }

        var parsedSize = ParseVehicleSize(request.VehicleSize);
        var price = service.Prices.FirstOrDefault(p => p.VehicleSize == parsedSize);
        if (price is null)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.price.not_configured", "Price not configured for this vehicle size.", ErrorType.Validation));
        }

        var hasCapacity = await _capacityChecker.HasCapacityAsync(
            tenantId,
            request.ScheduledDate,
            request.ScheduledTime,
            price.EstimatedDurationMinutes,
            ct);

        if (!hasCapacity)
        {
            return Result<BookingConfirmationDto>.Failure(new Error("booking.capacity.exceeded", "No boxes available for the selected time slot.", ErrorType.Conflict));
        }

        var customerMatches = await customerSearchRepository.SearchAsync(tenantId, null, request.CustomerPhone, 1, ct);
        var existingCustomer = customerMatches.FirstOrDefault();

        var bookingResult = Booking.Create(
            tenantId,
            request.CustomerName,
            request.CustomerPhone,
            request.VehiclePlate,
            request.VehicleModel,
            parsedSize,
            service.Id,
            service.Name,
            price.Amount,
            request.ScheduledDate,
            request.ScheduledTime,
            price.EstimatedDurationMinutes,
            BookingOrigin.WebReception,
            existingCustomer?.CustomerId,
            request.Notes);

        if (!bookingResult.IsSuccess)
        {
            return Result<BookingConfirmationDto>.Failure(bookingResult.Error!);
        }

        var booking = bookingResult.Value!;
        booking.Confirm();

        await bookingRepository.AddAsync(booking, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<BookingConfirmationDto>.Failure(saveResult.Error!);
        }

        var dto = new BookingConfirmationDto(
            booking.Id,
            booking.Protocol,
            booking.CustomerName,
            booking.ServiceName,
            booking.ScheduledDate,
            booking.ScheduledTime,
            booking.EstimatedPrice);

        return Result<BookingConfirmationDto>.Success(dto);
    }

    public async Task<Result<BookingSummaryDto>> ConfirmBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result<BookingSummaryDto>.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        var result = booking.Confirm();
        if (!result.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(result.Error!);
        }

        bookingRepository.Update(booking);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(saveResult.Error!);
        }

        return Result<BookingSummaryDto>.Success(MapToSummaryDto(booking));
    }

    public async Task<Result<BookingSummaryDto>> CancelBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result<BookingSummaryDto>.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        var result = booking.Cancel(reason);
        if (!result.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(result.Error!);
        }

        bookingRepository.Update(booking);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(saveResult.Error!);
        }

        return Result<BookingSummaryDto>.Success(MapToSummaryDto(booking));
    }

    public async Task<Result<BookingSummaryDto>> RescheduleBookingAsync(
        Guid tenantId,
        Guid bookingId,
        DateOnly newDate,
        TimeOnly newTime,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result<BookingSummaryDto>.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        var hasCapacity = await _capacityChecker.HasCapacityAsync(
            tenantId,
            newDate,
            newTime,
            booking.EstimatedDurationMinutes,
            ct);

        if (!hasCapacity)
        {
            return Result<BookingSummaryDto>.Failure(new Error("booking.capacity.exceeded", "No boxes available for the selected time slot.", ErrorType.Conflict));
        }

        var result = booking.Reschedule(newDate, newTime);
        if (!result.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(result.Error!);
        }

        bookingRepository.Update(booking);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<BookingSummaryDto>.Failure(saveResult.Error!);
        }

        return Result<BookingSummaryDto>.Success(MapToSummaryDto(booking));
    }

    public async Task<Result<BookingSummaryDto?>> GetUpcomingBookingForCustomerAsync(
        Guid tenantId,
        string customerPhone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(customerPhone))
        {
            return Result<BookingSummaryDto?>.Failure(new Error("booking.invalid_input", "Tenant and customer phone are required.", ErrorType.Validation));
        }

        var booking = await bookingRepository.GetUpcomingActiveBookingByPhoneAsync(tenantId, customerPhone, ct);
        return Result<BookingSummaryDto?>.Success(booking is null ? null : MapToSummaryDto(booking));
    }

    public async Task<Result> MarkBookingArrivedAsync(
        Guid tenantId,
        Guid bookingId,
        Guid workOrderId,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        var result = booking.MarkAsArrived(workOrderId);
        if (!result.IsSuccess)
        {
            return Result.Failure(result.Error!);
        }

        bookingRepository.Update(booking);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        return saveResult.IsSuccess ? Result.Success() : Result.Failure(saveResult.Error!);
    }

    private static VehicleSize ParseVehicleSize(string? size) => size?.Trim() switch
    {
        VehicleSizeConstants.Suv or "Suv" => VehicleSize.Suv,
        VehicleSizeConstants.PickupVan or "PickupVan" => VehicleSize.PickupVan,
        VehicleSizeConstants.Motorcycle or "Motorcycle" => VehicleSize.Motorcycle,
        _ => VehicleSize.HatchSedan
    };

    private static BookingSummaryDto MapToSummaryDto(Booking b) => new(
        b.Id,
        b.TenantId,
        b.Protocol,
        b.CustomerId,
        b.CustomerName,
        b.CustomerPhone,
        b.VehiclePlate,
        b.VehicleModel,
        b.VehicleSize.ToString(),
        b.ServiceId,
        b.ServiceName,
        b.EstimatedPrice,
        b.ScheduledDate,
        b.ScheduledTime,
        b.EstimatedDurationMinutes,
        b.Status.ToString(),
        b.Origin.ToString(),
        b.WorkOrderId,
        b.Notes,
        b.CreatedAt,
        b.Reminder24hSentAt,
        b.Reminder2hSentAt,
        b.ConfirmedAtUtc);
}
