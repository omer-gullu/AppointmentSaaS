using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Data.Extensions
{
    public static class DbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder UseAppointmentPostgreSql(
            this DbContextOptionsBuilder options,
            string? connectionString)
        {
            return options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        }
    }
}
