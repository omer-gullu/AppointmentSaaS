using Appointment_SaaS.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Appointment_SaaS.Data.Context
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // API projesindeki appsettings.json dosyasını okumak için yolu ayarlıyoruz
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Appointment_SaaS.API");
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.Development.json", optional: true)
                .Build();

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("__CONNECTION_STRING"))
            {
                connectionString = "Host=127.0.0.1;Port=5432;Database=appointmentsaas;Username=appointmentsaas;Password=devpassword";
            }

            builder.UseAppointmentPostgreSql(connectionString);

            // Sadece IDesignTimeDbContextFactory'den çağrıldığında bu constructor tetiklenir
            return new AppDbContext(builder.Options, null, null);
        }
    }
}
