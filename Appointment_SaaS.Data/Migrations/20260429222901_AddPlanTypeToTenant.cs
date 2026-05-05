using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanTypeToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanType",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Trial");

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "ffabd0c4-1d38-4672-9ea7-5bda9c53dd0f");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5089));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5120));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5124));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PlanType" },
                values: new object[] { new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5271), "Trial" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "PlanType" },
                values: new object[] { new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5392), "Trial" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "PlanType" },
                values: new object[] { new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5399), "Trial" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "53278578-59b0-4146-91fc-6687c56ffb3d");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6082));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6116));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6125));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6339));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6458));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 17, 24, 49, 429, DateTimeKind.Local).AddTicks(6464));
        }
    }
}
