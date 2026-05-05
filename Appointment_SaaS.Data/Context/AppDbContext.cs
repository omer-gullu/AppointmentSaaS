using Microsoft.AspNetCore.Http;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Interfaces;
using Appointment_SaaS.Core.Services;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Context
{
    public class AppDbContext : DbContext
    {
        private readonly ITenantProvider? _tenantProvider;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // IP adresi için HttpContext'e erişim
        private string? _currentIpAddress;

        // EF Core Query Filter için güvenli TenantId okuyucu property
        public int? CurrentTenantId => _tenantProvider?.GetTenantId();

        // Migration hatalarını engellemek için parametreler nullable/opsiyonel yapıldı ve iki constructor birleştirildi
        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider? tenantProvider = null, IHttpContextAccessor? httpContextAccessor = null) : base(options)
        {
            _tenantProvider = tenantProvider;
            _httpContextAccessor = httpContextAccessor;
            _currentIpAddress = httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
        }

        // Tablolarımız
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<OperationClaim> OperationClaims { get; set; }
        public DbSet<UserOperationClaim> UserOperationClaims { get; set; }
        public DbSet<BusinessHour> BusinessHours { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; } // YENİ: Audit log tablosu
        public DbSet<TransactionLog> TransactionLogs { get; set; } // YENİ: Finansal kanıt tablosu

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. Senin Stilinde Primary Key Tanımlama (EntityIsmiID)
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var idPropertyName = $"{entityType.ClrType.Name}ID";
                if (entityType.FindProperty(idPropertyName) != null)
                {
                    modelBuilder.Entity(entityType.ClrType).HasKey(idPropertyName);
                }
            }

            // 2. Global Query Filter: ITenantEntity implement eden tüm entity'lere otomatik TenantID filtresi
            // Bu filtre devredeyken sorguların manuel Where(x => x.TenantID == ...) yazılmasına gerek kalmaz,
            // çünkü filtreleme EF Core tarafından otomatik uygulanır. Veri sızıntısını önler.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)
                    && entityType.ClrType != typeof(Tenant))
                {
                    var method = typeof(AppDbContext)
                        .GetMethod(nameof(SetTenantQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .MakeGenericMethod(entityType.ClrType);
                    method.Invoke(this, new object[] { modelBuilder });
                }
            }

            // 3. İlişki Yapılandırmaları (Fluent API)
            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.Sector)
                .WithMany(s => s.Tenants)
                .HasForeignKey(t => t.SectorID);

            // Anti-Fraud alanları için migration default değerleri
            modelBuilder.Entity<Tenant>()
                .Property(t => t.TrialFingerprint)
                .HasDefaultValue(string.Empty);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.TrialUsed)
                .HasDefaultValue(false);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.IsBlacklisted)
                .HasDefaultValue(false);

            // Idempotency: Aynı Iyzico paymentId'si ile duplicate işlem engellenir
            modelBuilder.Entity<TransactionLog>()
                .HasIndex(t => t.PaymentId)
                .IsUnique()
                .HasFilter("[PaymentId] IS NOT NULL"); // NULL değerleri indekse dahil etme

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Tenant)
                .WithMany(t => t.Services)
                .HasForeignKey(s => s.TenantID);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Tenant)
                .WithMany(t => t.Appointments)
                .HasForeignKey(a => a.TenantID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.AppUser)
                .WithMany(u => u.Appointments)
                .HasForeignKey(a => a.AppUserID)
                .OnDelete(DeleteBehavior.Restrict);

            // Race Condition Önleme: Aynı personel, aynı tenant'ta aynı zaman dilimine iki randevu alamaz.
            // Farklı personellere (koltuk/usta) aynı saatte randevu verilebilir.
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.TenantID, a.AppUserID, a.StartDate, a.EndDate })
                .IsUnique()
                .HasDatabaseName("IX_Appointment_Tenant_Staff_Slot");

            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TransactionLog>()
                .Property(t => t.Amount)
                .HasColumnType("decimal(18,2)");

            // 4. SEED DATA (Başlangıç Verileri)

            // A. Roller (OperationClaims)
            modelBuilder.Entity<OperationClaim>().HasData(
                new OperationClaim { Id = 1, Name = "Admin" },
                new OperationClaim { Id = 2, Name = "Manager" },
                new OperationClaim { Id = 3, Name = "Staff" }
            );

            // B. Sektörler
            modelBuilder.Entity<Sector>().HasData(
                new Sector { SectorID = 1, Name = "Erkek Kuaförü", DefaultPrompt = "Sen profesyonel bir erkek kuaförü asistanısın. Maskülen, net ve çözüm odaklı konuş." },
                new Sector { SectorID = 2, Name = "Kadın Kuaförü", DefaultPrompt = "Sen nazik ve detaycı bir kadın kuaförü asistanısın. Estetik ve bakım konularına hakim konuş." },
                new Sector { SectorID = 3, Name = "Unisex Kuaför", DefaultPrompt = "Sen modern ve kapsayıcı bir kuaför asistanısın. Her türlü bakım hizmetine uygun profesyonel bir dille konuş." }
            );

            modelBuilder.Entity<Tenant>().HasData(
                new Tenant
                {
                    TenantID = 1,
                    Name = "Janti Erkek Kuaförü",
                    Address = "İstanbul, Şişli No:10",
                    ApiKey = "JNT-123-ABC",
                    PhoneNumber = "5551112233",
                    SectorID = 1,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                },
                new Tenant
                {
                    TenantID = 2,
                    Name = "Işıltı Bayan Salonu",
                    Address = "Ankara, Çankaya No:25",
                    ApiKey = "ISL-456-DEF",
                    PhoneNumber = "5552223344",
                    SectorID = 2,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                },
                new Tenant
                {
                    TenantID = 3,
                    Name = "Modern Tarz Unisex",
                    Address = "İzmir, Alsancak No:5",
                    ApiKey = "MOD-789-GHI",
                    PhoneNumber = "5553334455",
                    SectorID = 3,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                }
            );

            // C. Hizmetler (Services)
            modelBuilder.Entity<Service>().HasData(
                new Service { ServiceID = 1, TenantID = 1, Name = "Saç Kesimi", Price = 250, DurationInMinutes = 30 },
                new Service { ServiceID = 2, TenantID = 1, Name = "Sakal Tıraşı", Price = 150, DurationInMinutes = 20 },
                new Service { ServiceID = 3, TenantID = 1, Name = "Cilt Bakımı", Price = 400, DurationInMinutes = 45 },
                new Service { ServiceID = 4, TenantID = 2, Name = "Fön", Price = 200, DurationInMinutes = 30 },
                new Service { ServiceID = 5, TenantID = 2, Name = "Boya", Price = 1200, DurationInMinutes = 120 },
                new Service { ServiceID = 6, TenantID = 2, Name = "Manikür", Price = 350, DurationInMinutes = 40 },
                new Service { ServiceID = 7, TenantID = 3, Name = "Modern Kesim", Price = 500, DurationInMinutes = 60 },
                new Service { ServiceID = 8, TenantID = 3, Name = "Keratin Bakım", Price = 1500, DurationInMinutes = 90 },
                new Service { ServiceID = 9, TenantID = 3, Name = "Kaş Dizayn", Price = 300, DurationInMinutes = 30 }
            );

            // D. Çalışma Saatleri (BusinessHours)
            modelBuilder.Entity<BusinessHour>().HasData(
                new BusinessHour { BusinessHourID = 1, TenantID = 1, DayOfWeek = 1, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 2, TenantID = 1, DayOfWeek = 2, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 3, TenantID = 1, DayOfWeek = 3, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 4, TenantID = 1, DayOfWeek = 4, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 5, TenantID = 1, DayOfWeek = 5, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 6, TenantID = 1, DayOfWeek = 6, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(20, 0, 0), IsClosed = false },
                new BusinessHour { BusinessHourID = 7, TenantID = 1, DayOfWeek = 0, OpenTime = new TimeSpan(0, 0, 0), CloseTime = new TimeSpan(0, 0, 0), IsClosed = true }
            );

            // E. Kurucu Admin Kullanıcısı
            modelBuilder.Entity<AppUser>().HasData(
                new AppUser
                {
                    AppUserID = -1, // EF Core Identity çakışmasını önlemek için negatif ID
                    FirstName = "Kurucu",
                    LastName = "Admin",
                    Email = "admin@appointmentsaas.com",
                    PhoneNumber = "05078283441",
                    TenantID = 1,
                    Status = true
                }
            );

            modelBuilder.Entity<UserOperationClaim>().HasData(
                new UserOperationClaim
                {
                    Id = -1,
                    UserId = -1,
                    OperationClaimId = 1 // Admin Rolü
                }
            );

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// ITenantEntity implemente eden entity'ler için Generic Global Query Filter uygulayıcısı.
        /// Reflection ile çağrılır; her Tenant entity'sinin sorgusuna otomatik TenantID filtresi ekler.
        /// </summary>
        private void SetTenantQueryFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
        {
            // EF Core bu expression'ı SQL'e çevirirken arka planda CurrentTenantId property'sine erişir.
            // Bu property null-safe olduğu için _tenantProvider null olsa bile hata fırlatmaz.
            // Ayrıca == null durumu EF Core tarafından "filtreyi atla" mantığıyla derlenir (login, background jobs vb.)
            modelBuilder.Entity<T>().HasQueryFilter(e => CurrentTenantId == null || e.TenantID == CurrentTenantId);
        }


        /// <summary>
        /// Global Query Filter: ITenantEntity implement eden tüm entity'lere otomatik TenantID filtresi ekler.
        /// Böylece kullanıcı sadece kendi tenant'ının verilerini görebilir.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Eğer DI'dan tenant provider gelmediyse mevcut davranışı koru
            if (_tenantProvider == null)
                return;

            // optionsBuilder'i zaten DbContextOptions ile oluşturduk, burada ek filtreleme yapma
            // Global Query Filter'ı OnModelCreating'de ayarlayacağız
        }

        public override int SaveChanges()
        {
            // Audit log için async olmayan versiyon
            TrackChangesAndAudit();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await TrackChangesAndAuditAsync();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Değişen entity'leri takip eder ve AuditLog olarak kaydeder.
        /// </summary>
        private void TrackChangesAndAudit()
        {
            var auditLogs = CreateAuditLogs();
            if (auditLogs.Any())
            {
                AuditLogs.AddRange(auditLogs);
            }

            // Yeni eklenen ITenantEntity'lere otomatik TenantID ataması
            AutoSetTenantId();
        }

        private async Task TrackChangesAndAuditAsync()
        {
            TrackChangesAndAudit();
            await Task.CompletedTask;
        }

        private List<AuditLog> CreateAuditLogs()
        {
            var logs = new List<AuditLog>();
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                // ITenantEntity olanları VEYA Tenant'ın kendisini logla
                if (entry.Entity is ITenantEntity tenantEntity || entry.Entity is Tenant)
                {
                    // Kullanıcı bilgisini JWT'den çek
                    int? userId = GetCurrentUserId();
                    
                    // TenantID belirleme: ITenantEntity ise kendi TenantID'si, Tenant ise kendi ID'si
                    int? tenantId = null;
                    if (entry.Entity is ITenantEntity te) tenantId = _tenantProvider?.GetTenantId() ?? te.TenantID;
                    else if (entry.Entity is Tenant t) tenantId = t.TenantID;

                    var entityName = entry.Entity.GetType().Name;
                    var entityId = GetEntityId(entry.Entity);
                    var action = entry.State.ToString();

                    string? oldValues = null;
                    string? newValues = null;

                    if (entry.State == EntityState.Modified)
                    {
                        oldValues = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                        newValues = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                    }
                    else if (entry.State == EntityState.Added)
                    {
                        newValues = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        oldValues = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                    }

                    logs.Add(new AuditLog
                    {
                        UserId = userId,
                        TenantId = tenantId,
                        Action = action,
                        EntityName = entityName,
                        EntityId = entityId,
                        OldValues = oldValues,
                        NewValues = newValues,
                        Timestamp = DateTime.UtcNow,
                        IpAddress = _currentIpAddress
                    });
                }
            }

            return logs;
        }

        /// <summary>
        /// Yeni eklenen ITenantEntity kayıtlarına otomatik olarak mevcut TenantID'yi atar.
        /// </summary>
        private void AutoSetTenantId()
        {
            var tenantId = _tenantProvider?.GetTenantId();

            if (!tenantId.HasValue)
                return;

            var addedEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added && e.Entity is ITenantEntity)
                .ToList();

            foreach (var entry in addedEntries)
            {
                var entity = (ITenantEntity)entry.Entity;
                // Sadece 0 olanlara atama yap (zaten değer atanmışsa dokunma)
                if (entity.TenantID == 0)
                {
                    entity.TenantID = tenantId.Value;
                }
            }
        }

        private int? GetCurrentUserId()
        {
            try
            {
                // JWT'den NameIdentifier claim'ini oku
                var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }
            }
            catch { }

            return null;
        }

        private string GetEntityId(object entity)
        {
            // Primary key'i dinamik olarak bul
            var idProp = entity.GetType().GetProperty(entity.GetType().Name + "ID");
            if (idProp != null)
            {
                var value = idProp.GetValue(entity);
                return value?.ToString() ?? "Unknown";
            }
            return "Unknown";
        }
    }
}
