UPDATE "AppUsers"
SET "TrialEndDate" = TIMESTAMPTZ '2030-09-01 00:00:00+00'
WHERE "AppUserID" = 16
  AND "TenantID" = 44;
