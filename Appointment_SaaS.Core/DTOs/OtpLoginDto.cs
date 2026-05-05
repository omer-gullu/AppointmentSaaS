namespace Appointment_SaaS.Core.DTOs
{
    public class OtpLoginDto
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class OtpVerifyDto
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }
}
