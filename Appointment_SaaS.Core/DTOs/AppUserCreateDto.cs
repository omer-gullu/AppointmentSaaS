using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs
{
    public class AppUserCreateDto
    {

        public int TenantID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // Bunlar byte[] olmalı, çünkü veritabanında binary (varbinary) tutulur
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }

        public string Specialization { get; set; }
        public bool Status { get; set; } = true; // Kullanıcı aktif mi?
    }
}
