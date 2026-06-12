using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appointment_SaaS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    LogLevel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogID);
                });

            migrationBuilder.CreateTable(
                name: "OperationClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    SectorID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DefaultPrompt = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => x.SectorID);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    TransactionLogID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubscriptionReferenceCode = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AgreementVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLogs", x => x.TransactionLogID);
                });

            migrationBuilder.CreateTable(
                name: "UserOperationClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    OperationClaimId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOperationClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    InstanceName = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    IsBotActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    SubscriptionEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSubscriptionActive = table.Column<bool>(type: "boolean", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: true),
                    IyzicoUserKey = table.Column<string>(type: "text", nullable: true),
                    IyzicoCardToken = table.Column<string>(type: "text", nullable: true),
                    SubscriptionReferenceCode = table.Column<string>(type: "text", nullable: true),
                    GoogleEmail = table.Column<string>(type: "text", nullable: true),
                    GoogleAccessToken = table.Column<string>(type: "text", nullable: true),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    PlanType = table.Column<string>(type: "text", nullable: false),
                    BillingCycle = table.Column<string>(type: "text", nullable: false),
                    PendingPlanType = table.Column<string>(type: "text", nullable: true),
                    PendingBillingCycle = table.Column<string>(type: "text", nullable: true),
                    PendingCheckoutToken = table.Column<string>(type: "text", nullable: true),
                    PreviousSubscriptionReferenceCode = table.Column<string>(type: "text", nullable: true),
                    PendingPlanEffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                    TrialFingerprint = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    TrialUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsBlacklisted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SectorID = table.Column<int>(type: "integer", nullable: false),
                    BreakTimeEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    BreakStartTime = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValue: new TimeSpan(0, 12, 0, 0, 0)),
                    BreakEndTime = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValue: new TimeSpan(0, 13, 0, 0, 0))
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
                    AppUserID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    OtpCode = table.Column<string>(type: "text", nullable: true),
                    OtpExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastOtpRequestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    Specialization = table.Column<string>(type: "text", nullable: true),
                    GoogleCalendarId = table.Column<string>(type: "text", nullable: true),
                    GoogleRefreshToken = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<bool>(type: "boolean", nullable: false),
                    TrialStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantID = table.Column<int>(type: "integer", nullable: false)
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
                name: "BusinessHours",
                columns: table => new
                {
                    BusinessHourID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    FeedbackID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    FeedbackType = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.FeedbackID);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    ServiceID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DurationInMinutes = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false)
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
                name: "TenantBlockedPhones",
                columns: table => new
                {
                    TenantBlockedPhoneID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    PhoneCore = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBlockedPhones", x => x.TenantBlockedPhoneID);
                    table.ForeignKey(
                        name: "FK_TenantBlockedPhones_Tenants_TenantID",
                        column: x => x.TenantID,
                        principalTable: "Tenants",
                        principalColumn: "TenantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    AppointmentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    ServiceID = table.Column<int>(type: "integer", nullable: false),
                    AppUserID = table.Column<int>(type: "integer", nullable: false),
                    GoogleEventID = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "AppointmentServiceLinks",
                columns: table => new
                {
                    AppointmentServiceLinkID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentID = table.Column<int>(type: "integer", nullable: false),
                    ServiceID = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.InsertData(
                table: "OperationClaims",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Admin" },
                    { 2, "Manager" },
                    { 3, "Staff" }
                });

            migrationBuilder.InsertData(
                table: "Sectors",
                columns: new[] { "SectorID", "CreatedAt", "DefaultPrompt", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sen profesyonel bir erkek kuaförü asistanısın. Maskülen, net ve çözüm odaklı konuş.", "Erkek Kuaförü" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sen nazik ve detaycı bir kadın kuaförü asistanısın. Estetik ve bakım konularına hakim konuş.", "Kadın Kuaförü" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sen modern ve kapsayıcı bir kuaför asistanısın. Her türlü bakım hizmetine uygun profesyonel bir dille konuş.", "Unisex Kuaför" }
                });

            migrationBuilder.InsertData(
                table: "UserOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "UserId" },
                values: new object[] { -1, 1, -1 });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "TenantID", "Address", "ApiKey", "AutoRenew", "BillingCycle", "BreakEndTime", "BreakStartTime", "BreakTimeEnabled", "CancelAtPeriodEnd", "CreatedAt", "GoogleAccessToken", "GoogleEmail", "InstanceName", "IsActive", "IsBotActive", "IsSubscriptionActive", "IsTrial", "IyzicoCardToken", "IyzicoUserKey", "MessageCount", "Name", "PendingBillingCycle", "PendingCheckoutToken", "PendingPlanEffectiveDate", "PendingPlanType", "PhoneNumber", "PlanType", "PreviousSubscriptionReferenceCode", "SectorID", "StripeCustomerId", "SubscriptionEndDate", "SubscriptionReferenceCode", "TrialFingerprint" },
                values: new object[,]
                {
                    { 1, "İstanbul, Şişli No:10", "JNT-123-ABC", true, "Monthly", new TimeSpan(0, 13, 0, 0, 0), new TimeSpan(0, 12, 0, 0, 0), true, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, true, true, true, false, null, null, 0, "Janti Erkek Kuaförü", null, null, null, null, "5551112233", "Trial", null, 1, null, new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), null, "" },
                    { 2, "Ankara, Çankaya No:25", "ISL-456-DEF", true, "Monthly", new TimeSpan(0, 13, 0, 0, 0), new TimeSpan(0, 12, 0, 0, 0), true, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, true, true, true, false, null, null, 0, "Işıltı Bayan Salonu", null, null, null, null, "5552223344", "Trial", null, 2, null, new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), null, "" },
                    { 3, "İzmir, Alsancak No:5", "MOD-789-GHI", true, "Monthly", new TimeSpan(0, 13, 0, 0, 0), new TimeSpan(0, 12, 0, 0, 0), true, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, true, true, true, false, null, null, 0, "Modern Tarz Unisex", null, null, null, null, "5553334455", "Trial", null, 3, null, new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), null, "" }
                });

            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "AppUserID", "AccessFailedCount", "Email", "FirstName", "GoogleCalendarId", "GoogleRefreshToken", "LastName", "LastOtpRequestDate", "LockoutEnd", "OtpCode", "OtpExpiry", "PhoneNumber", "SecurityStamp", "Specialization", "Status", "TenantID", "TrialEndDate", "TrialStartDate" },
                values: new object[] { -1, 0, "admin@appointmentsaas.com", "Kurucu", null, null, "Admin", null, null, null, null, "05078283441", "e75bba40-b367-47f2-a7fe-91a8e552f47d", null, true, 1, null, null });

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
                name: "IX_Appointment_Tenant_Staff_Slot",
                table: "Appointments",
                columns: new[] { "TenantID", "AppUserID", "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppUserID",
                table: "Appointments",
                column: "AppUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ServiceID",
                table: "Appointments",
                column: "ServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceLinks_AppointmentID",
                table: "AppointmentServiceLinks",
                column: "AppointmentID");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServiceLinks_ServiceID",
                table: "AppointmentServiceLinks",
                column: "ServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_TenantID",
                table: "AppUsers",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_TenantID",
                table: "BusinessHours",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_TenantID",
                table: "Feedbacks",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_TenantId",
                table: "Holidays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_TenantID",
                table: "Services",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBlockedPhones_TenantID_PhoneCore",
                table: "TenantBlockedPhones",
                columns: new[] { "TenantID", "PhoneCore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SectorID",
                table: "Tenants",
                column: "SectorID");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_PaymentId",
                table: "TransactionLogs",
                column: "PaymentId",
                unique: true,
                filter: "\"PaymentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentServiceLinks");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BusinessHours");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "OperationClaims");

            migrationBuilder.DropTable(
                name: "TenantBlockedPhones");

            migrationBuilder.DropTable(
                name: "TransactionLogs");

            migrationBuilder.DropTable(
                name: "UserOperationClaims");

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
