using System.Security.Claims;
using Appointment_SaaS.Core.Services;

namespace Appointment_SaaS.API.Services;

/// <summary>
/// JWT Token'daki "TenantId" claim'inden aktif kullanıcının TenantId değerini okur.
/// JwtHelper'da token oluşturulurken eklenen: new Claim("TenantId", user.TenantID.ToString())
/// Bu sınıf AppDbContext'e DI ile enjekte edilerek Global Query Filter'ı besler.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? GetTenantId()
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId");
        
        if (tenantClaim != null && int.TryParse(tenantClaim.Value, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
