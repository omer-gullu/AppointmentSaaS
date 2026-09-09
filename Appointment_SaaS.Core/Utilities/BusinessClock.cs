namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// İşletme saati Avrupa/İstanbul'dur. Hetzner API UTC çalışır; n8n ise
/// <c>2026-09-28T14:00:00</c> gibi Z'siz (Unspecified) İstanbul duvar saati gönderir.
/// Postgres sütunları timestamptz olduğu için sorgulara/yazımlara UTC Kind basılmalı.
/// </summary>
public static class BusinessClock
{
    public static readonly TimeZoneInfo Istanbul = ResolveIstanbul();

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime IstanbulNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Istanbul);

    public static DateOnly IstanbulToday => DateOnly.FromDateTime(IstanbulNow);

    /// <summary>
    /// Unspecified = n8n / panel duvar saati (İstanbul).
    /// Local = makine saati (Hetzner'de UTC).
    /// Utc = zaten anlık.
    /// </summary>
    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local => TimeZoneInfo.ConvertTimeToUtc(value),
            _ => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(value, DateTimeKind.Unspecified), Istanbul)
        };
    }

    public static DateTime ToIstanbul(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, Istanbul),
            DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, Istanbul),
            _ => value
        };
    }

    public static (DateTime StartUtc, DateTime EndUtc) UtcRangeForIstanbulDate(DateOnly date)
    {
        var startLocal = date.ToDateTime(TimeOnly.MinValue);
        var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (ToUtc(startLocal), ToUtc(endLocal));
    }

    public static (DateTime StartUtc, DateTime EndUtc) UtcRangeForIstanbulDay(DateTime any)
    {
        var day = DateOnly.FromDateTime(ToIstanbul(any).Date);
        return UtcRangeForIstanbulDate(day);
    }

    public static (DateTime StartUtc, DateTime EndUtc) UtcRange(DateTime istanbulWallStart, DateTime istanbulWallEnd)
        => (ToUtc(istanbulWallStart), ToUtc(istanbulWallEnd));

    private static TimeZoneInfo ResolveIstanbul()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Europe/Istanbul zaman dilimi bu makinede yok.");
    }
}
