using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class SchedulingApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<BookingSummaryDto>> GetBookingsAsync(
        DateOnly? date = null,
        string? status = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var targetDate = (date ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd");
        var uri = $"scheduling/bookings?date={targetDate}";

        if (!string.IsNullOrWhiteSpace(status))
        {
            uri += $"&status={Uri.EscapeDataString(status.Trim())}";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            uri += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        using var response = await httpClient.GetAsync(uri, ct);
        return await ReadResponseAsync<IReadOnlyList<BookingSummaryDto>>(response, ct);
    }

    public async Task<BookingSummaryDto?> GetBookingByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"scheduling/bookings/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadResponseAsync<BookingSummaryDto>(response, ct);
    }

    public async Task<BookingConfirmationDto> CreateBookingAsync(CreateManualBookingRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("scheduling/bookings", request, ct);
        return await ReadResponseAsync<BookingConfirmationDto>(response, ct);
    }

    public async Task CancelBookingAsync(Guid id, string? reason = null, CancellationToken ct = default)
    {
        var payload = new { Reason = reason };
        using var response = await httpClient.PostAsJsonAsync($"scheduling/bookings/{id}/cancel", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new SchedulingApiException(response.StatusCode, message);
        }
    }

    public async Task<BookingSummaryDto> ConfirmBookingAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsync($"scheduling/bookings/{id}/confirm", null, ct);
        return await ReadResponseAsync<BookingSummaryDto>(response, ct);
    }

    public async Task<BookingSummaryDto> RescheduleBookingAsync(Guid id, DateOnly newDate, TimeOnly newTime, CancellationToken ct = default)
    {
        var request = new RescheduleBookingRequest(newDate, newTime);
        using var response = await httpClient.PostAsJsonAsync($"scheduling/bookings/{id}/reschedule", request, ct);
        return await ReadResponseAsync<BookingSummaryDto>(response, ct);
    }

    public async Task SendReminderAsync(Guid id, string reminderType, CancellationToken ct = default)
    {
        var request = new SendBookingReminderRequest(reminderType);
        using var response = await httpClient.PostAsJsonAsync($"scheduling/bookings/{id}/reminders/send", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new SchedulingApiException(response.StatusCode, message);
        }
    }

    public async Task<IReadOnlyList<AvailableTimeSlotDto>> GetAvailableSlotsAsync(
        DateOnly date,
        Guid serviceId,
        string? vehicleSize = null,
        CancellationToken ct = default)
    {
        var uri = $"scheduling/slots?date={date:yyyy-MM-dd}&serviceId={serviceId}";
        if (!string.IsNullOrWhiteSpace(vehicleSize))
        {
            uri += $"&vehicleSize={Uri.EscapeDataString(vehicleSize)}";
        }

        using var response = await httpClient.GetAsync(uri, ct);
        return await ReadResponseAsync<IReadOnlyList<AvailableTimeSlotDto>>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new SchedulingApiException(response.StatusCode, message);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken: ct);
        if (value is null)
        {
            throw new SchedulingApiException(response.StatusCode, "Resposta vazia recebida do serviço de agendamentos.");
        }

        return value;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return $"Operação falhou com status {(int)response.StatusCode}.";
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("description", out var description))
                {
                    return description.GetString() ?? raw;
                }

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    return error.GetString() ?? raw;
                }
            }

            return raw;
        }
        catch
        {
            return $"Operação falhou com status {(int)response.StatusCode}.";
        }
    }
}

public sealed class SchedulingApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
