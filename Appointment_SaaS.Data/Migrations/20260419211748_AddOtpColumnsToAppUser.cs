using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpColumnsToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "AppUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOtpRequestDate",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtpCode",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiry",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOtpRequestDate",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "OtpCode",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "OtpExpiry",
                table: "AppUsers");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8026));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8054));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8063));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8252));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8268));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8281));
        }
    }
}
