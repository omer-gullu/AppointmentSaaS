using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Test.TestHelpers;

/// <summary>
/// CI (ubuntu/UTC) ile Windows (TR) arasında DateTime.Now/Kind farklarını yok eder.
/// Tüm randevu testleri İstanbul duvar saati (Unspecified) veya açık UTC kullanmalı.
/// </summary>
internal static class TestTime
{
    public static DateTime IstanbulWall(int daysFromToday, int hour, int minute = 0)
        => BusinessClock.IstanbulToday.AddDays(daysFromToday).ToDateTime(new TimeOnly(hour, minute));

    public static DateTime IstanbulWallUtc(int daysFromToday, int hour, int minute = 0)
        => BusinessClock.ToUtc(IstanbulWall(daysFromToday, hour, minute));
}
