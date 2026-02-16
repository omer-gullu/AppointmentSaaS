using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract
{
    public interface ITenantService
    {
        Task<List<Tenant>> GetAllAsync();
        Task<Tenant> GetByApiKeyAsync(string apiKey);
         public Task<int> AddTenantAsync(TenantCreateDto dto);
    }
}
