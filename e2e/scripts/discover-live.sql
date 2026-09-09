SELECT t."TenantID",
       t."Name",
       COALESCE(t."InstanceName", ''),
       t."IsActive",
       t."IsSubscriptionActive",
       t."IsTrial",
       COALESCE(t."PlanType", ''),
       COALESCE(t."SubscriptionReferenceCode", ''),
       COALESCE(u."PhoneNumber", ''),
       COALESCE(u."AppUserID", 0),
       COALESCE((SELECT s."ServiceID" FROM "Services" s WHERE s."TenantID" = t."TenantID" ORDER BY s."ServiceID" LIMIT 1), 0),
       COALESCE((SELECT a."AppointmentID" FROM "Appointments" a WHERE a."TenantID" = t."TenantID" ORDER BY a."AppointmentID" DESC LIMIT 1), 0),
       t."ApiKey"
FROM "Tenants" t
JOIN "AppUsers" u ON u."TenantID" = t."TenantID"
JOIN "UserOperationClaims" uoc ON uoc."UserId" = u."AppUserID"
JOIN "OperationClaims" oc ON oc."Id" = uoc."OperationClaimId"
WHERE oc."Name" = 'Manager'
  AND u."Status" = true
ORDER BY t."TenantID" DESC
LIMIT 30;
