-- Isolated GitHub Actions Postgres: n8n HTTP sözleşmesi için tenant + personel.
-- Canlı DB'ye uygulanmaz. HasData sonrası InstanceName/staff/Google dummy doldurur.

UPDATE "Tenants"
SET
  "InstanceName" = 'ci-janti',
  "SubscriptionEndDate" = TIMESTAMP '2099-12-31 23:59:59',
  "IsActive" = TRUE,
  "IsSubscriptionActive" = TRUE,
  "IsTrial" = FALSE,
  "IsBlacklisted" = FALSE,
  "PlanType" = 'Pro'
WHERE "TenantID" = 1;

UPDATE "AppUsers"
SET
  "GoogleCalendarId" = COALESCE(NULLIF("GoogleCalendarId", ''), 'primary'),
  "GoogleRefreshToken" = COALESCE(NULLIF("GoogleRefreshToken", ''), 'ci-dummy-refresh'),
  "Status" = TRUE
WHERE "TenantID" = 1;

INSERT INTO "AppUsers" (
  "AppUserID",
  "AccessFailedCount",
  "Email",
  "FirstName",
  "LastName",
  "PhoneNumber",
  "Status",
  "TenantID",
  "GoogleCalendarId",
  "GoogleRefreshToken",
  "SecurityStamp",
  "Specialization"
) OVERRIDING SYSTEM VALUE
VALUES (
  101,
  0,
  'ci-staff@appointmentsaas.local',
  'CI',
  'Usta',
  '05551110101',
  TRUE,
  1,
  'primary',
  'ci-dummy-refresh',
  '11111111-1111-1111-1111-111111111111',
  'Kesim'
)
ON CONFLICT ("AppUserID") DO UPDATE
SET
  "GoogleCalendarId" = EXCLUDED."GoogleCalendarId",
  "GoogleRefreshToken" = EXCLUDED."GoogleRefreshToken",
  "Status" = TRUE,
  "TenantID" = 1;

INSERT INTO "UserOperationClaims" ("Id", "UserId", "OperationClaimId")
OVERRIDING SYSTEM VALUE
VALUES (101, 101, 3)
ON CONFLICT ("Id") DO NOTHING;

DO $$
DECLARE
  seq text;
BEGIN
  seq := pg_get_serial_sequence('"AppUsers"', 'AppUserID');
  IF seq IS NOT NULL THEN
    PERFORM setval(
      seq,
      GREATEST(101, COALESCE((SELECT MAX("AppUserID") FROM "AppUsers" WHERE "AppUserID" > 0), 1)),
      true
    );
  END IF;
END $$;
