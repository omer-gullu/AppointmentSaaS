namespace Appointment_SaaS.Business.Abstract;

public interface IEvolutionApiService
{
    Task<bool> CreateInstanceAsync(string instanceName);
    Task<bool> ConnectInstanceAsync(string instanceName);
    Task<bool> DisconnectInstanceAsync(string instanceName);
    Task<bool> DeleteInstanceAsync(string instanceName);
    Task<bool> SendWhatsAppMessageAsync(string instanceName, string toPhoneNumber, string message);
    Task<bool> SendOtpMessageAsync(string instanceName, string toPhoneNumber, string otpCode);
    Task<string?> GetQrCodeAsync(string instanceName);
    /// <summary>Evolution API'ye gerçek zamanlı bağlantı durumu sorar. 'open' state = true.</summary>
    Task<bool> IsInstanceConnectedAsync(string instanceName);
}
