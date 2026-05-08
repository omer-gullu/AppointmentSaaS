using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Business.Concrete
{
    public class FeedbackManager : IFeedbackService
    {
        private readonly IFeedbackRepository _feedbackRepository;

        public FeedbackManager(IFeedbackRepository feedbackRepository)
        {
            _feedbackRepository = feedbackRepository;
        }

        public async Task<int> AddAsync(Feedback feedback)
        {
            await _feedbackRepository.AddAsync(feedback);
            await _feedbackRepository.SaveAsync();
            return feedback.FeedbackID;
        }

        public async Task<List<Feedback>> GetAllAsync()
        {
            return await _feedbackRepository.Where(f => f.FeedbackID > 0)
                .Include(f => f.Tenant)
                .OrderByDescending(f => f.SentAt)
                .ToListAsync();
        }

        public async Task<List<Feedback>> GetByTenantIdAsync(int tenantId)
        {
            return await _feedbackRepository.Where(f => f.TenantID == tenantId)
                .OrderByDescending(f => f.SentAt)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int feedbackId)
        {
            var feedback = await _feedbackRepository.Where(f => f.FeedbackID == feedbackId)
                .FirstOrDefaultAsync();
            if (feedback != null)
            {
                feedback.IsRead = true;
                _feedbackRepository.Update(feedback);
                await _feedbackRepository.SaveAsync();
            }
        }
    }
}