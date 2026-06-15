using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSectorsToFourCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski kuaför alt türlerindeki tenant'ları tek Kuaför sektörüne taşı (seed tenant 2 hariç — güzellik salonu).
            migrationBuilder.Sql("""
                UPDATE "Tenants"
                SET "SectorID" = 1
                WHERE "SectorID" IN (1, 2, 3)
                  AND "TenantID" <> 2;
                """);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen profesyonel bir kuaför randevu asistanısın. Saç kesimi, boya, bakım ve randevu konularında net, nazik ve çözüm odaklı konuş.", "Kuaför" });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen güzellik salonu randevu asistanısın. Cilt bakımı, manikür, epilasyon ve bakım hizmetlerinde detaycı, nazik ve profesyonel konuş.", "Güzellik Salonu" });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen diş kliniği randevu asistanısın. Muayene, tedavi ve kontrol randevularında güven veren, açık ve profesyonel bir dille konuş.", "Diş Kliniği" });

            migrationBuilder.InsertData(
                table: "Sectors",
                columns: new[] { "SectorID", "CreatedAt", "DefaultPrompt", "Name" },
                values: new object[] { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sen psikolog randevu asistanısın. Görüşme randevularında empatik, saygılı, gizliliğe özen gösteren ve sakin bir dille konuş.", "Psikolog" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 2,
                column: "SectorID",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "SectorID",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 1,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen profesyonel bir erkek kuaförü asistanısın. Maskülen, net ve çözüm odaklı konuş.", "Erkek Kuaförü" });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 2,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen nazik ve detaycı bir kadın kuaförü asistanısın. Estetik ve bakım konularına hakim konuş.", "Kadın Kuaförü" });

            migrationBuilder.UpdateData(
                table: "Sectors",
                keyColumn: "SectorID",
                keyValue: 3,
                columns: new[] { "DefaultPrompt", "Name" },
                values: new object[] { "Sen modern ve kapsayıcı bir kuaför asistanısın. Her türlü bakım hizmetine uygun profesyonel bir dille konuş.", "Unisex Kuaför" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "TenantID",
                keyValue: 3,
                column: "SectorID",
                value: 3);
        }
    }
}
