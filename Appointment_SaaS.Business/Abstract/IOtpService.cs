namespace Appointment_SaaS.Business.Abstract
{
    public interface IOtpService
    {
        string GenerateOtp(string phoneNumber);
        bool VerifyOtp(string phoneNumber, string code);
    }
}
