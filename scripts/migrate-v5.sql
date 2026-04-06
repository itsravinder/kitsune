-- ============================================================
-- KITSUNE v5 – Database Migration Script
-- Run this on existing databases to upgrade to v5 schema
-- Safe to run multiple times (all statements are idempotent)
-- Usage: sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d KitsuneDB -i scripts/migrate-v5.sql
-- ============================================================

USE KitsuneDB;
GO

PRINT '🦊 KITSUNE v5 Migration Starting...';
GO

-- ── 1. Enterprise version table (new in v5) ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneObjectVersions')
BEGIN
    CREATE TABLE dbo.KitsuneObjectVersions (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        ObjectName    NVARCHAR(256)  NOT NULL,
        ObjectType    NVARCHAR(64)   NOT NULL,
        VersionNumber INT            NOT NULL,
        ScriptContent NVARCHAR(MAX)  NOT NULL,
        CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy     NVARCHAR(128)  NOT NULL DEFAULT 'system',
        CONSTRAINT UQ_KOV UNIQUE (ObjectName, VersionNumber)
    );
    CREATE INDEX IX_KOV_Name ON dbo.KitsuneObjectVersions (ObjectName, VersionNumber DESC);
    PRINT '  ✓ Created dbo.KitsuneObjectVersions';
END
ELSE
    PRINT '  - dbo.KitsuneObjectVersions already exists';
GO

-- ── 2. Add CreatedBy to legacy ObjectVersions ───────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
    WHERE object_id=OBJECT_ID('dbo.ObjectVersions') AND name='CreatedBy')
BEGIN
    ALTER TABLE dbo.ObjectVersions ADD CreatedBy NVARCHAR(128) NOT NULL DEFAULT 'system';
    PRINT '  ✓ Added CreatedBy to dbo.ObjectVersions';
END
GO

-- ── 3. Backfill KitsuneObjectVersions from ObjectVersions ────
INSERT INTO dbo.KitsuneObjectVersions
    (ObjectName, ObjectType, VersionNumber, ScriptContent, CreatedAt, CreatedBy)
SELECT ObjectName, ObjectType, VersionNumber, ScriptContent, CreatedAt,
       ISNULL(TRY_CONVERT(NVARCHAR(128), CreatedBy), 'system')
FROM dbo.ObjectVersions ov
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.KitsuneObjectVersions k
    WHERE k.ObjectName=ov.ObjectName AND k.VersionNumber=ov.VersionNumber
);
PRINT '  ✓ Backfilled KitsuneObjectVersions from ObjectVersions';
GO

-- ── 4. Add ConnectionStringEnc to KitsuneConnections ─────────
IF EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneConnections')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns
        WHERE object_id=OBJECT_ID('dbo.KitsuneConnections') AND name='ConnectionStringEnc')
    BEGIN
        ALTER TABLE dbo.KitsuneConnections ADD ConnectionStringEnc NVARCHAR(MAX) NOT NULL DEFAULT '';
        PRINT '  ✓ Added ConnectionStringEnc to dbo.KitsuneConnections';
    END
    ELSE
        PRINT '  - ConnectionStringEnc already exists in KitsuneConnections';
END
GO

-- ── 5. Extend DatabaseType check for MySQL/PostgreSQL ────────
-- (No schema change needed – VARCHAR(32) already accommodates new types)
PRINT '  ✓ MySQL/PostgreSQL support requires no schema change';
GO

-- ── 6. Verify all KITSUNE tables ─────────────────────────────
SELECT
    t.name                          AS TableName,
    p.rows                          AS RowCount,
    CONVERT(VARCHAR(20), t.create_date, 120) AS CreatedAt
FROM sys.tables t
INNER JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1)
WHERE t.name LIKE 'Kitsune%' OR t.name='ObjectVersions'
ORDER BY t.name;
GO

PRINT '';
PRINT '✓ KITSUNE v5 Migration Complete!';
PRINT '  New features enabled:';
PRINT '  - dbo.KitsuneObjectVersions (enterprise version table with CreatedBy)';
PRINT '  - Dynamic MySQL/PostgreSQL connection support';
PRINT '  - Connection string override (encrypted)';
GO
