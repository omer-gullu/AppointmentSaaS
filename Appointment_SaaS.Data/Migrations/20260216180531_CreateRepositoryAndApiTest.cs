using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateRepositoryAndApiTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WabaID",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
            
            migrationBuilder.AlterColumn<string>(
                name: "DefaultPrompt",
                table: "Sectors",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9080));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9104));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9106));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9775));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9781));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 21, 5, 27, 170, DateTimeKind.Local).AddTicks(9784));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WabaID",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPrompt",
                table: "Sectors",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(539));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(560));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(563));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1115));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1121));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1125));
        }
    }
}
