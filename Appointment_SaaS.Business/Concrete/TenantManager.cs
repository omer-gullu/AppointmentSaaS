using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Concrete
{
    public class TenantManager : ITenantService
    {
        private readonly IGenericRepository<Tenant> _tenantRepository;

        public TenantManager(IGenericRepository<Tenant> tenantRepository)
        {
            _tenantRepository = tenantRepository;
        }

        public async Task<Tenant> GetByApiKeyAsync(string apiKey)
        {
            // n8n için hayati metot: Gelen anahtarın hangi dükkana ait olduğunu bulur
            return await _tenantRepository.Where(x => x.ApiKey == apiKey && x.IsActive).FirstOrDefaultAsync();
        }

        public async Task<List<Tenant>> GetAllActiveTenantsAsync()
        {
            return await _tenantRepository.Where(x => x.IsActive).ToListAsync();
        }

        public async Task AddTenantAsync(Tenant tenant)
        {
            await _tenantRepository.AddAsync(tenant);
            await _tenantRepository.SaveAsync();
        }

        public async Task<List<Tenant>> GetAllAsync()
        {
           return await _tenantRepository.GetAllAsync();
        }
    }
}
