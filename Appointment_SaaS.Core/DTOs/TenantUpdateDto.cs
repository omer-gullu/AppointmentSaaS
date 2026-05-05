namespace Appointment_SaaS.Core.DTOs
{
    public class TenantUpdateDto
    {
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? InstanceName { get; set; }
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime SubscriptionEndDate { get; set; }
    }
}
