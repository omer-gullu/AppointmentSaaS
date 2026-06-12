using Appointment_SaaS.Business.Abstract;

namespace Appointment_SaaS.API.Authorization;

public static class WebhookTenantResolver
{
    public static int? TryGetTenantIdFromRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Tenant-Id", out var headerVal)
            && int.TryParse(headerVal.FirstOrDefault(), out var fromHeader)
            && fromHeader > 0)
            return fromHeader;

        if (request.Query.TryGetValue("tenantId", out var qTenant)
            && int.TryParse(qTenant.FirstOrDefault(), out var fromQuery)
            && fromQuery > 0)
            return fromQuery;

        return null;
    }

    public static async Task<int?> ResolveTenantIdAsync(HttpRequest request, ITenantService tenantService)
    {
        var explicitId = TryGetTenantIdFromRequest(request);
        if (explicitId.HasValue)
            return explicitId;

        var instanceName = request.Query["instanceName"].FirstOrDefault()
                           ?? request.Query["instance"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(instanceName))
            return null;

        var tenant = await tenantService.GetContextByInstanceAsync(instanceName.Trim());
        return tenant?.TenantID;
    }
}
