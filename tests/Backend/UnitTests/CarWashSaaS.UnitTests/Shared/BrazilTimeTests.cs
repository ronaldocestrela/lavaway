using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Shared;

public sealed class BrazilTimeTests
{
    [Fact]
    public void Now_Should_Always_Have_Minus_Three_Hours_Offset()
    {
        var now = BrazilTime.Now;

        Assert.Equal(TimeSpan.FromHours(-3), now.Offset);
    }

    [Fact]
    public void ToBrazilTime_Should_Convert_Utc_Timestamp_To_Brasilia_Time()
    {
        // 2026-11-15 01:30:00 UTC equivale a 2026-11-14 22:30:00 no Brasil (-03:00)
        var utc = new DateTimeOffset(2026, 11, 15, 1, 30, 0, TimeSpan.Zero);

        var br = utc.ToBrazilTime();

        Assert.Equal(TimeSpan.FromHours(-3), br.Offset);
        Assert.Equal(2026, br.Year);
        Assert.Equal(11, br.Month);
        Assert.Equal(14, br.Day);
        Assert.Equal(22, br.Hour);
        Assert.Equal(30, br.Minute);
        Assert.Equal(new DateOnly(2026, 11, 14), utc.ToBrazilDateOnly());
    }

    [Fact]
    public void StartOfDayUtc_And_EndOfDayUtc_Should_Respect_Brazil_Business_Day_Window()
    {
        var date = new DateOnly(2026, 11, 14);

        var startUtc = date.StartOfDayUtc();
        var endUtc = date.EndOfDayUtc();

        // 00:00:00 BRT (-03:00) = 03:00:00 UTC do mesmo dia
        Assert.Equal(TimeSpan.Zero, startUtc.Offset);
        Assert.Equal(new DateTime(2026, 11, 14, 3, 0, 0, DateTimeKind.Utc), startUtc.UtcDateTime);

        // 23:59:59.9999999 BRT (-03:00) = 02:59:59.9999999 UTC do dia seguinte (15/11)
        Assert.Equal(TimeSpan.Zero, endUtc.Offset);
        Assert.Equal(2026, endUtc.Year);
        Assert.Equal(11, endUtc.Month);
        Assert.Equal(15, endUtc.Day);
        Assert.Equal(2, endUtc.Hour);
        Assert.Equal(59, endUtc.Minute);
        Assert.Equal(59, endUtc.Second);

        // Uma venda ocorrida às 22:15 BRT do dia 14/11 (01:15 UTC do dia 15/11) deve pertencer ao caixa de 14/11
        var lateNightSaleUtc = new DateTimeOffset(2026, 11, 15, 1, 15, 0, TimeSpan.Zero);
        Assert.True(lateNightSaleUtc >= startUtc && lateNightSaleUtc <= endUtc);
    }
}

