using Appointment_SaaS.Business.Abstract;

namespace Appointment_SaaS.API.Services;

/// <summary>
/// CI / izole Postgres kapısı: Google Calendar HTTP çağrısı yok.
/// Üretimde kullanılmaz; <c>Google:SkipEventSync=true</c> ile bağlanır.
/// </summary>
public sealed class NoOpGoogleCalendarService : IGoogleCalendarService
{
    public Task<string?> AddEventAsync(int appUserId, string summary, string description, DateTime start, DateTime end)
        => Task.FromResult<string?>("ci-noop-event");

    public Task<bool> UpdateEventAsync(int appUserId, string googleEventId, string summary, string description, DateTime start, DateTime end)
        => Task.FromResult(true);

    public Task DeleteEventAsync(int appUserId, string googleEventId)
        => Task.CompletedTask;
}
