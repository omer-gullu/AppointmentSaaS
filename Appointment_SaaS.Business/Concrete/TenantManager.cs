using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using AutoMapper;
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
        private readonly IMapper _mapper;

        public TenantManager(ITenantRepository tenantRepository, IMapper mapper)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
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


        public async Task<List<Tenant>> GetAllAsync()
        {
           return await _tenantRepository.GetAllAsync();
        }
        public async Task<int> AddTenantAsync(TenantCreateDto dto)
        {
            // 1. Temel alanları DTO'dan eşle
            var tenant = _mapper.Map<Tenant>(dto);

            // 2. DTO'da olmayan özel mantıkları ekle
            tenant.ApiKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

            // Not: CreatedAt ve MessageCount'u Profile'a yazdıysan burada yazmana gerek yok.
            // Ama garanti olsun dersen burada da kalabilir.

            await _tenantRepository.AddAsync(tenant);
            await _tenantRepository.SaveAsync();

            return tenant.TenantID;
        }


    }
}
