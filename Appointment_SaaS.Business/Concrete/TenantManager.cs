using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
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
        private readonly ITenantRepository _tenantRepository;

        public TenantManager(ITenantRepository tenantRepository)
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

        public async Task<int> AddTenantAsync(TenantCreateDto dto)
        {
            var tenant = new Tenant
            {
                Name = dto.Name,
                SectorID = dto.SectorID,
                ApiKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                WabaID = dto.WabaID,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                IsBotActive = dto.IsBotActive,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.Now,
                MessageCount = 0
            };

            await _tenantRepository.AddAsync(tenant);
            await _tenantRepository.SaveAsync();
            return tenant.TenantID;
        }

      
    }
}
