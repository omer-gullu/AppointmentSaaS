using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract;

public interface IGoogleCalendarService
{
    Task<string?> AddEventAsync(int appUserId, string summary, string description, DateTime start, DateTime end);
    Task<bool> UpdateEventAsync(int appUserId, string googleEventId, string summary, string description, DateTime start, DateTime end);
    Task DeleteEventAsync(int appUserId, string googleEventId);
}
