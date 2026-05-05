namespace Appointment_SaaS.WebUI.Models
{
    public class InstanceListViewModel
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
    }

    public class InstanceCreateViewModel
    {
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? InstanceName { get; set; }
        public int SectorID { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        // Payment (kart) - yalnızca kayıt sırasında API'ye iletilir
        public string CardHolderName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string ExpireMonth { get; set; } = string.Empty;
        public string ExpireYear { get; set; } = string.Empty;
        public string Cvc { get; set; } = string.Empty;

        public List<SectorItem> Sectors { get; set; } = new();
    }

    public class SectorItem
    {
        public int SectorID { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
