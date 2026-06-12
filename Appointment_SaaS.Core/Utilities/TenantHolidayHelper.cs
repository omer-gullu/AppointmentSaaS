using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.Utilities;

public static class TenantHolidayHelper
{
    /// <summary>Randevu aralığının kapsadığı günlerden herhangi biri tatil ise o kaydı döner.</summary>
    public static Holiday? FindHolidayForAppointment(
        IEnumerable<Holiday>? holidays,
        DateTime appointmentStart,
        DateTime appointmentEnd)
    {
        if (holidays == null)
            return null;

        var list = holidays as ICollection<Holiday> ?? holidays.ToList();
        if (list.Count == 0)
            return null;

        for (var day = appointmentStart.Date; day <= appointmentEnd.Date; day = day.AddDays(1))
        {
            var dateOnly = DateOnly.FromDateTime(day);
            var match = list.FirstOrDefault(h => h.Date == dateOnly);
            if (match != null)
                return match;
        }

        return null;
    }
}
