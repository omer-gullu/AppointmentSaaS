using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffGoogleRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "GoogleRefreshToken",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                columns: new[] { "GoogleCalendarId", "GoogleRefreshToken", "SecurityStamp" },
                values: new object[] { null, null, "92f1c9b1-31ac-4f1d-b41a-66e4c6e2c126" });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9401));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9427));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9431));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9587));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9596));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 4, 14, 24, 519, DateTimeKind.Local).AddTicks(9601));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "GoogleRefreshToken",
                table: "AppUsers");

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
    }
}
