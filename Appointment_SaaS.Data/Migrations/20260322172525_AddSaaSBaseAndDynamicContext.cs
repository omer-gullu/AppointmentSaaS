using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSBaseAndDynamicContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessHours",
                columns: table => new
                {
                    BusinessHourID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.BusinessHourID);
                    table.ForeignKey(
                        name: "FK_BusinessHours_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BusinessHours",
                columns: new[] { "BusinessHourID", "CloseTime", "DayOfWeek", "IsClosed", "OpenTime", "TenantID" },
                values: new object[,]
                {
                    { 1, new TimeSpan(0, 20, 0, 0, 0), 1, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 2, new TimeSpan(0, 20, 0, 0, 0), 2, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 3, new TimeSpan(0, 20, 0, 0, 0), 3, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 4, new TimeSpan(0, 20, 0, 0, 0), 4, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 5, new TimeSpan(0, 20, 0, 0, 0), 5, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 6, new TimeSpan(0, 20, 0, 0, 0), 6, false, new TimeSpan(0, 9, 0, 0, 0), 1 },
                    { 7, new TimeSpan(0, 0, 0, 0, 0), 0, true, new TimeSpan(0, 0, 0, 0, 0), 1 }
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_TenantID",
                table: "BusinessHours",
                column: "TenantID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessHours");

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
    }
}
