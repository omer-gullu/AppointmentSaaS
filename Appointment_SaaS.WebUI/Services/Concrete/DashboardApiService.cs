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
                var bundleResponse = await httpClient.GetAsync($"api/Dashboard/tenant/{tenantId}");
                if (!bundleResponse.IsSuccessStatusCode)
                    return viewModel;

                var bundleJson = await bundleResponse.Content.ReadAsStringAsync();
                var bundle = JsonSerializer.Deserialize<JsonElement>(bundleJson, _jsonOptions);

                if (bundle.TryGetProperty("tenant", out var tenantObj))
                    MapTenant(viewModel, tenantObj);

                if (bundle.TryGetProperty("appointments", out var appointments) && appointments.ValueKind == JsonValueKind.Array)
                    viewModel.Appointments = JsonSerializer.Deserialize<List<dynamic>>(appointments.GetRawText(), _jsonOptions);

                if (bundle.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
                    viewModel.Services = JsonSerializer.Deserialize<List<dynamic>>(services.GetRawText(), _jsonOptions);

                if (bundle.TryGetProperty("staff", out var staff) && staff.ValueKind == JsonValueKind.Array)
                {
                    viewModel.Staff = JsonSerializer.Deserialize<List<dynamic>>(staff.GetRawText(), _jsonOptions);
                    viewModel.CurrentStaffCount = viewModel.Staff?.Count ?? 0;
                }

                if (!string.IsNullOrWhiteSpace(viewModel.InstanceName))
                    viewModel.IsWhatsAppConnected = await _evolutionApiService.IsInstanceConnectedAsync(viewModel.InstanceName);

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

        private static void MapTenant(DashboardViewModel viewModel, JsonElement tenantObj)
        {
            viewModel.ShopName = tenantObj.TryGetProperty("name", out var name) ? name.GetString() : null;
            viewModel.InstanceName = tenantObj.TryGetProperty("instanceName", out var inst) ? inst.GetString() : null;
            viewModel.GoogleEmail = tenantObj.TryGetProperty("googleEmail", out var email) ? email.GetString() : null;
            viewModel.IsBotActive = tenantObj.TryGetProperty("isBotActive", out var botActive) ? botActive.GetBoolean() : true;
            viewModel.PlanType = tenantObj.TryGetProperty("planType", out var plan) ? plan.GetString() ?? "Trial" : "Trial";
            viewModel.BillingCycle = tenantObj.TryGetProperty("billingCycle", out var cycle) ? cycle.GetString() ?? "Monthly" : "Monthly";
            viewModel.IsTrial = tenantObj.TryGetProperty("isTrial", out var trial) && trial.GetBoolean();
            viewModel.SubscriptionEndDate = tenantObj.TryGetProperty("subscriptionEndDate", out var subEnd) ? subEnd.GetDateTime() : DateTime.Now;
            viewModel.SubscriptionStatusLabel = tenantObj.TryGetProperty("subscriptionStatusLabel", out var statusLbl) ? statusLbl.GetString() ?? "" : "";
            viewModel.HasPendingPlanChange = tenantObj.TryGetProperty("hasPendingPlanChange", out var pending) && pending.GetBoolean();
            viewModel.HasPendingCheckout = tenantObj.TryGetProperty("hasPendingCheckout", out var pendingCheckout) && pendingCheckout.GetBoolean();
            viewModel.HasScheduledPlanActivation = tenantObj.TryGetProperty("hasScheduledPlanActivation", out var scheduled) && scheduled.GetBoolean();
            viewModel.PendingPlanDisplayLabel = tenantObj.TryGetProperty("pendingPlanDisplayLabel", out var pendingLbl)
                ? pendingLbl.GetString()
                : null;
            if (tenantObj.TryGetProperty("pendingPlanEffectiveDate", out var effectiveDate)
                && effectiveDate.ValueKind != JsonValueKind.Null)
            {
                viewModel.PendingPlanEffectiveDate = effectiveDate.GetDateTime();
            }
            viewModel.CancelAtPeriodEnd = tenantObj.TryGetProperty("cancelAtPeriodEnd", out var cancelEnd) && cancelEnd.GetBoolean();
            viewModel.DaysRemaining = tenantObj.TryGetProperty("daysRemaining", out var daysRem) ? daysRem.GetInt32() : 0;
            viewModel.DaysRemainingLabel = tenantObj.TryGetProperty("daysRemainingLabel", out var daysLbl)
                ? daysLbl.GetString() ?? ""
                : $"{viewModel.DaysRemaining} gün kaldı";
            if (tenantObj.TryGetProperty("scheduledNewPlanDays", out var newDays)
                && newDays.ValueKind != JsonValueKind.Null)
            {
                viewModel.ScheduledNewPlanDays = newDays.GetInt32();
            }
            viewModel.TotalAccessDays = tenantObj.TryGetProperty("totalAccessDays", out var totalDays)
                ? totalDays.GetInt32()
                : viewModel.DaysRemaining;

            if (tenantObj.TryGetProperty("businessHours", out var bh) && bh.ValueKind == JsonValueKind.Array)
            {
                viewModel.BusinessHours = JsonSerializer.Deserialize<List<dynamic>>(
                    bh.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            viewModel.BreakTimeEnabled = tenantObj.TryGetProperty("breakTimeEnabled", out var bte) && bte.GetBoolean();
            if (tenantObj.TryGetProperty("breakStartTime", out var bst))
            {
                var s = bst.GetString();
                if (!string.IsNullOrWhiteSpace(s) && s.Length >= 5)
                    viewModel.BreakStartTime = s.Length > 5 ? s.Substring(0, 5) : s;
            }
            if (tenantObj.TryGetProperty("breakEndTime", out var bet))
            {
                var e = bet.GetString();
                if (!string.IsNullOrWhiteSpace(e) && e.Length >= 5)
                    viewModel.BreakEndTime = e.Length > 5 ? e.Substring(0, 5) : e;
            }
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
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var doc = JsonSerializer.Deserialize<JsonElement>(body);
                    if (doc.TryGetProperty("message", out var msg))
                        return (true, msg.GetString() ?? "Abonelik yenilemesi iptal edildi.");
                }
                catch
                {
                    // ignore parse errors
                }
                return (true, "Abonelik yenilemesi iptal edildi. Ödenmiş dönem sonuna kadar kullanmaya devam edebilirsiniz.");
            }
            return (false, body);
        }
    }
}
