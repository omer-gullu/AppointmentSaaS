using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services
{
    /// <summary>
    /// JWT token'ı ASP.NET Core auth cookie'nin içinden okuyarak HttpClient'a ekler.
    /// Login sırasında StoreTokens ile kaydedilen "access_token" burada okunur.
    /// </summary>
    public static class HttpClientTokenHelper
    {
        public static async Task AttachBearerTokenAsync(HttpClient client, IHttpContextAccessor httpContextAccessor)
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            // 1. Önce ASP.NET Core auth cookie içindeki token'ı oku (StoreTokens ile kaydedilen)
            var token = await httpContext.GetTokenAsync("access_token");

            // 2. Fallback: Eğer eski cookie hala varsa onu kullan
            if (string.IsNullOrEmpty(token))
            {
                token = httpContext.Request.Cookies["token"];
            }

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}
