using Appointment_SaaS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Abstract
{
    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        /// <summary>
        /// Verilen tenant kapsamında, telefon varyasyonlarından herhangi birine eşit
        /// CustomerPhone'u olan ve <paramref name="nowLocal"/> anına göre henüz bitmemiş
        /// (EndDate &gt;= nowLocal) randevuları Service / AppointmentServiceLinks /
        /// AppUser Include'larıyla getirir. İptal statüsü filtresi servis katmanında
        /// uygulanır (Türkçe lower-case karşılaştırma SQL-translatable değil).
        /// </summary>
        Task<List<Appointment>> GetActiveByPhoneAsync(
            int tenantId,
            IReadOnlyCollection<string> phoneCandidates,
            DateTime nowLocal);
    }
}
