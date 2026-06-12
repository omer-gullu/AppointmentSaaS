namespace Appointment_SaaS.Core.DTOs
{
    public class AddStaffDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Specialization { get; set; }

        /// <summary>
        /// Personelin Google Takvim ID'si.
        /// Genellikle Gmail adresi (örn: ahmet@gmail.com) ya da
        /// Google Takvim > Ayarlar > Takvim ID kısmından kopyalanır.
        /// </summary>
        public string? GoogleCalendarId { get; set; }
    }

    public class UpdateStaffDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Specialization { get; set; }
        public string? GoogleCalendarId { get; set; }
        public bool Status { get; set; } = true;
    }
}