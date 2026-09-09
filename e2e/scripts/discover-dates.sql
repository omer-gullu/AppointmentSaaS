SELECT t."TenantID",
       t."Name",
       t."IsTrial",
       t."PlanType",
       t."SubscriptionEndDate",
       t."IsActive",
       t."IsSubscriptionActive"
FROM "Tenants" t
ORDER BY t."TenantID";
