using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test
{
    public class TenantManagerTests
    {
        private readonly Mock<ITenantRepository> _mockTenantRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IEvolutionApiService> _mockEvolutionApiService;
        private readonly AppDbContext _dbContext;
        private readonly TenantManager _tenantManager;

        public TenantManagerTests()
        {
            _mockTenantRepository = new Mock<ITenantRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockEvolutionApiService = new Mock<IEvolutionApiService>();

            // InMemory DB — her test için izole
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new AppDbContext(dbOptions);

            _tenantManager = new TenantManager(
                _mockTenantRepository.Object,
                _mockMapper.Object,
                _mockEvolutionApiService.Object,
                _dbContext
            );
        }

        [Fact]
        public async Task GetByApiKeyAsync_ShouldReturnNull_WhenTenantIsInactive()
        {
            string apiKey = "TEST-KEY-123";
            var tenants = new List<Tenant>
            {
                new Tenant { TenantID = 1, ApiKey = apiKey, IsActive = false }
            };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => tenants.AsQueryable().Where(predicate).BuildMock());

            var result = await _tenantManager.GetByApiKeyAsync(apiKey);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByPhoneNumberAsync_ShouldNormalizeAndReturnCorrectTenant()
        {
            var activeTenants = new List<Tenant>
            {
                new Tenant { TenantID = 1, PhoneNumber = "5551234567", IsActive = true },
                new Tenant { TenantID = 2, PhoneNumber = "5001112233", IsActive = true }
            };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => activeTenants.AsQueryable().Where(predicate).BuildMock());

            var result1 = await _tenantManager.GetByPhoneNumberAsync("05551234567");
            var result2 = await _tenantManager.GetByPhoneNumberAsync("905551234567");
            var result3 = await _tenantManager.GetByPhoneNumberAsync(" 5551234567 ");
            var result4 = await _tenantManager.GetByPhoneNumberAsync("05001112233");

            result1.Should().NotBeNull();
            result1!.TenantID.Should().Be(1);
            result2.Should().NotBeNull();
            result2!.TenantID.Should().Be(1);
            result3.Should().NotBeNull();
            result3!.TenantID.Should().Be(1);
            result4.Should().NotBeNull();
            result4!.TenantID.Should().Be(2);
        }

        [Fact]
        public async Task GetContextByInstanceAsync_ShouldOnlyReturnActiveTenants()
        {
            string instanceName = "test_instance";
            var tenants = new List<Tenant>
            {
                new Tenant { TenantID = 1, InstanceName = instanceName, IsActive = false }
            };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => tenants.AsQueryable().Where(predicate).BuildMock());

            var result = await _tenantManager.GetContextByInstanceAsync(instanceName);

            result.Should().BeNull();
        }

        [Fact]
        public async Task AddTenantAsync_ShouldThrowException_WhenDuplicateDataExists()
        {
            var dto = new TenantCreateDto { Name = "Duplicate Test", PhoneNumber = "5551234567" };
            var existingTenants = new List<Tenant>
            {
                new Tenant { PhoneNumber = "5551234567" }
            };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => existingTenants.AsQueryable().Where(predicate).BuildMock());
            _mockMapper.Setup(m => m.Map<Tenant>(dto)).Returns(new Tenant { PhoneNumber = "5551234567", Name = dto.Name });

            Func<Task> act = async () => await _tenantManager.AddTenantAsync(dto, It.IsAny<string>());

            var exception = await act.Should().ThrowAsync<Exception>();
            exception.WithMessage("Bu telefon numarası veya sistemsel isim (Instance) ile daha önce kayıt olunmuş.");
        }

        [Fact]
        public async Task AddTenantAsync_ShouldRollbackInstance_WhenDatabaseFails()
        {
            var dto = new TenantCreateDto { Name = "Rollback Test", PhoneNumber = "5559998877" };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant>().AsQueryable().Where(predicate).BuildMock());
            var fakeTenant = new Tenant { Name = dto.Name, PhoneNumber = "5559998877", InstanceName = "rollback_test" };
            _mockMapper.Setup(m => m.Map<Tenant>(dto)).Returns(fakeTenant);
            _mockEvolutionApiService.Setup(x => x.CreateInstanceAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockTenantRepository.Setup(x => x.SaveAsync()).ThrowsAsync(new Exception("DB Connection Error"));

            Func<Task> act = async () => await _tenantManager.AddTenantAsync(dto, It.IsAny<string>());

            var exception = await act.Should().ThrowAsync<Exception>();
            exception.WithMessage("İşletme kaydedilemedi, oluşturulan WhatsApp entegrasyonu geri alındı. Hata: DB Connection Error");
            _mockEvolutionApiService.Verify(x => x.DeleteInstanceAsync("rollback_test"), Times.Once);
        }

        // ─── YENİ: Anti-Fraud & Finansal Güvenlik Testleri ──────────────────────

        [Fact]
        public async Task GetByFingerprintAsync_ShouldReturnTenant_WhenFingerprintMatches()
        {
            var fingerprint = "abc123hash";
            var tenants = new List<Tenant>
            {
                new Tenant { TenantID = 5, TrialFingerprint = fingerprint, TrialUsed = true }
            };
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => tenants.AsQueryable().Where(predicate).BuildMock());

            var result = await _tenantManager.GetByFingerprintAsync(fingerprint);

            result.Should().NotBeNull();
            result!.TenantID.Should().Be(5);
            result.TrialUsed.Should().BeTrue();
        }

        [Fact]
        public async Task GetByFingerprintAsync_ShouldReturnNull_WhenNoMatch()
        {
            _mockTenantRepository.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                                 .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant>().AsQueryable().Where(predicate).BuildMock());

            var result = await _tenantManager.GetByFingerprintAsync("nonexistent_hash");

            result.Should().BeNull();
        }

        [Fact]
        public async Task SuspendForRefundAsync_ShouldDeactivateTenant_AndWriteLogs()
        {
            // Arrange
            var tenant = new Tenant { TenantID = 10, IsActive = true, IsSubscriptionActive = true, SubscriptionReferenceCode = "ref_xyz" };
            _mockTenantRepository.Setup(x => x.Update(tenant));
            _mockTenantRepository.Setup(x => x.SaveAsync()).ReturnsAsync(1);

            // Act
            await _tenantManager.SuspendForRefundAsync(tenant, "192.168.1.1", "{\"paymentId\":\"P123\"}", "P123");

            // Assert: Tenant kapatıldı
            tenant.IsActive.Should().BeFalse();
            tenant.IsSubscriptionActive.Should().BeFalse();

            // Assert: TransactionLog yazıldı
            var log = await _dbContext.TransactionLogs.FirstOrDefaultAsync();
            log.Should().NotBeNull();
            log!.TransactionType.Should().Be("Refund");
            log.PaymentId.Should().Be("P123");
            log.TenantId.Should().Be(10);
        }

        [Fact]
        public async Task SuspendForRefundAsync_ShouldBeIdempotent_WhenSamePaymentIdReceived()
        {
            // Arrange: Aynı P123 daha önce işlenmiş
            _dbContext.TransactionLogs.Add(new TransactionLog
            {
                TenantId = 10,
                PaymentId = "P123",
                TransactionType = "Refund",
                Status = "Processed",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            var tenant = new Tenant { TenantID = 10, IsActive = true, IsSubscriptionActive = true };
            _mockTenantRepository.Setup(x => x.Update(tenant));

            // Act: Aynı paymentId ile tekrar çağır
            await _tenantManager.SuspendForRefundAsync(tenant, "1.1.1.1", "{}", "P123");

            // Assert: Tenant state değişmedi (idempotent)
            tenant.IsActive.Should().BeTrue("Aynı işlem ikinci kez işlenmemeli");

            // Assert: Yeni log eklenmedi
            var logs = await _dbContext.TransactionLogs.Where(t => t.PaymentId == "P123").ToListAsync();
            logs.Should().HaveCount(1, "idempotent: duplicate kayıt olmamalı");
        }

        [Fact]
        public async Task BlacklistAsync_ShouldSetIsBlacklisted_AndWriteAuditLog()
        {
            // Arrange
            var tenant = new Tenant { TenantID = 99, IsActive = true, IsBlacklisted = false };
            _mockTenantRepository.Setup(x => x.Update(tenant));

            // Act
            await _tenantManager.BlacklistAsync(tenant, "Mükerrer iade suistimali");

            // Assert: Tenant kara listeye alındı
            tenant.IsBlacklisted.Should().BeTrue();
            tenant.IsActive.Should().BeFalse();

            // Assert: AuditLog yazıldı
            var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
            auditLog.Should().NotBeNull();
            auditLog!.Action.Should().Be("Blacklist");
            auditLog.TenantId.Should().Be(99);
        }
    }
}
