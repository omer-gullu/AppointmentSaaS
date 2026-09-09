SELECT u."AppUserID", u."TenantID", u."TrialEndDate"
FROM "AppUsers" u
WHERE u."TenantID" = 44
ORDER BY u."AppUserID";
