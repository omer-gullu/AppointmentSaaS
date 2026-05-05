using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreatePasswordHashAndSalt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- TENANTS GÜNCELLEMELERİ ---
            migrationBuilder.AddColumn<bool>(name: "AutoRenew", table: "Tenants", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsSubscriptionActive", table: "Tenants", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsTrial", table: "Tenants", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "StripeCustomerId", table: "Tenants", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "SubscriptionEndDate", table: "Tenants", type: "datetime2", nullable: false, defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // --- APPUSERS KRİTİK DÜZELTME ---
            // AlterColumn yerine Drop/Add yaparak tip dönüşüm hatasını (CS0119) bypass ediyoruz
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "AppUsers");

            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordHash",
                table: "AppUsers",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordSalt",
                table: "AppUsers",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<bool>(
                name: "Status",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // --- YENİ TABLOLAR ---
            migrationBuilder.CreateTable(
                name: "OperationClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_OperationClaims", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "UserOperationClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OperationClaimId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_UserOperationClaims", x => x.Id); });

            // --- SEED DATA GÜNCELLEMELERİ ---
            // (Aşağıdaki UpdateData kısımlarına dokunma, onlar kalsın)
            migrationBuilder.UpdateData(table: "Sectors", keyColumn: "SectorID", keyValue: 1, column: "CreatedAt", value: new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4168));
            migrationBuilder.UpdateData(table: "Sectors", keyColumn: "SectorID", keyValue: 2, column: "CreatedAt", value: new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4187));
            migrationBuilder.UpdateData(table: "Sectors", keyColumn: "SectorID", keyValue: 3, column: "CreatedAt", value: new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4189));
            migrationBuilder.UpdateData(table: "Tenants", keyColumn: "TenantID", keyValue: 1, columns: new[] { "AutoRenew", "CreatedAt", "IsSubscriptionActive", "IsTrial", "StripeCustomerId", "SubscriptionEndDate" }, values: new object[] { true, new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4411), false, false, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
            migrationBuilder.UpdateData(table: "Tenants", keyColumn: "TenantID", keyValue: 2, columns: new[] { "AutoRenew", "CreatedAt", "IsSubscriptionActive", "IsTrial", "StripeCustomerId", "SubscriptionEndDate" }, values: new object[] { true, new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4416), false, false, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
            migrationBuilder.UpdateData(table: "Tenants", keyColumn: "TenantID", keyValue: 3, columns: new[] { "AutoRenew", "CreatedAt", "IsSubscriptionActive", "IsTrial", "StripeCustomerId", "SubscriptionEndDate" }, values: new object[] { true, new DateTime(2026, 2, 26, 20, 42, 34, 907, DateTimeKind.Local).AddTicks(4420), false, false, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationClaims");

            migrationBuilder.DropTable(
                name: "UserOperationClaims");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsSubscriptionActive",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PasswordSalt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppUsers");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "AppUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

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
    }
}
