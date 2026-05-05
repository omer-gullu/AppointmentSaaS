using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class subscripedTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4847), true });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4856), true });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 13, 1, 26, 1, 342, DateTimeKind.Local).AddTicks(4861), true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8645));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8661));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8663));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8941), false });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8946), false });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsSubscriptionActive" },
                values: new object[] { new DateTime(2026, 3, 1, 23, 20, 17, 429, DateTimeKind.Local).AddTicks(8949), false });
        }
    }
}
