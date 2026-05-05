using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "AppUserID", "AccessFailedCount", "Email", "FirstName", "LastName", "LastOtpRequestDate", "LockoutEnd", "OtpCode", "OtpExpiry", "PhoneNumber", "Specialization", "Status", "TenantID", "TrialEndDate", "TrialStartDate" },
                values: new object[] { 1, 0, "admin@appointmentsaas.com", "Kurucu", "Admin", null, null, null, null, "05078283441", null, true, 1, null, null });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4293));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4328));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4333));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4541));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4551));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 22, 29, 19, 987, DateTimeKind.Local).AddTicks(4558));

            migrationBuilder.InsertData(
                table: "UserOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "UserId" },
                values: new object[] { 1, 1, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserOperationClaims",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(2550));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(2585));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(2590));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(2785));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(2794));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 22, 54, 15, 250, DateTimeKind.Local).AddTicks(3070));
        }
    }
}
