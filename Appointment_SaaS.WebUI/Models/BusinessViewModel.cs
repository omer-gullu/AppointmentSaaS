namespace Appointment_SaaS.WebUI.Models
{
    public class BusinessListViewModel
    {
        public int TenantID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? InstanceName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime SubscriptionEndDate { get; set; }
        public int SectorID { get; set; }
    }

    public class BusinessEditViewModel
    {
        public int TenantID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? InstanceName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime SubscriptionEndDate { get; set; }
    }
}
