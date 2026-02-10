using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    SectorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => x.SectorID);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WabaID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SectorID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantID);
                    table.ForeignKey(
                        name: "FK_Tenants_Sectors_SectorID",
                        column: x => x.SectorID,
                        principalTable: "Sectors",
                        principalColumn: "SectorID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    AppUserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.AppUserID);
                    table.ForeignKey(
                        name: "FK_AppUsers_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    ServiceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationInMinutes = table.Column<int>(type: "int", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.ServiceID);
                    table.ForeignKey(
                        name: "FK_Services_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    AppointmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    AppUserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.AppointmentID);
                    table.ForeignKey(
                        name: "FK_Appointments_AppUsers_AppUserID",
                        column: x => x.AppUserID,
                        principalTable: "AppUsers",
                        principalColumn: "AppUserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Services_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "Services",
                        principalColumn: "ServiceID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Appointments_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID");
                });

            migrationBuilder.InsertData(
                table: "Sectors",
                columns: new[] { "SectorID", "DefaultPrompt", "Name" },
                values: new object[,]
                {
                    { 1, "Sen profesyonel bir erkek kuaförü asistanısın. Maskülen, net ve çözüm odaklı konuş.", "Erkek Kuaförü" },
                    { 2, "Sen nazik ve detaycı bir kadın kuaförü asistanısın. Estetik ve bakım konularına hakim konuş.", "Kadın Kuaförü" },
                    { 3, "Sen modern ve kapsayıcı bir kuaför asistanısın. Her türlü bakım hizmetine uygun profesyonel bir dille konuş.", "Unisex Kuaför" }
                });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "TenantID", "Address", "ApiKey", "CreatedAt", "IsActive", "Name", "PhoneNumber", "SectorID", "WabaID" },
                values: new object[,]
                {
                    { 1, "İstanbul, Şişli No:10", "JNT-123-ABC", new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9707), true, "Janti Erkek Kuaförü", "5551112233", 1, "W101" },
                    { 2, "Ankara, Çankaya No:25", "ISL-456-DEF", new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9723), true, "Işıltı Bayan Salonu", "5552223344", 2, "W202" },
                    { 3, "İzmir, Alsancak No:5", "MOD-789-GHI", new DateTime(2026, 2, 10, 22, 29, 47, 115, DateTimeKind.Local).AddTicks(9726), true, "Modern Tarz Unisex", "5553334455", 3, "W303" }
                });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "ServiceID", "DurationInMinutes", "Name", "Price", "TenantID" },
                values: new object[,]
                {
                    { 1, 30, "Saç Kesimi", 250m, 1 },
                    { 2, 20, "Sakal Tıraşı", 150m, 1 },
                    { 3, 45, "Cilt Bakımı", 400m, 1 },
                    { 4, 30, "Fön", 200m, 2 },
                    { 5, 120, "Boya", 1200m, 2 },
                    { 6, 40, "Manikür", 350m, 2 },
                    { 7, 60, "Modern Kesim", 500m, 3 },
                    { 8, 90, "Keratin Bakım", 1500m, 3 },
                    { 9, 30, "Kaş Dizayn", 300m, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_TenantID",
                table: "AppUsers",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppUserID",
                table: "Appointments",
                column: "AppUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ServiceID",
                table: "Appointments",
                column: "ServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantID",
                table: "Appointments",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_Services_TenantID",
                table: "Services",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SectorID",
                table: "Tenants",
                column: "SectorID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "Sectors");
        }
    }
}
