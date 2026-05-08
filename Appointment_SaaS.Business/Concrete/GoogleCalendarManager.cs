using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.DataAccess.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Appointment_SaaS.Business.Concrete;

public class GoogleCalendarManager : IGoogleCalendarService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCalendarManager> _logger;
   

    public GoogleCalendarManager(
        IAppUserRepository appUserRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleCalendarManager> logger)
      
    {
        _appUserRepository = appUserRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;



    }

    private async Task<string?> GetAccessTokenAsync(int appUserId)
    {
        var user = await _appUserRepository.Where(u => u.AppUserID == appUserId).FirstOrDefaultAsync();
        if (user == null || string.IsNullOrEmpty(user.GoogleRefreshToken))
        {
            _logger.LogWarning("[GoogleCalendar] Personelin Google hesabı bağlı değil. AppUserID={Id}", appUserId);
            return null;
        }

        var clientId = _configuration["Google:ClientId"];
        var clientSecret = _configuration["Google:ClientSecret"];

        var httpClient = _httpClientFactory.CreateClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId! },
            { "client_secret", clientSecret! },
            { "refresh_token", user.GoogleRefreshToken },
            { "grant_type", "refresh_token" }
        });

        var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[GoogleCalendar] Token alınamadı. AppUserID={Id} Body={Body}", appUserId, body);
            return null;
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(body);
        return tokenData.GetProperty("access_token").GetString();
    }

    public async Task<string?> AddEventAsync(int appUserId, string summary, string description, DateTime start, DateTime end)
    {
        try
        {
            var user = await _appUserRepository.Where(u => u.AppUserID == appUserId).FirstOrDefaultAsync();
            if (user == null || string.IsNullOrEmpty(user.GoogleCalendarId)) return null;

            var accessToken = await GetAccessTokenAsync(appUserId);
            if (accessToken == null) return null;

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var eventBody = new
            {
                summary,
                description,
                start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" },
                end = new { dateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" }
            };

            var content = new StringContent(JsonSerializer.Serialize(eventBody), Encoding.UTF8, "application/json");
            var calendarId = Uri.EscapeDataString(user.GoogleCalendarId);
            var response = await httpClient.PostAsync($"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[GoogleCalendar] Event eklenemedi. Body={Body}", body);
                return null;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(body);
            return result.GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleCalendar] AddEventAsync hatası.");
            return null;
        }
    }

    public async Task<bool> UpdateEventAsync(int appUserId, string googleEventId, string summary, string description, DateTime start, DateTime end)
    {
        try
        {
            var user = await _appUserRepository.Where(u => u.AppUserID == appUserId).FirstOrDefaultAsync();
            if (user == null || string.IsNullOrEmpty(user.GoogleCalendarId)) return false;

            var accessToken = await GetAccessTokenAsync(appUserId);
            if (accessToken == null) return false;

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var eventBody = new
            {
                summary,
                description,
                start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" },
                end = new { dateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "Europe/Istanbul" }
            };

            var content = new StringContent(JsonSerializer.Serialize(eventBody), Encoding.UTF8, "application/json");
            var calendarId = Uri.EscapeDataString(user.GoogleCalendarId);
            var response = await httpClient.PutAsync($"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events/{googleEventId}", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[GoogleCalendar] Event güncellenemedi. Body={Body}", body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleCalendar] UpdateEventAsync hatası.");
            return false;
        }
    }

    public async Task DeleteEventAsync(int appUserId, string googleEventId)
    {
        try
        {
            var user = await _appUserRepository.Where(u => u.AppUserID == appUserId).FirstOrDefaultAsync();
            if (user == null || string.IsNullOrEmpty(user.GoogleCalendarId)) return;

            var accessToken = await GetAccessTokenAsync(appUserId);
            if (accessToken == null) return;

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var calendarId = Uri.EscapeDataString(user.GoogleCalendarId);
            await httpClient.DeleteAsync($"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events/{googleEventId}?sendUpdates=all");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleCalendar] DeleteEventAsync hatası.");
        }
    }
}