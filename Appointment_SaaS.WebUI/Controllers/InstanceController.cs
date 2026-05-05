using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Appointment_SaaS.Business.Abstract;

namespace Appointment_SaaS.WebUI.Controllers
{
    public class InstanceController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IEvolutionApiService _evolutionApiService;

        public InstanceController(IHttpClientFactory httpClientFactory, IEvolutionApiService evolutionApiService)
        {
            _httpClient = httpClientFactory.CreateClient("Api");
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _evolutionApiService = evolutionApiService;
        }

        // GET: /Instance
        public async Task<IActionResult> Index()
        {
            // Kullanıcı dükkan sahibi mi?
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            
            if (int.TryParse(tenantIdClaim, out int tenantId))
            {
                // DÜKKAN SAHİBİ EKRANI: Sadece kendi instance'ının QR kodunu göster
                try
                {
                    // 1. Önce API'den bu dükkanın instance adını çek
                    var tenantResponse = await _httpClient.GetAsync($"api/Tenants/{tenantId}");
                    if (tenantResponse.IsSuccessStatusCode)
                    {
                        var tenantJson = await tenantResponse.Content.ReadAsStringAsync();
                        var tenantObj = JsonSerializer.Deserialize<JsonElement>(tenantJson);
                        string instanceName = tenantObj.GetProperty("instanceName").GetString();

                        // 2. Evolution API üzerinden QR kod al
                        ViewBag.InstanceName = instanceName;
                        var qrCodeBase64 = await _evolutionApiService.GetQrCodeAsync(instanceName);
                        
                        if (!string.IsNullOrEmpty(qrCodeBase64))
                        {
                            ViewBag.QrCode = qrCodeBase64;
                        }
                        else
                        {
                            ViewBag.Info = "Instance şu an bağlı veya QR kod üretilemedi. Eğer zaten bağlıysa işlem yapmanıza gerek yoktur.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.Error = $"Sistem hatası: {ex.Message}";
                }

                return View("ShowQr"); // Yeni bir view dönelim
            }

            // ADMIN EKRANI: Tüm işletmeleri listele (Mevcut mantık)
            try
            {
                var response = await _httpClient.GetAsync("api/Tenants");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(json);
                    
                    List<InstanceListViewModel> tenants = new();
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        tenants = JsonSerializer.Deserialize<List<InstanceListViewModel>>(json, _jsonOptions) ?? new();
                    }
                    else if (document.RootElement.TryGetProperty("value", out var valueProp))
                    {
                        tenants = JsonSerializer.Deserialize<List<InstanceListViewModel>>(valueProp.GetRawText(), _jsonOptions) ?? new();
                    }

                    return View(tenants);
                }
            }
            catch { }

            return View(new List<InstanceListViewModel>());
        }

        // GET: /Instance/Create
        public async Task<IActionResult> Create()
        {
            var model = new InstanceCreateViewModel();

            try
            {
                var response = await _httpClient.GetAsync("api/Sector");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(json, _jsonOptions) ?? new();
                    }
                    else if (document.RootElement.TryGetProperty("value", out var valueProp))
                    {
                        model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(valueProp.GetRawText(), _jsonOptions) ?? new();
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Sektör listesi alınamadı: {ex.Message}";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstanceCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                try
                {
                    var sectorResponse = await _httpClient.GetAsync("api/Sector");
                    if (sectorResponse.IsSuccessStatusCode)
                    {
                        var json = await sectorResponse.Content.ReadAsStringAsync();
                        using var document = JsonDocument.Parse(json);
                        if (document.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(json, _jsonOptions) ?? new();
                        }
                        else if (document.RootElement.TryGetProperty("value", out var valueProp))
                        {
                            model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(valueProp.GetRawText(), _jsonOptions) ?? new();
                        }
                    }
                }
                catch { }

                return View(model);
            }

            try
            {
                var payload = new
                {
                    userFullName = model.UserFullName,
                    userEmail = model.UserEmail,
                    businessName = model.Name,
                    sectorID = model.SectorID,
                    phoneNumber = model.PhoneNumber,
                    address = model.Address,

                    cardHolderName = model.CardHolderName,
                    cardNumber = model.CardNumber,
                    expireMonth = model.ExpireMonth,
                    expireYear = model.ExpireYear,
                    cvc = model.Cvc
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("api/Auth/register-business", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "İşletme başarıyla oluşturuldu! Şimdi telefon numaranız ile giriş yapabilirsiniz.";
                    return RedirectToAction("Login", "Auth");
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                string errorMessage = errorBody;
                try
                {
                    var errJson = JsonDocument.Parse(errorBody);
                    if (errJson.RootElement.TryGetProperty("message", out var msgProp))
                        errorMessage = msgProp.GetString() ?? errorBody;
                }
                catch { }
                ViewBag.Error = $"İşletme oluşturulamadı: {errorMessage}";
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"API bağlantı hatası: {ex.Message}";
            }

            // Sectors reload...
            try
            {
                var sectorResponse2 = await _httpClient.GetAsync("api/Sector");
                if (sectorResponse2.IsSuccessStatusCode)
                {
                    var sJson = await sectorResponse2.Content.ReadAsStringAsync();
                    using var sDoc = JsonDocument.Parse(sJson);
                    if (sDoc.RootElement.ValueKind == JsonValueKind.Array)
                        model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(sJson, _jsonOptions) ?? new();
                    else if (sDoc.RootElement.TryGetProperty("value", out var sVal))
                        model.Sectors = JsonSerializer.Deserialize<List<SectorItem>>(sVal.GetRawText(), _jsonOptions) ?? new();
                }
            }
            catch { }
            return View(model);
        }
    }
}
