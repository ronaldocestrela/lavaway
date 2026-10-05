using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.YardOperations.Application;

public sealed class BookingReminderApplicationService(
    IBookingRepository bookingRepository,
    IOutboundWhatsAppDispatcher messageDispatcher,
    ITenantStoreProfileLookup storeProfileLookup,
    IUnitOfWork unitOfWork,
    ILogger<BookingReminderApplicationService> logger)
{
    public async Task<Result> ExecuteReminderScanForTenantAsync(
        Guid tenantId,
        DateTimeOffset? referenceTime = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure(new Error("reminder.tenant_required", "Tenant is required.", ErrorType.Validation));
        }

        var refTime = referenceTime ?? DateTimeOffset.UtcNow;
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;
        var storeName = storeProfile?.TradeName ?? "Lavaway";
        var address = storeProfile is not null
            ? $"{storeProfile.Street}, {storeProfile.City}"
            : "Consulte nosso endereço no perfil";

        var countDispatched = 0;

        // 1. Lembretes 24h
        var pending24h = await bookingRepository.GetBookingsPending24hReminderAsync(tenantId, refTime, ct);
        foreach (var booking in pending24h)
        {
            var text = Build24hReminderMessage(booking, storeName, address);
            var idempotencyKey = $"reminder-24h-{booking.Id:N}";

            var dispatchResult = await messageDispatcher.DispatchTextMessageAsync(
                tenantId,
                booking.CustomerPhone,
                text,
                idempotencyKey,
                ct);

            if (dispatchResult.IsSuccess)
            {
                booking.MarkReminder24hSent(refTime);
                bookingRepository.Update(booking);
                countDispatched++;
            }
            else
            {
                logger.LogWarning("Failed to dispatch 24h reminder for booking {BookingId}: {Error}",
                    booking.Id, dispatchResult.Error?.Description);
            }
        }

        // 2. Lembretes 2h
        var pending2h = await bookingRepository.GetBookingsPending2hReminderAsync(tenantId, refTime, ct);
        foreach (var booking in pending2h)
        {
            var text = Build2hReminderMessage(booking, storeName, address);
            var idempotencyKey = $"reminder-2h-{booking.Id:N}";

            var dispatchResult = await messageDispatcher.DispatchTextMessageAsync(
                tenantId,
                booking.CustomerPhone,
                text,
                idempotencyKey,
                ct);

            if (dispatchResult.IsSuccess)
            {
                booking.MarkReminder2hSent(refTime);
                bookingRepository.Update(booking);
                countDispatched++;
            }
            else
            {
                logger.LogWarning("Failed to dispatch 2h reminder for booking {BookingId}: {Error}",
                    booking.Id, dispatchResult.Error?.Description);
            }
        }

        if (countDispatched > 0)
        {
            var saveResult = await unitOfWork.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
            {
                return Result.Failure(saveResult.Error!);
            }
        }

        return Result.Success();
    }

    public async Task<Result> SendManualReminderAsync(
        Guid tenantId,
        Guid bookingId,
        string reminderType,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || bookingId == Guid.Empty)
        {
            return Result.Failure(new Error("reminder.invalid_input", "Tenant and booking ID are required.", ErrorType.Validation));
        }

        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result.Failure(new Error("booking.not_found", "Booking not found.", ErrorType.NotFound));
        }

        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeProfile = profileResult.IsSuccess ? profileResult.Value : null;
        var storeName = storeProfile?.TradeName ?? "Lavaway";
        var address = storeProfile is not null
            ? $"{storeProfile.Street}, {storeProfile.City}"
            : "Consulte nosso endereço no perfil";

        var is24h = reminderType.Trim().Equals("24h", StringComparison.OrdinalIgnoreCase);
        var text = is24h
            ? Build24hReminderMessage(booking, storeName, address)
            : Build2hReminderMessage(booking, storeName, address);

        var idempotencyKey = $"manual-reminder-{reminderType.Trim().ToLowerInvariant()}-{booking.Id:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        var dispatchResult = await messageDispatcher.DispatchTextMessageAsync(
            tenantId,
            booking.CustomerPhone,
            text,
            idempotencyKey,
            ct);

        if (!dispatchResult.IsSuccess)
        {
            return Result.Failure(dispatchResult.Error!);
        }

        if (is24h)
        {
            booking.MarkReminder24hSent();
        }
        else
        {
            booking.MarkReminder2hSent();
        }

        bookingRepository.Update(booking);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        return saveResult.IsSuccess ? Result.Success() : Result.Failure(saveResult.Error!);
    }

    private static string Build24hReminderMessage(Booking booking, string storeName, string address)
    {
        return $"🔔 *Lembrete de Agendamento - {storeName}* 🚗✨\n\n" +
               $"Olá, *{booking.CustomerName}*! Lembramos que você tem um agendamento amanhã:\n" +
               $"• *Serviço:* {booking.ServiceName}\n" +
               $"• *Data e Horário:* {booking.ScheduledDate:dd/MM/yyyy} às {booking.ScheduledTime:HH\\:mm}\n" +
               $"• *Veículo:* {booking.VehiclePlate}\n" +
               $"• *Local:* {address}\n\n" +
               "Por favor, responda com o *número da opção desejada*:\n" +
               "1️⃣ - *Confirmar presença*\n" +
               "2️⃣ - *Remarcar horário*\n" +
               "3️⃣ - *Cancelar agendamento*";
    }

    private static string Build2hReminderMessage(Booking booking, string storeName, string address)
    {
        return $"⏳ *Seu horário está chegando! - {storeName}* 🚗✨\n\n" +
               $"Olá, *{booking.CustomerName}*! Seu atendimento está agendado para hoje às *{booking.ScheduledTime:HH\\:mm}* " +
               $"({booking.ServiceName} - Placa: {booking.VehiclePlate}).\n" +
               $"Local: {address}\n\n" +
               "Ainda podemos te aguardar? Responda:\n" +
               "1️⃣ - *Confirmar presença*\n" +
               "2️⃣ - *Remarcar horário*\n" +
               "3️⃣ - *Cancelar agendamento*";
    }
}
