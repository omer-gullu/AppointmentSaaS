-- Manuel yedek: Update-Database yeni migration'ı uygulamıyorsa
-- SSMS veya sqlcmd ile AppointmentSaaSDB üzerinde çalıştırın.

IF COL_LENGTH('dbo.Tenants', 'BreakTimeEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants
        ADD BreakTimeEnabled bit NOT NULL
            CONSTRAINT DF_Tenants_BreakTimeEnabled DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.Tenants', 'BreakStartTime') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants
        ADD BreakStartTime time NOT NULL
            CONSTRAINT DF_Tenants_BreakStartTime DEFAULT ('12:00:00');
END
GO

IF COL_LENGTH('dbo.Tenants', 'BreakEndTime') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants
        ADD BreakEndTime time NOT NULL
            CONSTRAINT DF_Tenants_BreakEndTime DEFAULT ('13:00:00');
END
GO

IF NOT EXISTS (
    SELECT 1 FROM dbo.[__EFMigrationsHistory]
    WHERE MigrationId = N'20260525120000_AddTenantBreakTimeSettings'
)
BEGIN
    INSERT INTO dbo.[__EFMigrationsHistory] (MigrationId, ProductVersion)
    VALUES (N'20260525120000_AddTenantBreakTimeSettings', N'8.0.23');
END
GO
