SELECT u."AppUserID", u."TenantID", u."PhoneNumber", u."TrialEndDate", u."TrialStartDate", u."Status"
FROM "AppUsers" u
WHERE u."TenantID" IN (29, 35, 44)
ORDER BY u."TenantID", u."AppUserID";
