using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeSecurityStampNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserOperationClaims",
                keyColumn: "Id",
                keyValue: 1);


            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "AppUserID", "AccessFailedCount", "Email", "FirstName", "LastName", "LastOtpRequestDate", "LockoutEnd", "OtpCode", "OtpExpiry", "PhoneNumber", "SecurityStamp", "Specialization", "Status", "TenantID", "TrialEndDate", "TrialStartDate" },
                values: new object[] { -1, 0, "admin@appointmentsaas.com", "Kurucu", "Admin", null, null, null, null, "05078283441", "c8ce70fc-4317-4751-81ec-bac7b1151062", null, true, 1, null, null });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(2898));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(2930));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(2938));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(3138));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(3154));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 1, 37, 51, 172, DateTimeKind.Local).AddTicks(3167));

            migrationBuilder.InsertData(
                table: "UserOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "UserId" },
                values: new object[] { -1, 1, -1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1);

            migrationBuilder.DeleteData(
                table: "UserOperationClaims",
                keyColumn: "Id",
                keyValue: -1);

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "AppUsers");

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
    }
}
