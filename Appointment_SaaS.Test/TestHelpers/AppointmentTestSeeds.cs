using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Test.TestHelpers;

internal static class AppointmentTestSeeds
{
    public static async Task<int> EnsureStaffWithGoogleAsync(AppDbContext db, int tenantId = 1)
    {
        var staff = await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.TenantID == tenantId
                && u.Status
                && !string.IsNullOrWhiteSpace(u.GoogleRefreshToken)
                && !string.IsNullOrWhiteSpace(u.GoogleCalendarId));

        if (staff != null)
            return staff.AppUserID;

        var user = new AppUser
        {
            TenantID = tenantId,
            FirstName = "Test",
            LastName = "Personel",
            Email = "staff@test.com",
            PhoneNumber = "5551112233",
            Status = true,
            GoogleRefreshToken = "test-refresh-token",
            GoogleCalendarId = "staff@test.calendar"
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user.AppUserID;
    }
}
