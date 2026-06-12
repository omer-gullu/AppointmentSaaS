using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Appointment_SaaS.Business.Concrete;

public class EvolutionApiManager : IEvolutionApiService
{
    private readonly HttpClient _httpClient;
    private readonly EvolutionApiSettings _settings;
    private readonly ILogger<EvolutionApiManager> _logger;

    public EvolutionApiManager(
        HttpClient httpClient,
        IOptions<EvolutionApiSettings> options,
        ILogger<EvolutionApiManager> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<bool> CreateInstanceAsync(string instanceName)
    {
        try
        {
            var createPayload = new
            {
                instanceName,
                qrcode = true,
                token = Guid.NewGuid().ToString()[..10].ToLower(),
                integration = "WHATSAPP-BAILEYS"
            };

            var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var createResponse = await _httpClient.PostAsync("/instance/create", createContent);
            if (!createResponse.IsSuccessStatusCode) return false;

            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                var webhookPayload = new
                {
                    webhook = new
                    {
                        enabled = true,
                        url = _settings.WebhookUrl,
                        webhookByEvents = false,
                        webhookBase64 = true,
                        events = new[]
                        {
                            "MESSAGES_UPSERT",
                            "MESSAGES_UPDATE",
                            "MESSAGES_DELETE",
                            "QRCODE_UPDATED",
                            "CONNECTION_UPDATE"
                        }
                    }
                };

                var webhookContent = new StringContent(JsonSerializer.Serialize(webhookPayload), Encoding.UTF8, "application/json");
                var webhookResponse = await _httpClient.PostAsync($"/webhook/set/{instanceName}", webhookContent);

                if (!webhookResponse.IsSuccessStatusCode)
                {
                    var body = await webhookResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("[EvolutionApi] Webhook set başarısız. Instance={Instance} Status={Status} Body={Body}",
                        instanceName, webhookResponse.StatusCode, body);
                }
            }

            var settingsPayload = new
            {
                rejectCall = false,
                msgCall = "",
                groupsIgnore = true,
                alwaysOnline = true,
                readMessages = false,
                readStatus = false,
                syncFullHistory = false
            };

            var settingsContent = new StringContent(JsonSerializer.Serialize(settingsPayload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"/settings/set/{instanceName}", settingsContent);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] CreateInstanceAsync başarısız. Instance={Instance}", instanceName);
            return false;
        }
    }

    public async Task<bool> ConnectInstanceAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.GetAsync($"/instance/connect/{Uri.EscapeDataString(instanceName)}");
            if (!response.IsSuccessStatusCode) return false;

            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                var webhookPayload = new
                {
                    webhook = new
                    {
                        enabled = true,
                        url = _settings.WebhookUrl,
                        webhookByEvents = false,
                        webhookBase64 = false,
                        events = new[]
                        {
                            "MESSAGES_UPSERT",
                            "MESSAGES_UPDATE",
                            "MESSAGES_DELETE",
                            "QRCODE_UPDATED",
                            "CONNECTION_UPDATE"
                        }
                    }
                };

