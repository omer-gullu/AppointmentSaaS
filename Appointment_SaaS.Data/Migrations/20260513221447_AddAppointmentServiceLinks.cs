using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentServiceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentServiceLinks",
                columns: table => new
                {
                    AppointmentServiceLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentServiceLinks", x => x.AppointmentServiceLinkID);
                    table.ForeignKey(
                        name: "FK_AppointmentServiceLinks_Appointments_AppointmentID",
                        column: x => x.AppointmentID,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentServiceLinks_Services_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "Services",
                        principalColumn: "ServiceID");
                });

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "98192ff3-6818-4d71-bcae-6061c676b65a");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(643));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(735));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(740));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(990));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(1003));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 1, 14, 36, 405, DateTimeKind.Local).AddTicks(1014));

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceLinks_AppointmentID",
                table: "AppointmentServiceLinks",
                column: "AppointmentID");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceLinks_ServiceID",
                table: "AppointmentServiceLinks",
                column: "ServiceID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentServiceLinks");

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "a40ed08f-b029-4865-9211-8cdf6c50791a");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2162));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2188));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2191));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2271));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2279));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 34, 3, 408, DateTimeKind.Local).AddTicks(2507));
        }
    }
}
