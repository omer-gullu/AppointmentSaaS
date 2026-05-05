using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAntiFraudAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlacklisted",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrialFingerprint",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "TrialUsed",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // GoogleEventID veritabanında zaten mevcut olduğu için hata veriyor, yorum satırına alındı
            // migrationBuilder.AddColumn<string>(
            //     name: "GoogleEventID",
            //     table: "Appointments",
            //     type: "nvarchar(max)",
            //     nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    TransactionLogID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubscriptionReferenceCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AgreementVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLogs", x => x.TransactionLogID);
                });

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
                columns: new[] { "CreatedAt", "TrialFingerprint" },
                values: new object[] { new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8252), "" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "TrialFingerprint" },
                values: new object[] { new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8268), "" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "TrialFingerprint" },
                values: new object[] { new DateTime(2026, 4, 19, 19, 24, 56, 484, DateTimeKind.Local).AddTicks(8281), "" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_PaymentId",
                table: "TransactionLogs",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionLogs");

            migrationBuilder.DropColumn(
                name: "IsBlacklisted",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialFingerprint",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialUsed",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GoogleEventID",
                table: "Appointments");

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
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4468));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4480));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 22, 15, 10, 426, DateTimeKind.Local).AddTicks(4487));
        }
    }
}
