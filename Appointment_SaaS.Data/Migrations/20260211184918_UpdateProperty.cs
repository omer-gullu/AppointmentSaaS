using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBotActive",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MessageCount",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Sectors",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

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
                columns: new[] { "CreatedAt", "IsBotActive", "MessageCount" },
                values: new object[] { new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1115), true, 0 });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsBotActive", "MessageCount" },
                values: new object[] { new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1121), true, 0 });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsBotActive", "MessageCount" },
                values: new object[] { new DateTime(2026, 2, 11, 21, 49, 16, 503, DateTimeKind.Local).AddTicks(1125), true, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBotActive",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MessageCount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Appointments");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9707));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9723));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9726));
        }
    }
}
