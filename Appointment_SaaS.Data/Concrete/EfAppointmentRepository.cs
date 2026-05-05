using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;

namespace Appointment_SaaS.Data.Concrete;

public class EfAppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
{
    public EfAppointmentRepository(AppDbContext context) : base(context) { }
}