using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstanceName",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1091));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1112));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1115));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "InstanceName" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1411), null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "InstanceName" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1416), null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "InstanceName" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 55, 53, 246, DateTimeKind.Local).AddTicks(1420), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstanceName",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1039));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1066));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1073));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1663));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1678));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 20, 25, 21, 142, DateTimeKind.Local).AddTicks(1689));
        }
    }
}
