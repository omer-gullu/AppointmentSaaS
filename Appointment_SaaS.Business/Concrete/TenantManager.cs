using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ITenantAccessEvaluator _tenantAccessEvaluator;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public TenantManager(
            ITenantRepository tenantRepository,
            IMapper mapper,
            IEvolutionApiService evolutionApiService,
            AppDbContext db,
            IHostEnvironment environment,
            ILogger<TenantManager> logger,
            ITenantAccessEvaluator tenantAccessEvaluator,
            IServiceScopeFactory serviceScopeFactory)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
            _evolutionApiService = evolutionApiService;
            _db = db;
            _environment = environment;
            _logger = logger;
            _tenantAccessEvaluator = tenantAccessEvaluator;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<TenantAccessEvaluation> EvaluateOperationalAccessAsync(int tenantId)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.TenantID == tenantId);

            if (tenant == null)
            {
                return new TenantAccessEvaluation(
                    false,
                    TenantAccessDenialKind.Suspended,
                    "İşletme bulunamadı.",
                    StatusCodes.Status404NotFound,
                    false);
            }

            if (SubscriptionAccessPolicy.ShouldAttemptIyzicoReconcile(tenant))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var planService = scope.ServiceProvider.GetRequiredService<ITenantPlanService>();
                if (await planService.TryReconcileFromIyzicoAsync(tenant))
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation(
                        "İyzico reconcile ile abonelik bitişi güncellendi. TenantId={TenantId} EndDate={EndDate:o}",
                        tenant.TenantID,
                        tenant.SubscriptionEndDate);
                }
            }

            var owner = await _db.AppUsers
                .AsNoTracking()
                .Where(u => u.TenantID == tenantId && u.Status)
                .OrderBy(u => u.AppUserID)
                .FirstOrDefaultAsync()
                ?? await _db.AppUsers
                    .AsNoTracking()
                    .Where(u => u.TenantID == tenantId)
                    .OrderBy(u => u.AppUserID)
                    .FirstOrDefaultAsync()
                ?? new AppUser { TenantID = tenantId };

            return _tenantAccessEvaluator.Evaluate(tenant, owner);
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
            var today = DateOnly.FromDateTime(DateTime.Today);

            return await _tenantRepository
                .Where(x => x.InstanceName == instanceName)
                .AsNoTracking()
                .Include(x => x.Services)
                .Include(x => x.BusinessHours)
                .Include(x => x.AppUsers)
                .Include(x => x.Holidays.Where(h => h.Date >= today))
                .FirstOrDefaultAsync();
        }

        public async Task<Tenant?> GetBySubscriptionReferenceAsync(string referenceCode)
        {
            return await _tenantRepository
                .Where(x => x.SubscriptionReferenceCode == referenceCode
                         || x.PendingCheckoutToken == referenceCode
                         || x.PreviousSubscriptionReferenceCode == referenceCode)
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
            return await _tenantRepository.Where(x => x.TenantID == id).FirstOrDefaultAsync();
        }

        public async Task<Tenant?> GetByIdWithBusinessHoursAsync(int id)
        {
            return await _tenantRepository
                .Where(x => x.TenantID == id)
                .Include(x => x.BusinessHours)
                .AsNoTracking()
                .FirstOrDefaultAsync();
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
                tenant.InstanceName = GenerateInstanceName(tenant.Name);
            }

            // Unique kontrolü
            var isDuplicate = await _tenantRepository
                .Where(x => x.PhoneNumber == tenant.PhoneNumber || x.InstanceName == tenant.InstanceName)
                .AnyAsync();

            if (isDuplicate)
                throw new Exception("Bu telefon numarası veya sistemsel isim (Instance) ile daha önce kayıt olunmuş.");

            // Temel alanlar
            tenant.ApiKey = TenantIntegrationKeyGenerator.Create();
            tenant.CreatedAt = DateTime.UtcNow;
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

                tenant.BreakTimeEnabled = true;
                tenant.BreakStartTime = new TimeSpan(12, 0, 0);
                tenant.BreakEndTime = new TimeSpan(13, 0, 0);

                await _tenantRepository.AddAsync(tenant);
                await _tenantRepository.SaveAsync();

                // TenantID üretildikten sonra default tatilleri ekle
                var defaultHolidays = GetDefaultHolidays2026(tenant.TenantID);
                await _db.Holidays.AddRangeAsync(defaultHolidays);
                await _db.SaveChangesAsync();

                var evolutionTimeout = _environment.IsDevelopment()
                    ? TimeSpan.FromSeconds(20)
                    : TimeSpan.FromSeconds(90);
                var createTask = _evolutionApiService.CreateInstanceAsync(tenant.InstanceName);
                var evolutionFinished = await Task.WhenAny(createTask, Task.Delay(evolutionTimeout));
                var isCreated = evolutionFinished == createTask && await createTask;

                if (evolutionFinished != createTask)
                {
                    _logger.LogWarning(
                        "Evolution API zaman aşımı ({Seconds}s). Instance={InstanceName}",
                        evolutionTimeout.TotalSeconds,
                        tenant.InstanceName);
                }

                if (!isCreated)
                {
                    if (_environment.IsDevelopment())
                    {
                        _logger.LogWarning(
                            "[Dev] Evolution API instance oluşturulamadı: {InstanceName}. Kayıt devam ediyor.",
                            tenant.InstanceName);
                    }
                    else
                    {
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

                var rootCause = ex;
                while (rootCause.InnerException != null)
                    rootCause = rootCause.InnerException;

                _logger.LogError(ex,
                    "Tenant kaydedilemedi. Rollback yapıldı. RootCause={RootCause}",
                    rootCause.Message);

                throw new Exception("İşletme kaydedilemedi: " + rootCause.Message);
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

        public async Task UpdateBreakTimeSettingsAsync(int tenantId, BreakTimeSettingsDto settings)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId)
                ?? throw new BadHttpRequestException("İşletme bulunamadı.");

            if (settings.IsEnabled)
            {
                if (!TimeSpan.TryParse(settings.StartTime, out var breakStart))
                    throw new BadHttpRequestException("Mola başlangıç saati geçersiz.");
                if (!TimeSpan.TryParse(settings.EndTime, out var breakEnd))
                    throw new BadHttpRequestException("Mola bitiş saati geçersiz.");
                if (breakEnd <= breakStart)
                    throw new BadHttpRequestException("Mola bitiş saati, başlangıç saatinden sonra olmalıdır.");

                tenant.BreakStartTime = breakStart;
                tenant.BreakEndTime = breakEnd;
            }

            tenant.BreakTimeEnabled = settings.IsEnabled;
            _tenantRepository.Update(tenant);
            await _tenantRepository.SaveAsync();
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
        private string GenerateInstanceName(string businessName)
        {
            var normalized = NormalizeToAscii(businessName);
            var sanitized = new string(normalized.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (string.IsNullOrEmpty(sanitized)) sanitized = "tenant";
            return $"{sanitized}_{Guid.NewGuid().ToString().Substring(0, 4)}";
        }

        private string NormalizeToAscii(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            
            var result = text;
            var mapping = new Dictionary<char, char>
            {
                {'ç','c'}, {'Ç','C'}, {'ğ','g'}, {'Ğ','G'},
                {'ı','i'}, {'İ','I'}, {'ö','o'}, {'Ö','O'},
                {'ş','s'}, {'Ş','S'}, {'ü','u'}, {'Ü','U'}
            };

            foreach (var item in mapping)
            {
                result = result.Replace(item.Key, item.Value);
            }

            return result;
        }
        /// <summary>
        /// İşletmenin kayıtlı tatil günleri. Silinen günler listede yoktur; asistan o güne randevu verebilir.
        /// Varsayılan tatiller yalnızca yeni kayıtta eklenir; Get ile otomatik yeniden oluşturulmaz.
        /// </summary>
        public async Task<List<Holiday>> GetHolidaysAsync(int tenantId)
        {
            return await _db.Holidays
                .Where(h => h.TenantId == tenantId)
                .OrderBy(h => h.Date)
                .ToListAsync();
        }

        /// <summary>Panel: listede hiç tatil yoksa önerilen resmi tatilleri bir kez yükler.</summary>
        public async Task<int> SeedDefaultHolidaysIfEmptyAsync(int tenantId)
        {
            var hasAny = await _db.Holidays.AnyAsync(h => h.TenantId == tenantId);
            if (hasAny)
                return 0;

            var defaults = GetDefaultHolidays2026(tenantId);
            await _db.Holidays.AddRangeAsync(defaults);
            await _db.SaveChangesAsync();
            return defaults.Count;
        }

        private List<Holiday> GetDefaultHolidays2026(int tenantId)
        {
            var list = new[]
            {
        (new DateOnly(2026, 1, 1),   "Yılbaşı"),
        (new DateOnly(2026, 3, 20),  "Ramazan Bayramı 1. Gün"),
        (new DateOnly(2026, 3, 21),  "Ramazan Bayramı 2. Gün"),
        (new DateOnly(2026, 3, 22),  "Ramazan Bayramı 3. Gün"),
        (new DateOnly(2026, 4, 23),  "Ulusal Egemenlik ve Çocuk Bayramı"),
        (new DateOnly(2026, 5, 1),   "Emek ve Dayanışma Günü"),
        (new DateOnly(2026, 5, 19),  "Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
        (new DateOnly(2026, 5, 27),  "Kurban Bayramı Arifesi"),
        (new DateOnly(2026, 5, 28),  "Kurban Bayramı 1. Gün"),
        (new DateOnly(2026, 5, 29),  "Kurban Bayramı 2. Gün"),
        (new DateOnly(2026, 5, 30),  "Kurban Bayramı 3. Gün"),
        (new DateOnly(2026, 5, 31),  "Kurban Bayramı 4. Gün"),
        (new DateOnly(2026, 7, 15),  "Demokrasi ve Millî Birlik Günü"),
        (new DateOnly(2026, 8, 30),  "Zafer Bayramı"),
        (new DateOnly(2026, 10, 29), "Cumhuriyet Bayramı"),
    };

            return list.Select(h => new Holiday
            {
                TenantId = tenantId,
                Date = h.Item1,
                Name = h.Item2,
                IsDefault = true
            }).ToList();
        }

        public async Task<Holiday> AddHolidayAsync(int tenantId, DateOnly date, string name)
        {
            name = HtmlInputSanitizer.SanitizeName(name);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tatil adı geçersiz.");

            var exists = await _db.Holidays
                .AnyAsync(h => h.TenantId == tenantId && h.Date == date);
            if (exists)
                throw new Exception("Bu tarih zaten tatil olarak tanımlı.");

            var holiday = new Holiday
            {
                TenantId = tenantId,
                Date = date,
                Name = name,
                IsDefault = false
            };
            _db.Holidays.Add(holiday);
            await _db.SaveChangesAsync();
            return holiday;
        }

        public async Task DeleteHolidayAsync(int tenantId, int holidayId)
        {
            var holiday = await _db.Holidays
                .FirstOrDefaultAsync(h => h.Id == holidayId && h.TenantId == tenantId);
            if (holiday == null)
                throw new Exception("Tatil bulunamadı.");

            _db.Holidays.Remove(holiday);
            await _db.SaveChangesAsync();
        }
    }
}