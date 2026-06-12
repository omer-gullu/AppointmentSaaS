using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedDataForHolidaysTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "a40ed08f-b029-4865-9211-8cdf6c50791a");

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "Date", "IsDefault", "Name", "TenantId" },
                values: new object[,]
                {
                    { 1, new DateOnly(2026, 1, 1), true, "Yılbaşı", 1 },
                    { 2, new DateOnly(2026, 3, 20), true, "Ramazan Bayramı 1. Gün", 1 },
                    { 3, new DateOnly(2026, 3, 21), true, "Ramazan Bayramı 2. Gün", 1 },
                    { 4, new DateOnly(2026, 3, 22), true, "Ramazan Bayramı 3. Gün", 1 },
                    { 5, new DateOnly(2026, 4, 23), true, "Ulusal Egemenlik ve Çocuk Bayramı", 1 },
                    { 6, new DateOnly(2026, 5, 1), true, "Emek ve Dayanışma Günü", 1 },
                    { 7, new DateOnly(2026, 5, 19), true, "Atatürk'ü Anma, Gençlik ve Spor Bayramı", 1 },
                    { 8, new DateOnly(2026, 5, 26), true, "Kurban Bayramı Arifesi", 1 },
                    { 9, new DateOnly(2026, 5, 27), true, "Kurban Bayramı 1. Gün", 1 },
                    { 10, new DateOnly(2026, 5, 28), true, "Kurban Bayramı 2. Gün", 1 },
                    { 11, new DateOnly(2026, 5, 29), true, "Kurban Bayramı 3. Gün", 1 },
                    { 12, new DateOnly(2026, 5, 30), true, "Kurban Bayramı 4. Gün", 1 },
                    { 13, new DateOnly(2026, 7, 15), true, "Demokrasi ve Millî Birlik Günü", 1 },
                    { 14, new DateOnly(2026, 8, 30), true, "Zafer Bayramı", 1 },
                    { 15, new DateOnly(2026, 10, 29), true, "Cumhuriyet Bayramı", 1 },
                    { 16, new DateOnly(2026, 11, 10), true, "Atatürkü Anma Günü", 1 },
                    { 17, new DateOnly(2026, 1, 1), true, "Yılbaşı", 2 },
                    { 18, new DateOnly(2026, 3, 20), true, "Ramazan Bayramı 1. Gün", 2 },
                    { 19, new DateOnly(2026, 3, 21), true, "Ramazan Bayramı 2. Gün", 2 },
                    { 20, new DateOnly(2026, 3, 22), true, "Ramazan Bayramı 3. Gün", 2 },
                    { 21, new DateOnly(2026, 4, 23), true, "Ulusal Egemenlik ve Çocuk Bayramı", 2 },
                    { 22, new DateOnly(2026, 5, 1), true, "Emek ve Dayanışma Günü", 2 },
                    { 23, new DateOnly(2026, 5, 19), true, "Atatürk'ü Anma, Gençlik ve Spor Bayramı", 2 },
                    { 24, new DateOnly(2026, 5, 26), true, "Kurban Bayramı Arifesi", 2 },
                    { 25, new DateOnly(2026, 5, 27), true, "Kurban Bayramı 1. Gün", 2 },
                    { 26, new DateOnly(2026, 5, 28), true, "Kurban Bayramı 2. Gün", 2 },
                    { 27, new DateOnly(2026, 5, 29), true, "Kurban Bayramı 3. Gün", 2 },
                    { 28, new DateOnly(2026, 5, 30), true, "Kurban Bayramı 4. Gün", 2 },
                    { 29, new DateOnly(2026, 7, 15), true, "Demokrasi ve Millî Birlik Günü", 2 },
                    { 30, new DateOnly(2026, 8, 30), true, "Zafer Bayramı", 2 },
                    { 31, new DateOnly(2026, 10, 29), true, "Cumhuriyet Bayramı", 2 },
                    { 32, new DateOnly(2026, 11, 10), true, "Atatürkü Anma Günü", 2 },
                    { 33, new DateOnly(2026, 1, 1), true, "Yılbaşı", 3 },
                    { 34, new DateOnly(2026, 3, 20), true, "Ramazan Bayramı 1. Gün", 3 },
                    { 35, new DateOnly(2026, 3, 21), true, "Ramazan Bayramı 2. Gün", 3 },
                    { 36, new DateOnly(2026, 3, 22), true, "Ramazan Bayramı 3. Gün", 3 },
                    { 37, new DateOnly(2026, 4, 23), true, "Ulusal Egemenlik ve Çocuk Bayramı", 3 },
                    { 38, new DateOnly(2026, 5, 1), true, "Emek ve Dayanışma Günü", 3 },
                    { 39, new DateOnly(2026, 5, 19), true, "Atatürk'ü Anma, Gençlik ve Spor Bayramı", 3 },
                    { 40, new DateOnly(2026, 5, 26), true, "Kurban Bayramı Arifesi", 3 },
                    { 41, new DateOnly(2026, 5, 27), true, "Kurban Bayramı 1. Gün", 3 },
                    { 42, new DateOnly(2026, 5, 28), true, "Kurban Bayramı 2. Gün", 3 },
                    { 43, new DateOnly(2026, 5, 29), true, "Kurban Bayramı 3. Gün", 3 },
                    { 44, new DateOnly(2026, 5, 30), true, "Kurban Bayramı 4. Gün", 3 },
                    { 45, new DateOnly(2026, 7, 15), true, "Demokrasi ve Millî Birlik Günü", 3 },
                    { 46, new DateOnly(2026, 8, 30), true, "Zafer Bayramı", 3 },
                    { 47, new DateOnly(2026, 10, 29), true, "Cumhuriyet Bayramı", 3 },
                    { 48, new DateOnly(2026, 11, 10), true, "Atatürkü Anma Günü", 3 }
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Holidays",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.UpdateData(
                table: "AppUsers",
                keyColumn: "AppUserID",
                keyValue: -1,
                column: "SecurityStamp",
                value: "153491e3-e4c5-4565-97b3-1d5d22a292c1");

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3739));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3776));

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3781));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3897));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3908));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 12, 0, 16, 43, 727, DateTimeKind.Local).AddTicks(3914));
        }
    }
}
