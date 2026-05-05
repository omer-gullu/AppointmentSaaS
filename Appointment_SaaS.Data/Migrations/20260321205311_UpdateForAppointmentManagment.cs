using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForAppointmentManagment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WabaID",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(1328));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(1354));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(1358));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(3103));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(3121));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 21, 23, 53, 7, 969, DateTimeKind.Local).AddTicks(3127));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WabaID",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4463));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4492));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4495));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "WabaID" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4847), "W101" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "WabaID" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4856), "W202" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "WabaID" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4861), "W303" });
        }
    }
}
