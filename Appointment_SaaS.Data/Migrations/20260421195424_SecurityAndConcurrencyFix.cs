using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndConcurrencyFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_TenantID",
                table: "Appointments");

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_Tenant_Staff_Slot",
                table: "Appointments",
                columns: new[] { "TenantID", "AppUserID", "StartDate", "EndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointment_Tenant_Staff_Slot",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "AppUsers");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3668));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3702));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3708));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3803));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3813));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 20, 0, 17, 43, 558, DateTimeKind.Local).AddTicks(3820));

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantID",
                table: "Appointments",
                column: "TenantID");
        }
    }
}