                var webhookContent = new StringContent(JsonSerializer.Serialize(webhookPayload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"/webhook/set/{Uri.EscapeDataString(instanceName)}", webhookContent);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] ConnectInstanceAsync başarısız. Instance={Instance}", instanceName);
            return false;
        }
    }

    /// <summary>
    /// /instance/connect/ endpoint'ini TEK SEFERDE çağırır: hem bağlantıyı başlatır
    /// hem dönen response'dan QR base64'ü okur. Webhook da bu sırada ayarlanır.
    /// </summary>
    public async Task<string?> ConnectAndGetQrAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.GetAsync($"/instance/connect/{Uri.EscapeDataString(instanceName)}");

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                "[EvolutionApi][ConnectAndGetQr] Instance={Instance} Status={Status} Body={Body}",
                instanceName, (int)response.StatusCode, json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[EvolutionApi] ConnectAndGetQrAsync başarısız. Instance={Instance} Status={Status}",
                    instanceName, response.StatusCode);
                return null;
            }

            // Webhook'u arka planda ayarla (QR dönüşünü engellemez)
            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var wh = new StringContent(JsonSerializer.Serialize(new
                        {
                            webhook = new
                            {
                                enabled = true,
                                url = _settings.WebhookUrl,
                                webhookByEvents = false,
                                webhookBase64 = false,
                                events = new[] { "MESSAGES_UPSERT", "MESSAGES_UPDATE", "MESSAGES_DELETE", "QRCODE_UPDATED", "CONNECTION_UPDATE" }
                            }
                        }), Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync($"/webhook/set/{Uri.EscapeDataString(instanceName)}", wh);
                    }
                    catch { /* Webhook hatası QR akışını etkilemez */ }
                });
            }

            // QR verisini JSON'dan çıkar (3 farklı yapı denenir)
            var doc = JsonDocument.Parse(json);

            // Yapı 1: { "base64": "data:image/..." }
            if (doc.RootElement.TryGetProperty("base64", out var b64) && !string.IsNullOrEmpty(b64.GetString()))
                return b64.GetString();

            // Yapı 2: { "qrcode": { "base64": "..." } }
            if (doc.RootElement.TryGetProperty("qrcode", out var qrObj) &&
                qrObj.TryGetProperty("base64", out var nested) && !string.IsNullOrEmpty(nested.GetString()))
                return nested.GetString();

            // Yapı 3: { "code": "..." }
            if (doc.RootElement.TryGetProperty("code", out var code) && !string.IsNullOrEmpty(code.GetString()))
                return code.GetString();

            _logger.LogWarning("[EvolutionApi] ConnectAndGetQrAsync: QR alanı bulunamadı. Body={Body}", json);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] ConnectAndGetQrAsync exception. Instance={Instance}", instanceName);
            return null;
        }
    }

    public async Task<bool> DisconnectInstanceAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                var webhookPayload = new
                {
                    webhook = new
                    {
                        enabled = false,
                        url = _settings.WebhookUrl,
                        webhookByEvents = false,
                        webhookBase64 = true,
                        events = new[]
                        {
                            "MESSAGES_UPSERT",
                            "MESSAGES_UPDATE",
                            "MESSAGES_DELETE",
                            "SEND_MESSAGE",
                            "QRCODE_UPDATED",
                            "CONNECTION_UPDATE"
                        }
                    }
                };

                var webhookContent = new StringContent(JsonSerializer.Serialize(webhookPayload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"/webhook/set/{instanceName}", webhookContent);
            }

            var response = await _httpClient.DeleteAsync($"/instance/logout/{instanceName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] DisconnectInstanceAsync başarısız. Instance={Instance}", instanceName);
            return false;
        }
    }

    public async Task<bool> DeleteInstanceAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.DeleteAsync($"/instance/delete/{instanceName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] DeleteInstanceAsync başarısız. Instance={Instance}", instanceName);
            return false;
        }
    }

    public async Task<bool> SendWhatsAppMessageAsync(string instanceName, string toPhoneNumber, string message)
    {
        try
        {
            var formattedNumber = FormatPhoneForWhatsApp(toPhoneNumber);
            if (IsSandboxPhone(formattedNumber))
            {
                _logger.LogInformation(
                    "[EvolutionApi] Test/sandbox numara — WhatsApp gönderilmedi. Instance={Instance} To={To}",
                    instanceName, formattedNumber);
                return true;
            }

            if (!IsAllowedWhatsAppRecipient(formattedNumber))
            {
                _logger.LogInformation(
                    "[EvolutionApi] İzin listesi dışı numara — WhatsApp gönderilmedi. Instance={Instance} To={To}",
                    instanceName, formattedNumber);
                return true;
            }

            var payload = new
            {
                number = formattedNumber,
                text = message,
                delay = 1200,
                linkPreview = false
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Headers temizliği ve yeniden ekleme (Önemli: apikey her zaman taze olmalı)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.PostAsync($"/message/sendText/{Uri.EscapeDataString(instanceName)}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[EvolutionApi] Mesaj gönderilemedi. Instance={Instance} To={To} Status={Status} Body={Body}",
                    instanceName, formattedNumber, response.StatusCode, errorBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] SendWhatsAppMessageAsync exception. Instance={Instance}", instanceName);
            return false;
        }
    }

    private static string FormatPhoneForWhatsApp(string toPhoneNumber)
    {
        var formattedNumber = toPhoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (formattedNumber.StartsWith("0"))
            formattedNumber = "90" + formattedNumber[1..];
        if (!formattedNumber.StartsWith("90") && formattedNumber.Length == 10)
            formattedNumber = "90" + formattedNumber;
        return formattedNumber;
    }

    private bool IsSandboxPhone(string formattedNumber90)
    {
        if (_settings.SandboxPhonePrefixes == null || _settings.SandboxPhonePrefixes.Count == 0)
            return false;

        foreach (var raw in _settings.SandboxPhonePrefixes)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var prefix = FormatPhoneForWhatsApp(raw);
            if (formattedNumber90.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>EnforceWhatsAppAllowlist=false veya liste boşsa tüm (sandbox dışı) numaralara izin.</summary>
    private bool IsAllowedWhatsAppRecipient(string formattedNumber90)
    {
        if (!_settings.EnforceWhatsAppAllowlist)
            return true;

        if (_settings.AllowedWhatsAppNumbers == null || _settings.AllowedWhatsAppNumbers.Count == 0)
        {
            _logger.LogWarning(
                "[EvolutionApi] EnforceWhatsAppAllowlist=true ancak AllowedWhatsAppNumbers boş — mesaj gönderilmedi. To={To}",
                formattedNumber90);
            return false;
        }

        foreach (var raw in _settings.AllowedWhatsAppNumbers)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (formattedNumber90 == FormatPhoneForWhatsApp(raw))
                return true;
        }

        return false;
    }

    public async Task<string?> GetQrCodeAsync(string instanceName)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 2000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

                var response = await _httpClient.GetAsync($"/instance/connect/{Uri.EscapeDataString(instanceName)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "[EvolutionApi] GetQrCodeAsync başarısız. Instance={Instance} Status={Status} Deneme={Attempt}/{Max}",
                        instanceName, response.StatusCode, attempt, maxRetries);
                }
                else
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // ── TEŞHİS: Ham JSON yanıtını logla (sorun giderildikten sonra kaldırılabilir) ──
                    _logger.LogInformation(
                        "[EvolutionApi][DIAGNOSE] /instance/connect/ yanıtı. Instance={Instance} Deneme={Attempt} RawJson={Json}",
                        instanceName, attempt, json);

                    var doc = JsonDocument.Parse(json);

                    // Evolution API v2: { "base64": "data:image/png;base64,..." }
                    if (doc.RootElement.TryGetProperty("base64", out var base64Prop))
                    {
                        var base64 = base64Prop.GetString();
                        if (!string.IsNullOrEmpty(base64))
                        {
                            _logger.LogInformation(
                                "[EvolutionApi] QR kodu alındı (base64). Instance={Instance} Deneme={Attempt}/{Max}",
                                instanceName, attempt, maxRetries);
                            return base64;
                        }
                    }

                    // Evolution API v2 alternatif: { "qrcode": { "base64": "..." } }
                    if (doc.RootElement.TryGetProperty("qrcode", out var qrcodeProp) &&
                        qrcodeProp.TryGetProperty("base64", out var nested64))
                    {
                        var base64 = nested64.GetString();
                        if (!string.IsNullOrEmpty(base64))
                        {
                            _logger.LogInformation(
                                "[EvolutionApi] QR kodu alındı (qrcode.base64). Instance={Instance} Deneme={Attempt}/{Max}",
                                instanceName, attempt, maxRetries);
                            return base64;
                        }
                    }

                    // Evolution API v1: { "code": "..." }
                    if (doc.RootElement.TryGetProperty("code", out var codeProp))
                    {
                        var code = codeProp.GetString();
                        if (!string.IsNullOrEmpty(code))
                        {
                            _logger.LogInformation(
                                "[EvolutionApi] QR kodu alındı (code). Instance={Instance} Deneme={Attempt}/{Max}",
                                instanceName, attempt, maxRetries);
                            return code;
                        }
                    }

                    _logger.LogWarning(
                        "[EvolutionApi] Hiçbir QR alanı bulunamadı (base64/qrcode.base64/code). Instance={Instance} Deneme={Attempt}/{Max} Json={Json}",
                        instanceName, attempt, maxRetries, json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[EvolutionApi] GetQrCodeAsync exception. Instance={Instance} Deneme={Attempt}/{Max}",
                    instanceName, attempt, maxRetries);
            }

            if (attempt < maxRetries)
                await Task.Delay(retryDelayMs);
        }

        _logger.LogWarning(
            "[EvolutionApi] {Max} denemeden sonra QR alınamadı. Instance={Instance}",
            maxRetries, instanceName);
        return null;
    }

    public async Task<bool> SendOtpMessageAsync(string instanceName, string toPhoneNumber, string otpCode)
    {
        var messageBody = $"*Akıllı Randevu Doğrulama*\n\nGiriş kodunuz: *{otpCode}*\n\nBu kod *{OtpLoginSettings.ValidityDisplayText}* geçerlidir. Kimseyle paylaşmayınız.";
        return await SendWhatsAppMessageAsync(instanceName, toPhoneNumber, messageBody);
    }

    public async Task<bool> IsInstanceConnectedAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.GetAsync($"/instance/connectionState/{Uri.EscapeDataString(instanceName)}");
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            // Evolution API v2
            if (doc.RootElement.TryGetProperty("instance", out var instanceProp) &&
                instanceProp.TryGetProperty("state", out var stateProp))
                return stateProp.GetString()?.Equals("open", StringComparison.OrdinalIgnoreCase) == true;

            // Evolution API v1 fallback
            if (doc.RootElement.TryGetProperty("state", out var stateV1))
                return stateV1.GetString()?.Equals("open", StringComparison.OrdinalIgnoreCase) == true;

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] IsInstanceConnectedAsync exception. Instance={Instance}", instanceName);
            return false;
        }
    }
}