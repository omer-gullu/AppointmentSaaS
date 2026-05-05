using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Concrete
{
    public class AppointmentApiService : IAppointmentApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<AppointmentApiService> _logger;

        public AppointmentApiService(
            IHttpClientFactory httpClientFactory,
            ILogger<AppointmentApiService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _logger = logger;
        }

        private async Task<HttpClient> CreateAuthenticatedApiClientAsync()
        {
            var client = _httpClientFactory.CreateClient("Api");
            await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);
            return client;
        }

        public async Task<(bool Success, string Message, int? AppointmentId, int? AppUserId)> CreateAppointmentAsync(
            int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate)
        {
            try
            {
                var httpClient = await CreateAuthenticatedApiClientAsync();

                var tenantResponse = await httpClient.GetAsync($"api/Tenants/{tenantId}");
                string businessPhoneOrInstance = string.Empty;
                if (tenantResponse.IsSuccessStatusCode)
                {
                    var tenantObj = JsonSerializer.Deserialize<JsonElement>(await tenantResponse.Content.ReadAsStringAsync());
                    businessPhoneOrInstance = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() ?? string.Empty : string.Empty;
                }

                var payload = new
                {
                    customerName,
                    customerPhone,
                    serviceID = serviceId,
                    startDate,
                    businessPhone = businessPhoneOrInstance
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("api/Appointments", content);

                if (response.IsSuccessStatusCode)
                {
                    var createObj = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), _jsonOptions);
                    int newAppointmentId = createObj.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                    int? newAppUserId = createObj.TryGetProperty("appUserID", out var uidProp) ? uidProp.GetInt32() : (int?)null;
                    return (true, "Randevu başarıyla eklendi.", newAppointmentId, newAppUserId);
                }

                var err = await response.Content.ReadAsStringAsync();
                return (false, $"Hata: {err}", null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CreateAppointmentAsync");
                return (false, "Sistem hatası oluştu.", null, null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateAppointmentAsync(
            int tenantId, int appointmentId, string customerName, string customerPhone, int serviceId, DateTime startDate, string? googleEventId)
        {
            try
            {
                var httpClient = await CreateAuthenticatedApiClientAsync();

                var tenantResponse = await httpClient.GetAsync($"api/Tenants/{tenantId}");
                string instanceName = string.Empty;
                if (tenantResponse.IsSuccessStatusCode)
                {
                    var tenantObj = JsonSerializer.Deserialize<JsonElement>(await tenantResponse.Content.ReadAsStringAsync());
                    instanceName = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() ?? string.Empty : string.Empty;
                }

                var payload = new
                {
                    customerName,
                    customerPhone,
                    serviceID = serviceId,
                    startDate,
                    businessPhone = instanceName,
                    note = (string?)null,
                    googleEventID = googleEventId
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync($"api/Appointments/{appointmentId}", content);

                if (response.IsSuccessStatusCode)
                    return (true, "Randevu başarıyla güncellendi.");

                var err = await response.Content.ReadAsStringAsync();
                return (false, $"Hata: {err}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in UpdateAppointmentAsync");
                return (false, "Sistem hatası oluştu.");
            }
        }

        public async Task<bool> UpdateAppointmentGoogleEventIdAsync(int appointmentId, int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate, string googleEventId)
        {
            try
            {
                var httpClient = await CreateAuthenticatedApiClientAsync();

                var tenantResponse = await httpClient.GetAsync($"api/Tenants/{tenantId}");
                string instanceName = string.Empty;
                if (tenantResponse.IsSuccessStatusCode)
                {
                    var tenantObj = JsonSerializer.Deserialize<JsonElement>(await tenantResponse.Content.ReadAsStringAsync());
                    instanceName = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() ?? string.Empty : string.Empty;
                }

                var payload = new
                {
                    customerName,
                    customerPhone,
                    serviceID = serviceId,
                    startDate,
                    businessPhone = instanceName,
                    googleEventID = googleEventId
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync($"api/Appointments/{appointmentId}", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in UpdateAppointmentGoogleEventIdAsync");
                return false;
            }
        }

        public async Task<(bool Success, string Message)> DeleteAppointmentAsync(int appointmentId)
        {
            try
            {
                var httpClient = await CreateAuthenticatedApiClientAsync();
                var response = await httpClient.DeleteAsync($"api/Appointments/{appointmentId}");

                if (response.IsSuccessStatusCode)
                    return (true, "Randevu başarıyla silindi.");

                var err = await response.Content.ReadAsStringAsync();
                return (false, $"Hata: {err}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in DeleteAppointmentAsync");
                return (false, "Sistem hatası oluştu.");
            }
        }
    }
}
