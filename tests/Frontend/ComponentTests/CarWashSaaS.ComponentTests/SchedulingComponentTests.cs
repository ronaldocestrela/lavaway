using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class SchedulingComponentTests : BunitContext
{
    private static BookingSummaryDto CreateSampleBooking(
        string status = BookingStatusConstants.Scheduled,
        string origin = BookingOriginConstants.WhatsAppBot) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BK-2610-8F9D",
            null,
            "Carlos Eduardo",
            "11988887777",
            "ABC1D23",
            "Civic G10",
            VehicleSizeConstants.HatchSedan,
            Guid.NewGuid(),
            "Polimento Técnico",
            350.00m,
            DateOnly.FromDateTime(DateTime.Today),
            new TimeOnly(14, 0),
            120,
            status,
            origin,
            null,
            "Cliente solicitou cera especial",
            DateTimeOffset.UtcNow);

    [Fact]
    public void BookingCard_ShouldRenderDetails_AndTriggerCheckIn()
    {
        var booking = CreateSampleBooking();
        BookingSummaryDto? checkedIn = null;

        var cut = Render<BookingCard>(parameters => parameters
            .Add(p => p.Booking, booking)
            .Add(p => p.OnCheckIn, b => checkedIn = b));

        Assert.Contains("ABC-1D23", cut.Markup);
        Assert.Contains("Carlos Eduardo", cut.Markup);
        Assert.Contains("14:00", cut.Markup);
        Assert.Contains("120 min", cut.Markup);
        Assert.Contains("Polimento Técnico", cut.Markup);
        Assert.Contains("350,00", cut.Markup);
        Assert.Contains("WhatsApp Bot", cut.Markup);
        Assert.Contains("Agendado", cut.Markup);
        Assert.Contains("#BK-2610-8F9D", cut.Markup);

        var checkinBtn = cut.Find("button.btn-checkin");
        Assert.Contains("Iniciar Atendimento", checkinBtn.TextContent);
        checkinBtn.Click();

        Assert.NotNull(checkedIn);
        Assert.Equal(booking.Id, checkedIn.Id);
    }

    [Fact]
    public void BookingCard_ShouldTriggerCancel()
    {
        var booking = CreateSampleBooking();
        BookingSummaryDto? cancelled = null;

        var cut = Render<BookingCard>(parameters => parameters
            .Add(p => p.Booking, booking)
            .Add(p => p.OnCancel, b => cancelled = b));

        var cancelBtn = cut.Find("button.btn-cancel");
        cancelBtn.Click();

        Assert.NotNull(cancelled);
        Assert.Equal(booking.Id, cancelled.Id);
    }

    [Fact]
    public void BookingCapacityMeter_ShouldRenderStatistics()
    {
        var cut = Render<BookingCapacityMeter>(parameters => parameters
            .Add(p => p.BookingsCount, 5)
            .Add(p => p.OccupiedBoxes, 3)
            .Add(p => p.TotalBoxes, 4));

        var statsText = cut.Find(".meter-stats").TextContent;
        Assert.Contains("5", statsText);
        Assert.Contains("agendamentos hoje", statsText);
        Assert.Contains("3 / 4", statsText);
        Assert.Contains("boxes ocupados", statsText);
        Assert.Contains("style=\"width: 75%;\"", cut.Markup);
        Assert.Contains("progress-warning", cut.Markup);
    }
}
