using System;
using System.Linq;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.Business.Abstract;
using AutoMapper;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Appointment_SaaS.Test
{
    public class AuditLogIntegrationTests
    {
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IEvolutionApiService> _mockEvolutionApiService;

        public AuditLogIntegrationTests()
        {
            _mockMapper = new Mock<IMapper>();
            _mockEvolutionApiService = new Mock<IEvolutionApiService>();
        }

        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var dbContext = new AppDbContext(options);
            return dbContext;
        }

        [Fact]
        public async Task Tenant_Update_Should_Create_AuditLog()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            
            // Seed a tenant
            var tenant = new Tenant
            {
                Name = "Güllü Randevu",
                PhoneNumber = "5551112233",
                Address = "İstanbul",
                ApiKey = "api-key",
                IsActive = true
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            // Act: Update Tenant status via Manager
            var tenantRepo = new EfTenantRepository(db);
            var mockEnvironment = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<TenantManager>>();
            var scopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
            var scope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider());
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            var tenantService = new TenantManager(
                tenantRepo,
                _mockMapper.Object,
                _mockEvolutionApiService.Object,
                db,
                mockEnvironment.Object,
                mockLogger.Object,
                new TenantAccessEvaluator(),
                scopeFactory.Object);

            var tenantToUpdate = await tenantService.GetByIdAsync(tenant.TenantID);
            tenantToUpdate!.IsActive = false;

            await tenantService.UpdateAsync(tenantToUpdate);

            // Assert
            var finalLogs = await db.AuditLogs.ToListAsync();
            var updateLog = finalLogs.FirstOrDefault(x => x.Action == "Modified" && x.EntityName == "Tenant");
            
            updateLog.Should().NotBeNull();
            updateLog!.EntityId.Should().Be(tenant.TenantID.ToString());

            // Check if OldValues and NewValues reflect IsActive change
            updateLog.OldValues.Should().Contain("\"IsActive\":true");
            updateLog.NewValues.Should().Contain("\"IsActive\":false");
        }
    }
}
