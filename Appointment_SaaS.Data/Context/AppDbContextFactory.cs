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
            var apiPath = FindApiPath();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(apiPath ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("__CONNECTION_STRING"))
            {
                connectionString = "Host=127.0.0.1;Port=5432;Database=appointmentsaas;Username=appointmentsaas;Password=devpassword";
            }

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseAppointmentPostgreSql(connectionString);

            return new AppDbContext(builder.Options, null, null);
        }

        private static string? FindApiPath()
        {
            var cwd = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(cwd, "Appointment_SaaS.API"),
                Path.Combine(cwd, "../Appointment_SaaS.API"),
                cwd
            };

            foreach (var candidate in candidates)
            {
                var full = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(full, "appsettings.json")))
                    return full;
            }

            return null;
        }
    }
}
