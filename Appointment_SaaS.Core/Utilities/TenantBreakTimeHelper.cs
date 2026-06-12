namespace Appointment_SaaS.Core.Utilities;

public static class TenantBreakTimeHelper
{
    public static bool OverlapsBreak(
        bool isEnabled,
        TimeSpan breakStart,
        TimeSpan breakEnd,
        DateTime appointmentStart,
        DateTime appointmentEnd)
    {
        if (!isEnabled || breakEnd <= breakStart)
            return false;

        var day = appointmentStart.Date;
        var blockStart = day.Add(breakStart);
        var blockEnd = day.Add(breakEnd);
        return appointmentStart < blockEnd && appointmentEnd > blockStart;
    }

    /// <summary>Slot mola ile çakışıyorsa öneri döngüsünde atlanacak saat (mola bitişi).</summary>
    public static DateTime? GetResumeTimeAfterBreak(
        bool isEnabled,
        TimeSpan breakStart,
        TimeSpan breakEnd,
        DateTime day,
        DateTime slotStart,
        DateTime slotEnd)
    {
        if (!isEnabled || breakEnd <= breakStart)
            return null;

        var blockStart = day.Add(breakStart);
        var blockEnd = day.Add(breakEnd);
        if (slotStart < blockEnd && slotEnd > blockStart)
            return blockEnd;

        return null;
    }
}
