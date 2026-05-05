using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarIdToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "84f2a437-e169-458c-a8ff-8fb8f7edd098");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(545));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(581));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(585));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(772));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(783));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 12, 9, 57, 708, DateTimeKind.Local).AddTicks(790));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5271));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5392));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 1, 28, 55, 459, DateTimeKind.Local).AddTicks(5399));
        }
    }
}
