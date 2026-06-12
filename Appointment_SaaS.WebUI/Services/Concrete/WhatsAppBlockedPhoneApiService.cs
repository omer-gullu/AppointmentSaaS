using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appointment_SaaS.WebUI.Services.Concrete;

public class WhatsAppBlockedPhoneApiService : IWhatsAppBlockedPhoneApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<WhatsAppBlockedPhoneApiService> _logger;

    public WhatsAppBlockedPhoneApiService(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<WhatsAppBlockedPhoneApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        var client = _httpClientFactory.CreateClient("Api");
        await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);
        return client;
    }

    public async Task<(List<TenantBlockedPhoneDto> Items, string? ErrorMessage)> GetListAsync()
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.GetAsync("api/WhatsAppBlockedPhones");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gri liste alınamadı. Status={Status}", response.StatusCode);
                var err = await ReadErrorMessageAsync(response);
                return (new List<TenantBlockedPhoneDto>(), err);
            }

            var list = await response.Content.ReadFromJsonAsync<List<TenantBlockedPhoneDto>>(_jsonOptions);
            return (list ?? new List<TenantBlockedPhoneDto>(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gri liste getirilirken hata.");
            return (new List<TenantBlockedPhoneDto>(), "Gri liste yüklenemedi. Lütfen tekrar deneyin.");
        }
    }

    public async Task<(bool Success, string Message, TenantBlockedPhoneDto? Item)> AddAsync(string phone, string? note)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.PostAsJsonAsync("api/WhatsAppBlockedPhones", new TenantBlockedPhoneCreateDto
            {
                Phone = phone,
                Note = note
            });

            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<TenantBlockedPhoneDto>(_jsonOptions);
                return (true, "Numara gri listeye eklendi.", item);
            }

            var err = await ReadErrorMessageAsync(response);
            return (false, err, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gri listeye ekleme hatası.");
            return (false, "Bağlantı hatası. Lütfen tekrar deneyin.", null);
        }
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        try
        {
            var client = await CreateClientAsync();
            var response = await client.DeleteAsync($"api/WhatsAppBlockedPhones/{id}");
            if (response.IsSuccessStatusCode)
                return (true, "Numara listeden kaldırıldı.");

            var err = await ReadErrorMessageAsync(response);
            return (false, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gri listeden silme hatası.");
            return (false, "Bağlantı hatası. Lütfen tekrar deneyin.");
        }
    }

    private async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "İşlem başarısız.";
        }
        catch { /* ignore */ }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => "Geçersiz telefon numarası veya istek.",
            System.Net.HttpStatusCode.Unauthorized => "Oturum süreniz dolmuş olabilir. Tekrar giriş yapın.",
            System.Net.HttpStatusCode.NotFound => "Kayıt bulunamadı.",
            _ => "İşlem başarısız."
        };
    }
}
