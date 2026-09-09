using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class BusinessClockTests
{
    [Fact]
    public void ToUtc_UnspecifiedIstanbulAfternoon_IsThreeHoursEarlier()
    {
        // Türkiye 2016'dan beri yaz saati uygulamaz; UTC+3 sabittir.
        var wall = new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Unspecified);

        var utc = BusinessClock.ToUtc(wall);

        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToIstanbul_UtcMorning_IsAfternoonWallClock()
    {
        var utc = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc);

        var wall = BusinessClock.ToIstanbul(utc);

        wall.Should().Be(new DateTime(2026, 7, 15, 14, 0, 0));
    }

    [Fact]
    public void UtcRangeForIstanbulDate_CoversMidnightToMidnightInUtc()
    {
        var (startUtc, endUtc) = BusinessClock.UtcRangeForIstanbulDate(new DateOnly(2026, 7, 15));

        startUtc.Should().Be(new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 7, 15, 21, 0, 0, DateTimeKind.Utc));
        startUtc.Kind.Should().Be(DateTimeKind.Utc);
        endUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToUtc_AlreadyUtc_StaysIdentity()
    {
        var utc = new DateTime(2026, 9, 8, 11, 0, 0, DateTimeKind.Utc);

        BusinessClock.ToUtc(utc).Should().Be(utc);
    }
}
