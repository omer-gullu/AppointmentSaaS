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
        Task<List<Tenant>> GetAllActiveTenantsAsync();
        Task<Tenant> GetByApiKeyAsync(string apiKey);
        Task AddTenantAsync(Tenant tenant);
    }
}
