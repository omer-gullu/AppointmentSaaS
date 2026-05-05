using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Concrete
{
    public class GoogleCalendarApiService : IGoogleCalendarApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleCalendarApiService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GoogleCalendarApiService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GoogleCalendarApiService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Her çağrıda taze bir HttpClient oluşturur ve JWT Bearer token'ı ekler.
        /// Constructor'da token okumak yerine her metod çağrısında async olarak okunur.
        /// </summary>
        private async Task<HttpClient> CreateAuthenticatedApiClientAsync()
        {
            var client = _httpClientFactory.CreateClient("Api");
            await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);
            return client;
        }

        public string GetConnectUrl()
        {
            var clientId = _configuration["Google:ClientId"];
            var redirectUri = _configuration["Google:RedirectUri"];
            var scope = "https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/userinfo.email openid";

            return "https://accounts.google.com/o/oauth2/v2/auth?" +
                   $"client_id={clientId}" +
                   $"&redirect_uri={System.Net.WebUtility.UrlEncode(redirectUri)}" +
                   "&response_type=code" +
                   $"&scope={System.Net.WebUtility.UrlEncode(scope)}" +
                   "&access_type=offline" +
                   "&include_granted_scopes=true" +
                   "&prompt=consent";
        }

        public string GetConnectStaffUrl(int staffId)
        {
            var clientId = _configuration["Google:ClientId"];
            var redirectUri = _configuration["Google:RedirectUri"];
            var scope = "https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/userinfo.email openid";

            return "https://accounts.google.com/o/oauth2/v2/auth?" +
                   $"client_id={clientId}" +
                   $"&redirect_uri={System.Net.WebUtility.UrlEncode(redirectUri)}" +
                   "&response_type=code" +
                   $"&scope={System.Net.WebUtility.UrlEncode(scope)}" +
                   "&access_type=offline" +
                   "&include_granted_scopes=true" +
                   $"&state=staff_{staffId}" +
                   "&prompt=consent";
        }

        public async Task<(bool Success, string Message)> ProcessCallbackAsync(string code, int tenantId)
        {
            return await ProcessGoogleOAuthCallbackAsync(code, tenantId, null);
        }

        public async Task<(bool Success, string Message)> ProcessStaffCallbackAsync(string code, int tenantId, int staffId)
        {
            return await ProcessGoogleOAuthCallbackAsync(code, tenantId, staffId);
        }

        private async Task<(bool Success, string Message)> ProcessGoogleOAuthCallbackAsync(string code, int tenantId, int? staffId)
        {
            try
            {
                var googleClient = _httpClientFactory.CreateClient();
                var clientId = _configuration["Google:ClientId"];
                var clientSecret = _configuration["Google:ClientSecret"];
                var redirectUri = _configuration["Google:RedirectUri"];

                var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", clientId! },
                    { "client_secret", clientSecret! },
                    { "redirect_uri", redirectUri! },
                    { "grant_type", "authorization_code" }
                });

                var tokenResponse = await googleClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequestContent);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var error = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Google token error: {Error}", error);
                    return (false, $"Google yetkilendirme hatası: {error}");
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(await tokenResponse.Content.ReadAsStringAsync());
                string accessToken = tokenData.GetProperty("access_token").GetString()!;
                string? refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                var profileRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var profileResponse = await googleClient.SendAsync(profileRequest);

                string userEmail = "bilinmeyen@gmail.com";
                if (profileResponse.IsSuccessStatusCode)
                {
                    var profileData = JsonSerializer.Deserialize<JsonElement>(await profileResponse.Content.ReadAsStringAsync());
                    userEmail = profileData.GetProperty("email").GetString()!;
                }

                string tokenToSave = refreshToken ?? accessToken;
                var apiClient = await CreateAuthenticatedApiClientAsync();

                if (staffId.HasValue)
                {
                    var payload = new { email = userEmail, token = tokenToSave };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var updateResponse = await apiClient.PostAsync($"api/AppUsers/{staffId.Value}/google-token", content);
                    
                    if (!updateResponse.IsSuccessStatusCode)
                    {
                        var errBody = await updateResponse.Content.ReadAsStringAsync();
                        return (false, $"Personel API Güncelleme Hatası: {errBody}");
                    }
                }
                else
                {
                    var payload = new { tenantId, email = userEmail, token = tokenToSave };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var updateResponse = await apiClient.PostAsync("api/Tenants/UpdateGoogleEmail", content);
                    
                    if (!updateResponse.IsSuccessStatusCode)
                    {
                        var errBody = await updateResponse.Content.ReadAsStringAsync();
                        return (false, $"Tenant API Güncelleme Hatası: {errBody}");
                    }
                }

                return (true, "Başarılı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System error in ProcessGoogleOAuthCallbackAsync");
                return (false, $"Sistem hatası: {ex.Message}");
            }
        }

        private async Task<(string? AccessToken, string? CalendarId, string ServiceName, int DurationMinutes)> GetGoogleCalendarContextAsync(string instanceName, int serviceId, int? staffId = null)
        {
            int durationMinutes = 30;
            string serviceName = $"Hizmet #{serviceId}";

            var apiClient = await CreateAuthenticatedApiClientAsync();

            HttpResponseMessage tokenResponse;
            if (staffId.HasValue && staffId.Value > 0)
            {
                tokenResponse = await apiClient.GetAsync($"api/AppUsers/{staffId.Value}/google-token");
            }
            else
            {
                tokenResponse = await apiClient.GetAsync($"api/Tenants/GetGoogleAccessToken?instanceName={instanceName}");
            }

            if (!tokenResponse.IsSuccessStatusCode) 
            {
                _logger.LogWarning("Failed to get Google Access Token for instance {InstanceName} or staff {StaffId}", instanceName, staffId);
                return (null, null, serviceName, durationMinutes);
            }

            var tokenObj = JsonSerializer.Deserialize<JsonElement>(await tokenResponse.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var accessToken = tokenObj.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            var calendarId = tokenObj.TryGetProperty("calendarId", out var cal) ? cal.GetString() : null;

            try 
            {
                var serviceResponse = await apiClient.GetAsync($"api/Services/{serviceId}");
                if (serviceResponse.IsSuccessStatusCode)
                {
                    var s = JsonSerializer.Deserialize<JsonElement>(await serviceResponse.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    durationMinutes = s.TryGetProperty("durationInMinutes", out var d) ? d.GetInt32() : 30;
                    serviceName = s.TryGetProperty("name", out var n) ? n.GetString() ?? serviceName : serviceName;
                }
            }
            catch(Exception ex)
            {
                 _logger.LogWarning(ex, "Could not fetch service details for ServiceId {ServiceId}", serviceId);
            }

            return (accessToken, calendarId, serviceName, durationMinutes);
        }

        public async Task<string?> CreateEventAsync(string instanceName, string customerName, int serviceId, string customerPhone, DateTime startDate, int? staffId = null)
        {
            try
            {
                var (accessToken, calendarId, serviceName, durationMinutes) = await GetGoogleCalendarContextAsync(instanceName, serviceId, staffId);
                if (accessToken == null || calendarId == null) return null;

                var googleClient = _httpClientFactory.CreateClient();
                googleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var eventPayload = new
                {
                    summary = $"Randevu: {customerName} - {serviceName}",
                    description = $"Müşteri Tel: {customerPhone}",
                    start = new { dateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" },
                    end = new { dateTime = startDate.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" }
                };

                var content = new StringContent(JsonSerializer.Serialize(eventPayload), Encoding.UTF8, "application/json");
                var insertResponse = await googleClient.PostAsync(
                    $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events", content);

                if (!insertResponse.IsSuccessStatusCode) 
                {
                    var error = await insertResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create Google Calendar Event: {Error}", error);
                    return null;
                }

                var insertObj = JsonSerializer.Deserialize<JsonElement>(await insertResponse.Content.ReadAsStringAsync());
                return insertObj.TryGetProperty("id", out var evId) ? evId.GetString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CreateEventAsync");
                return null;
            }
        }

        public async Task<bool> UpdateEventAsync(string instanceName, string googleEventId, string customerName, int serviceId, string customerPhone, DateTime startDate, int? staffId = null)
        {
            try
            {
                var (accessToken, calendarId, serviceName, durationMinutes) = await GetGoogleCalendarContextAsync(instanceName, serviceId, staffId);
                if (accessToken == null || calendarId == null) return false;

                var googleClient = _httpClientFactory.CreateClient();
                googleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var eventPayload = new
                {
                    summary = $"Randevu: {customerName} - {serviceName}",
                    description = $"Müşteri Tel: {customerPhone}",
                    start = new { dateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" },
                    end = new { dateTime = startDate.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" }
                };

                var content = new StringContent(JsonSerializer.Serialize(eventPayload), Encoding.UTF8, "application/json");
                var updateResponse = await googleClient.PutAsync(
                    $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(googleEventId)}",
                    content);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update Google Calendar Event: {Error}", error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in UpdateEventAsync");
                return false;
            }
        }

        public async Task<bool> DeleteEventAsync(string instanceName, string googleEventId, int? staffId = null)
        {
            try
            {
                var apiClient = await CreateAuthenticatedApiClientAsync();

                HttpResponseMessage tokenResponse;
                if (staffId.HasValue && staffId.Value > 0)
                {
                    tokenResponse = await apiClient.GetAsync($"api/AppUsers/{staffId.Value}/google-token");
                }
                else
                {
                    tokenResponse = await apiClient.GetAsync($"api/Tenants/GetGoogleAccessToken?instanceName={instanceName}");
                }

                if (!tokenResponse.IsSuccessStatusCode) return false;

                var tokenObj = JsonSerializer.Deserialize<JsonElement>(await tokenResponse.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var accessToken = tokenObj.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
                var calendarId = tokenObj.TryGetProperty("calendarId", out var cal) ? cal.GetString() : null;
                
                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(calendarId)) return false;

                var googleClient = _httpClientFactory.CreateClient();
                googleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var deleteResponse = await googleClient.DeleteAsync(
                    $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(googleEventId)}");

                if (!deleteResponse.IsSuccessStatusCode)
                {
                     var error = await deleteResponse.Content.ReadAsStringAsync();
                     _logger.LogError("Failed to delete Google Calendar Event: {Error}", error);
                     return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in DeleteEventAsync");
                return false;
            }
        }
    }
}
