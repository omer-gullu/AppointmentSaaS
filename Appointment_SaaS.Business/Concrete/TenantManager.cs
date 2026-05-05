using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.Business.Concrete
{
    public class TenantManager : ITenantService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IMapper _mapper;
        private readonly IEvolutionApiService _evolutionApiService;
        private readonly AppDbContext _db;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<TenantManager> _logger;

        public TenantManager(
            ITenantRepository tenantRepository,
            IMapper mapper,
            IEvolutionApiService evolutionApiService,
            AppDbContext db,
            IHostEnvironment environment,
            ILogger<TenantManager> logger)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
            _evolutionApiService = evolutionApiService;
            _db = db;
            _environment = environment;
            _logger = logger;
        }

        public async Task<Tenant?> GetByApiKeyAsync(string apiKey)
        {
            return await _tenantRepository.Where(x => x.ApiKey == apiKey && x.IsActive).FirstOrDefaultAsync();
        }

        public async Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

            var cleanPhone = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (cleanPhone.StartsWith("90") && cleanPhone.Length > 10) cleanPhone = cleanPhone.Substring(2);
            if (cleanPhone.StartsWith("0") && cleanPhone.Length > 10) cleanPhone = cleanPhone.Substring(1);

            return await _tenantRepository.Where(x => x.IsActive && x.PhoneNumber == cleanPhone).FirstOrDefaultAsync();
        }

        public async Task<Tenant?> GetContextByInstanceAsync(string instanceName)
        {
            return await _tenantRepository
                .Where(x => x.InstanceName == instanceName && x.IsActive)
                .Include(x => x.Services)
                .Include(x => x.BusinessHours)
                .Include(x => x.AppUsers)
                .FirstOrDefaultAsync();
        }

        public async Task<Tenant?> GetBySubscriptionReferenceAsync(string referenceCode)
        {
            return await _tenantRepository
                .Where(x => x.SubscriptionReferenceCode == referenceCode)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateSubscriptionStatusAsync(Tenant tenant, bool isActive)
        {
            tenant.IsActive = isActive;
            tenant.IsSubscriptionActive = isActive;
            _tenantRepository.Update(tenant);

            if (!isActive)
            {
                var users = await _db.AppUsers.Where(u => u.TenantID == tenant.TenantID).ToListAsync();
                foreach (var user in users)
                {
                    user.LockoutEnd = DateTime.Now.AddYears(10);
                    user.AccessFailedCount = 99;
                    user.SecurityStamp = Guid.NewGuid().ToString();
                }
            }
            else
            {
                var users = await _db.AppUsers.Where(u => u.TenantID == tenant.TenantID).ToListAsync();
                foreach (var user in users)
                {
                    user.LockoutEnd = null;
                    user.AccessFailedCount = 0;
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<Tenant>> GetAllActiveTenantsAsync()
        {
            return await _tenantRepository.Where(x => x.IsActive).ToListAsync();
        }

        public async Task<List<Tenant>> GetAllAsync()
        {
            return await _tenantRepository.GetAllAsync();
        }

        public async Task<Tenant?> GetByIdAsync(int id)
        {
            return await _tenantRepository.Where(x => x.TenantID == id).Include(x => x.BusinessHours).FirstOrDefaultAsync();
        }

        public async Task<int> AddTenantAsync(TenantCreateDto dto, string fingerprint)
        {
            var tenant = _mapper.Map<Tenant>(dto);

            // Telefon normalizasyonu
            if (!string.IsNullOrWhiteSpace(tenant.PhoneNumber))
            {
                var cleanPhone = new string(tenant.PhoneNumber.Where(char.IsDigit).ToArray());
                if (cleanPhone.StartsWith("90") && cleanPhone.Length > 10) cleanPhone = cleanPhone.Substring(2);
                if (cleanPhone.StartsWith("0") && cleanPhone.Length > 10) cleanPhone = cleanPhone.Substring(1);
                tenant.PhoneNumber = cleanPhone;
            }

            // InstanceName otomatik oluştur
            if (string.IsNullOrWhiteSpace(tenant.InstanceName))
            {
                var sanitizedName = new string(tenant.Name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                tenant.InstanceName = $"{sanitizedName}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            }

            // Unique kontrolü
            var isDuplicate = await _tenantRepository
                .Where(x => x.PhoneNumber == tenant.PhoneNumber || x.InstanceName == tenant.InstanceName)
                .AnyAsync();

            if (isDuplicate)
                throw new Exception("Bu telefon numarası veya sistemsel isim (Instance) ile daha önce kayıt olunmuş.");

            // Temel alanlar
            tenant.ApiKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            tenant.SubscriptionEndDate = DateTime.Now.AddDays(15);
            tenant.IsTrial = true;
            // ✅ DÜZELTME: IsActive başlangıçta true — AuthManager zaten yönetiyor
            tenant.IsActive = true;
            tenant.IsSubscriptionActive = true;
            tenant.TrialFingerprint = fingerprint;

            if (string.IsNullOrWhiteSpace(tenant.Address)) tenant.Address = "Adres belirtilmedi";
            if (string.IsNullOrWhiteSpace(tenant.PhoneNumber)) tenant.PhoneNumber = "Telefon belirtilmedi";

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Varsayılan çalışma saatleri
                tenant.BusinessHours = new List<BusinessHour>();
                for (int i = 0; i < 7; i++)
                {
                    bool isWeekend = i == 0 || i == 6;
                    tenant.BusinessHours.Add(new BusinessHour
                    {
                        DayOfWeek = i,
                        OpenTime = new TimeSpan(9, 0, 0),
                        CloseTime = new TimeSpan(18, 0, 0),
                        IsClosed = isWeekend
                    });
                }

                await _tenantRepository.AddAsync(tenant);
                await _tenantRepository.SaveAsync();

                // ✅ DÜZELTME: Development ortamında Evolution API hatası kaydı engellemez
                var isCreated = await _evolutionApiService.CreateInstanceAsync(tenant.InstanceName);

                if (!isCreated)
                {
                    if (_environment.IsDevelopment())
                    {
                        // Dev ortamında Evolution API yoksa sadece logla, devam et
                        _logger.LogWarning(
                            "[Dev] Evolution API instance oluşturulamadı: {InstanceName}. Kayıt devam ediyor.",
                            tenant.InstanceName);
                    }
                    else
                    {
                        // Production'da Evolution API zorunlu
                        throw new Exception("Evolution API Hatası: WhatsApp instance oluşturulamadı. Lütfen sistem yöneticisi ile iletişime geçin.");
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Tenant oluşturuldu. TenantId={TenantId}, Instance={Instance}",
                    tenant.TenantID, tenant.InstanceName);

                return tenant.TenantID;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // Evolution API'de instance oluşturulmuşsa temizle
                try { await _evolutionApiService.DeleteInstanceAsync(tenant.InstanceName); } catch { }

                _logger.LogError(ex, "Tenant kaydedilemedi. Rollback yapıldı.");
                throw new Exception("İşletme kaydedilemedi: " + ex.Message);
            }
        }

        public async Task UpdateAsync(Tenant tenant)
        {
            _tenantRepository.Update(tenant);
            await _tenantRepository.SaveAsync();
        }

        public async Task DeleteAsync(Tenant tenant)
        {
            _tenantRepository.Delete(tenant);
            await _tenantRepository.SaveAsync();
        }

        public async Task UpdateBusinessHoursAsync(int tenantId, List<BusinessHourDto> hours)
        {
            var existingHours = await _db.BusinessHours.Where(b => b.TenantID == tenantId).ToListAsync();
            _db.BusinessHours.RemoveRange(existingHours);

            var newHours = hours.Select(h => new BusinessHour
            {
                TenantID = tenantId,
                DayOfWeek = h.DayOfWeek,
                OpenTime = TimeSpan.TryParse(h.OpenTime, out var ot) ? ot : new TimeSpan(9, 0, 0),
                CloseTime = TimeSpan.TryParse(h.CloseTime, out var ct) ? ct : new TimeSpan(18, 0, 0),
                IsClosed = h.IsClosed
            });

            await _db.BusinessHours.AddRangeAsync(newHours);
            await _db.SaveChangesAsync();
        }

        public async Task<Tenant?> GetByFingerprintAsync(string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint)) return null;
            return await _tenantRepository
                .Where(t => t.TrialFingerprint == fingerprint)
                .FirstOrDefaultAsync();
        }

        public async Task SuspendForRefundAsync(
            Tenant tenant,
            string? ipAddress,
            string? rawPayload,
            string? paymentId)
        {
            if (!string.IsNullOrWhiteSpace(paymentId))
            {
                var alreadyProcessed = await _db.TransactionLogs
                    .AnyAsync(t => t.PaymentId == paymentId && t.TransactionType == "Refund");

                if (alreadyProcessed) return;
            }

            tenant.IsActive = false;
            tenant.IsSubscriptionActive = false;
            _tenantRepository.Update(tenant);

            _db.AuditLogs.Add(new AuditLog
            {
                TenantId = tenant.TenantID,
                Action = "Suspend",
                EntityName = "Tenant",
                EntityId = tenant.TenantID.ToString(),
                NewValues = "{\"Reason\": \"İade Nedeniyle Durduruldu\"}",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow,
                Source = "n8n",
                LogLevel = "Warning"
            });

            _db.TransactionLogs.Add(new TransactionLog
            {
                TenantId = tenant.TenantID,
                PaymentId = paymentId,
                SubscriptionReferenceCode = tenant.SubscriptionReferenceCode,
                TransactionType = "Refund",
                Status = "Processed",
                IpAddress = ipAddress,
                RawPayload = rawPayload,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task BlacklistAsync(Tenant tenant, string reason)
        {
            tenant.IsBlacklisted = true;
            tenant.IsActive = false;
            tenant.IsSubscriptionActive = false;
            _tenantRepository.Update(tenant);

            _db.AuditLogs.Add(new AuditLog
            {
                TenantId = tenant.TenantID,
                Action = "Blacklist",
                EntityName = "Tenant",
                EntityId = tenant.TenantID.ToString(),
                NewValues = $"{{\"Reason\": \"{reason}\", \"IsBlacklisted\": true}}",
                Timestamp = DateTime.UtcNow,
                Source = "API",
                LogLevel = "Error"
            });

            await _db.SaveChangesAsync();
        }
    }
}