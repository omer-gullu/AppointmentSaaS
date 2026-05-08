using Appointment_SaaS.Business.Abstract;
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
                            "SEND_MESSAGE",
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
                            "SEND_MESSAGE",
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
            // Numara formatlama: 0531... -> 90531...
            var formattedNumber = toPhoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            if (formattedNumber.StartsWith("0"))
                formattedNumber = "90" + formattedNumber[1..];
            if (!formattedNumber.StartsWith("90") && formattedNumber.Length == 10)
                formattedNumber = "90" + formattedNumber;

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

    public async Task<string?> GetQrCodeAsync(string instanceName)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _settings.GlobalApiKey);

            var response = await _httpClient.GetAsync($"/instance/connect/{Uri.EscapeDataString(instanceName)}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[EvolutionApi] GetQrCodeAsync başarısız. Instance={Instance} Status={Status}",
                    instanceName, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("base64", out var base64Prop))
                return base64Prop.GetString();

            _logger.LogInformation("[EvolutionApi] QR base64 alanı bulunamadı. Instance={Instance}", instanceName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EvolutionApi] GetQrCodeAsync exception. Instance={Instance}", instanceName);
            return null;
        }
    }

    public async Task<bool> SendOtpMessageAsync(string instanceName, string toPhoneNumber, string otpCode)
    {
        var messageBody = $"*Akıllı Randevu Doğrulama*\n\nGiriş kodunuz: *{otpCode}*\n\nBu kod *45 saniye* geçerlidir. Kimseyle paylaşmayınız.";
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