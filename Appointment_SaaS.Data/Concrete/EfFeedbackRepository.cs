using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;

namespace Appointment_SaaS.Data.Concrete
{
    public class EfFeedbackRepository : GenericRepository<Feedback>, IFeedbackRepository
    {
        public EfFeedbackRepository(AppDbContext context) : base(context) { }
    }
}