-- Manuel yedek: EF update çalışmazsa SSMS'te AppointmentSaaSDB üzerinde çalıştırın.

IF COL_LENGTH('dbo.Tenants', 'CancelAtPeriodEnd') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants
        ADD CancelAtPeriodEnd bit NOT NULL
            CONSTRAINT DF_Tenants_CancelAtPeriodEnd DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Tenants', 'PendingPlanEffectiveDate') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants
        ADD PendingPlanEffectiveDate datetime2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM dbo.[__EFMigrationsHistory]
    WHERE MigrationId = N'20260521120000_AddScheduledPlanChangeFields'
)
BEGIN
    INSERT INTO dbo.[__EFMigrationsHistory] (MigrationId, ProductVersion)
    VALUES (N'20260521120000_AddScheduledPlanChangeFields', N'8.0.23');
END
GO
