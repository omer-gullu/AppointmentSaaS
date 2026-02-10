using Appointment_SaaS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract;

public interface IAppointmentService
{
    Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId);
    Task AddAppointmentAsync(Appointment appointment);
    Task<bool> IsSlotAvailableAsync(int tenantId, DateTime date); // Aynı saate iki randevu gelmesin diye
}
