using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract
{
    public interface IFeedbackService
    {
        Task<int> AddAsync(Feedback feedback);
        Task<List<Feedback>> GetAllAsync();
        Task<List<Feedback>> GetByTenantIdAsync(int tenantId);
        Task MarkAsReadAsync(int feedbackId);
    }
}