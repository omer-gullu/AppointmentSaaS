using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.WebUI.Models;
using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Concrete
{
    public class DashboardApiService : IDashboardApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IEvolutionApiService _evolutionApiService;
        private readonly ILogger<DashboardApiService> _logger;

        public DashboardApiService(
            IHttpClientFactory httpClientFactory,
            IEvolutionApiService evolutionApiService,
            ILogger<DashboardApiService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _evolutionApiService = evolutionApiService;
            _logger = logger;
        }

        private async Task<HttpClient> CreateAuthenticatedApiClientAsync()
        {
            var client = _httpClientFactory.CreateClient("Api");
            await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);
            return client;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int tenantId)
        {
            var viewModel = new DashboardViewModel();
            var today = DateTime.Today;

            try
            {
                var httpClient = await CreateAuthenticatedApiClientAsync();

                var appResponse = await httpClient.GetAsync($"api/Appointments/tenant/{tenantId}");
                if (appResponse.IsSuccessStatusCode)
                {
                    var appJson = await appResponse.Content.ReadAsStringAsync();
                    viewModel.Appointments = JsonSerializer.Deserialize<List<dynamic>>(appJson, _jsonOptions);
                }

                var tenantResponse = await httpClient.GetAsync($"api/Tenants/{tenantId}");
                if (tenantResponse.IsSuccessStatusCode)
                {
                    var tenantJson = await tenantResponse.Content.ReadAsStringAsync();
                    var tenantObj = JsonSerializer.Deserialize<JsonElement>(tenantJson);

                    viewModel.ShopName = tenantObj.TryGetProperty("name", out var name) ? name.GetString() : null;
                    viewModel.InstanceName = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() : null;
                    viewModel.GoogleEmail = tenantObj.TryGetProperty("googleEmail", out var email) ? email.GetString() : null;
                    viewModel.IsBotActive = tenantObj.TryGetProperty("isBotActive", out var botActive) ? botActive.GetBoolean() : true;
                    viewModel.PlanType = tenantObj.TryGetProperty("planType", out var plan) ? plan.GetString() ?? "Trial" : "Trial";
                    viewModel.SubscriptionEndDate = tenantObj.TryGetProperty("subscriptionEndDate", out var subEnd) ? subEnd.GetDateTime() : DateTime.Now;

                    if (tenantObj.TryGetProperty("businessHours", out var bh) && bh.ValueKind == JsonValueKind.Array)
                    {
                        var bhList = JsonSerializer.Deserialize<List<dynamic>>(bh.GetRawText(), _jsonOptions);
                        viewModel.BusinessHours = bhList;
                    }

                    if (!string.IsNullOrWhiteSpace(viewModel.InstanceName))
                        viewModel.IsWhatsAppConnected = await _evolutionApiService.IsInstanceConnectedAsync(viewModel.InstanceName);
                }

                var servicesResponse = await httpClient.GetAsync($"api/Services/tenant/{tenantId}");
                if (servicesResponse.IsSuccessStatusCode)
                {
                    var servicesJson = await servicesResponse.Content.ReadAsStringAsync();
                    viewModel.Services = JsonSerializer.Deserialize<List<dynamic>>(servicesJson, _jsonOptions);
                }

                var staffResponse = await httpClient.GetAsync($"api/AppUsers/staff/{tenantId}");
                if (staffResponse.IsSuccessStatusCode)
                {
                    var staffJson = await staffResponse.Content.ReadAsStringAsync();
                    viewModel.Staff = JsonSerializer.Deserialize<List<dynamic>>(staffJson, _jsonOptions);
                    viewModel.CurrentStaffCount = viewModel.Staff?.Count ?? 0;
                }

                viewModel.TotalAppointmentCount = viewModel.Appointments?.Count ?? 0;
                viewModel.TodayAppointmentCount = viewModel.Appointments?
                    .Count(a =>
                    {
                        try { return DateTime.Parse(((JsonElement)a).GetProperty("startDate").ToString()).Date == today; }
                        catch { return false; }
                    }) ?? 0;
                viewModel.TotalServiceCount = viewModel.Services?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data for tenant {TenantId}", tenantId);
            }

            return viewModel;
        }

        public async Task<bool> ToggleAssistantAsync(int tenantId, bool isActive)
        {
            var httpClient = await CreateAuthenticatedApiClientAsync();

            var response = await httpClient.PostAsync($"api/Tenants/UpdateBotStatus?tenantId={tenantId}&isBotActive={isActive}", null);
            if (!response.IsSuccessStatusCode) return false;

            var tenantResponse = await httpClient.GetAsync($"api/Tenants/{tenantId}");
            if (tenantResponse.IsSuccessStatusCode)
            {
                var tenantObj = JsonSerializer.Deserialize<JsonElement>(await tenantResponse.Content.ReadAsStringAsync());
                string? instanceName = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() : null;

                if (!string.IsNullOrWhiteSpace(instanceName))
                {
                    if (isActive) await _evolutionApiService.ConnectInstanceAsync(instanceName);
                    else await _evolutionApiService.DisconnectInstanceAsync(instanceName);
                }
            }
            return true;
        }

        public async Task<bool> CreateServiceAsync(int tenantId, string name, decimal price, int durationMinutes)
        {
            var httpClient = await CreateAuthenticatedApiClientAsync();
            var payload = new { name, price, durationMinutes, tenantID = tenantId };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("api/Services", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateServiceAsync(int tenantId, int serviceId, string name, decimal price, int durationMinutes)
        {
            var httpClient = await CreateAuthenticatedApiClientAsync();
            var payload = new { name, price, durationMinutes, tenantID = tenantId };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"api/Services/{serviceId}", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteServiceAsync(int serviceId)
        {
            var httpClient = await CreateAuthenticatedApiClientAsync();
            var response = await httpClient.DeleteAsync($"api/Services/{serviceId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<(bool Success, string Message)> CancelSubscriptionAsync(int tenantId)
        {
            var httpClient = await CreateAuthenticatedApiClientAsync();
            var response = await httpClient.PostAsync($"api/Tenants/{tenantId}/cancel-subscription", null);
            if (response.IsSuccessStatusCode) return (true, "Aboneliğiniz iptal talebi alındı.");
            return (false, await response.Content.ReadAsStringAsync());
        }
    }
}
