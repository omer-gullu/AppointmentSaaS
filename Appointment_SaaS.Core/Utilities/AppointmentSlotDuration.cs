namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// available-slots süresi: hizmet ID toplamı öncelikli, sessiz 30 yok.
/// </summary>
public static class AppointmentSlotDuration
{
    public readonly record struct CatalogItem(int ServiceId, int DurationInMinutes);

    public static List<int> ParseServiceIds(int? serviceId, string? serviceIds)
    {
        var ids = new List<int>();
        if (!string.IsNullOrWhiteSpace(serviceIds))
        {
            var cleaned = serviceIds.Trim().Trim('[', ']');
            foreach (var part in cleaned.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id) && id > 0 && !ids.Contains(id))
                    ids.Add(id);
            }
        }

        if (ids.Count == 0 && serviceId is > 0)
            ids.Add(serviceId.Value);

        return ids;
    }

    public static (int Minutes, string? Error, string Source) Resolve(
        int? durationMinutes,
        int? serviceId,
        string? serviceIds,
        IEnumerable<CatalogItem>? catalog)
    {
        var ids = ParseServiceIds(serviceId, serviceIds);
        if (ids.Count > 0)
        {
            var map = (catalog ?? Array.Empty<CatalogItem>())
                .GroupBy(c => c.ServiceId)
                .ToDictionary(g => g.Key, g => g.First().DurationInMinutes);
            if (map.Count == 0)
                return (0, "Hizmet listesi yüklenemedi.", "none");

            var total = 0;
            foreach (var id in ids)
            {
                if (!map.TryGetValue(id, out var minutes))
                    return (0, $"Hizmet bulunamadı veya bu işletmeye ait değil (ServiceID={id}).", "none");
                total += minutes;
            }

            return total > 0
                ? (total, null, "serviceIds")
                : (0, "Seçilen hizmetlerin süresi geçersiz.", "none");
        }

        if (durationMinutes is > 0)
            return (durationMinutes.Value, null, "durationMinutes");

        return (0, "durationMinutes veya serviceID/serviceIds gereklidir.", "none");
    }
}
