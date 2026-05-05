using System;

namespace Appointment_SaaS.Core.DTOs
{
    public class BusinessHourDto
    {
        public int DayOfWeek { get; set; }
        public string OpenTime { get; set; } = "09:00";
        public string CloseTime { get; set; } = "18:00";
        public bool IsClosed { get; set; }
    }
}
