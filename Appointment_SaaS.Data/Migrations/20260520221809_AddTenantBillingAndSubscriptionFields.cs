using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBillingAndSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PendingBillingCycle",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCheckoutToken",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPlanType",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSubscriptionReferenceCode",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "b276aa7e-f7b7-4194-a9b0-22a14f140df1");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2583));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2637));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2640));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                columns: new[] { "BillingCycle", "CreatedAt", "PendingBillingCycle", "PendingCheckoutToken", "PendingPlanType", "PreviousSubscriptionReferenceCode" },
                values: new object[] { "Monthly", new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2780), null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "BillingCycle", "CreatedAt", "PendingBillingCycle", "PendingCheckoutToken", "PendingPlanType", "PreviousSubscriptionReferenceCode" },
                values: new object[] { "Monthly", new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2791), null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "BillingCycle", "CreatedAt", "PendingBillingCycle", "PendingCheckoutToken", "PendingPlanType", "PreviousSubscriptionReferenceCode" },
                values: new object[] { "Monthly", new DateTime(2026, 5, 21, 1, 18, 3, 113, DateTimeKind.Local).AddTicks(2800), null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PendingBillingCycle",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PendingCheckoutToken",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PendingPlanType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PreviousSubscriptionReferenceCode",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "5716960e-a732-4fad-9b08-f4b9fdd2e15a");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(2832));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(3250));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(3254));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(3638));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(3648));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 15, 42, 30, 605, DateTimeKind.Local).AddTicks(3656));
        }
    }
}
