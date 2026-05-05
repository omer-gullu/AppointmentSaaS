using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIyzicoSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IyzicoCardToken",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IyzicoUserKey",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionReferenceCode",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4293));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4325));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4329));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "IyzicoCardToken", "IyzicoUserKey", "SubscriptionReferenceCode" },
                values: new object[] { new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4468), null, null, null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IyzicoCardToken", "IyzicoUserKey", "SubscriptionReferenceCode" },
                values: new object[] { new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4480), null, null, null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IyzicoCardToken", "IyzicoUserKey", "SubscriptionReferenceCode" },
                values: new object[] { new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4487), null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IyzicoCardToken",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IyzicoUserKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionReferenceCode",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1204));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1232));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1237));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1329));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1342));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 0, 26, 57, 493, DateTimeKind.Local).AddTicks(1349));
        }
    }
}
