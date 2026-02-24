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
            // 1. ADIM: DTO'yu Entity'ye çeviriyoruz
            // Mapper; Name, Address gibi kullanıcıdan gelen alanları doldurur.
            var tenant = _mapper.Map<Tenant>(dto);

            // 2. ADIM: İŞ MANTIĞI (Business Logic)
            // Bu değerler DTO'dan gelmez, sistem tarafından burada atanır.
            tenant.ApiKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

            // İşletme için 10 günlük deneme süresi tanımlıyoruz
            tenant.SubscriptionEndDate = DateTime.Now.AddDays(10);

            // Yeni kayıt olan dükkanı varsayılan olarak aktif yapıyoruz
            tenant.IsTrial = true;
            tenant.IsActive = true;

            // 3. ADIM: Veritabanı işlemleri
            await _tenantRepository.AddAsync(tenant);
            await _tenantRepository.SaveAsync();

            // 4. ADIM: AuthManager'ın kullanıcıyı bu tenant'a bağlaması için ID dönüyoruz
            return tenant.TenantID;
        }


    }
}
