-- ============================================================
-- KITSUNE: Dependency Validation SQL Queries
-- ============================================================

-- ① Object Versions Table (run once on setup)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ObjectVersions')
BEGIN
    CREATE TABLE dbo.ObjectVersions (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        ObjectName    NVARCHAR(256)  NOT NULL,
        ObjectType    NVARCHAR(64)   NOT NULL,
        VersionNumber INT            NOT NULL,
        ScriptContent NVARCHAR(MAX)  NOT NULL,
        CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ObjectVersions UNIQUE (ObjectName, VersionNumber)
    );
    CREATE INDEX IX_OV_ObjectName ON dbo.ObjectVersions (ObjectName, VersionNumber DESC);
END
GO

-- ② Query: Find all objects that depend on a given object
-- Usage: replace @ObjectName with the target object name
-- Returns every object that references @ObjectName
SELECT
    o.name                                          AS AffectedObject,
    o.type_desc                                     AS ObjectType,
    OBJECT_SCHEMA_NAME(o.object_id)                 AS SchemaName,
    OBJECT_NAME(sed.referencing_id)                 AS ReferencingObject,
    ro.type_desc                                    AS ReferencingType,
    sed.is_caller_dependent,
    sed.is_ambiguous
FROM sys.sql_expression_dependencies sed
INNER JOIN sys.objects o  ON o.object_id  = sed.referenced_id
INNER JOIN sys.objects ro ON ro.object_id = sed.referencing_id
WHERE sed.referenced_id = OBJECT_ID(@ObjectName)
  AND sed.referencing_id <> sed.referenced_id
ORDER BY ro.type_desc, ro.name;


-- ③ Query: Get full dependency tree (recursive CTE)
WITH DependencyTree AS (
    -- Anchor: direct dependents
    SELECT
        sed.referencing_id                          AS ObjectId,
        OBJECT_NAME(sed.referencing_id)             AS ObjectName,
        o.type_desc                                 AS ObjectType,
        OBJECT_SCHEMA_NAME(sed.referencing_id)      AS SchemaName,
        1                                           AS Depth,
        CAST(OBJECT_NAME(sed.referencing_id) AS NVARCHAR(MAX)) AS Path
    FROM sys.sql_expression_dependencies sed
    INNER JOIN sys.objects o ON o.object_id = sed.referencing_id
    WHERE sed.referenced_id = OBJECT_ID(@ObjectName)
      AND sed.referencing_id IS NOT NULL

    UNION ALL

    -- Recursive: dependents of dependents
    SELECT
        sed2.referencing_id,
        OBJECT_NAME(sed2.referencing_id),
        o2.type_desc,
        OBJECT_SCHEMA_NAME(sed2.referencing_id),
        dt.Depth + 1,
        dt.Path + N' → ' + OBJECT_NAME(sed2.referencing_id)
    FROM sys.sql_expression_dependencies sed2
    INNER JOIN sys.objects o2 ON o2.object_id = sed2.referencing_id
    INNER JOIN DependencyTree dt ON dt.ObjectId = sed2.referenced_id
    WHERE sed2.referencing_id IS NOT NULL
      AND dt.Depth < 10   -- guard against cycles
)
SELECT DISTINCT ObjectId, ObjectName, ObjectType, SchemaName, Depth, Path
FROM DependencyTree
ORDER BY Depth, ObjectName;


-- ④ Query: Get parameters of a stored procedure or function
SELECT
    p.name          AS ParameterName,
    t.name          AS DataType,
    p.max_length,
    p.is_output,
    p.has_default_value,
    p.default_value
FROM sys.parameters p
INNER JOIN sys.types t ON t.user_type_id = p.user_type_id
WHERE p.object_id = OBJECT_ID(@ObjectName)
ORDER BY p.parameter_id;


-- ⑤ Query: Get current definition of any programmable object
SELECT
    o.name          AS ObjectName,
    o.type_desc     AS ObjectType,
    m.definition    AS ScriptContent,
    o.create_date,
    o.modify_date
FROM sys.objects o
INNER JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.name = @ObjectName
   OR OBJECT_SCHEMA_NAME(o.object_id) + '.' + o.name = @ObjectName;


-- ⑥ Query: Check for circular dependencies before applying new definition
SELECT
    OBJECT_NAME(sed.referencing_id)  AS Referencer,
    OBJECT_NAME(sed.referenced_id)   AS Referenced,
    sed.is_ambiguous
FROM sys.sql_expression_dependencies sed
WHERE sed.referencing_id = OBJECT_ID(@ObjectName)
  AND sed.referenced_id  = OBJECT_ID(@ObjectName);


-- ⑦ Retrieve last N versions for an object (used by GET /versions/{object})
SELECT TOP (@MaxVersions)
    Id, ObjectName, ObjectType, VersionNumber, ScriptContent, CreatedAt
FROM dbo.ObjectVersions
WHERE ObjectName = @ObjectName
ORDER BY VersionNumber DESC;


-- ⑧ Purge old versions, keeping only last 3
DELETE FROM dbo.ObjectVersions
WHERE ObjectName = @ObjectName
  AND VersionNumber NOT IN (
      SELECT TOP 3 VersionNumber
      FROM dbo.ObjectVersions
      WHERE ObjectName = @ObjectName
      ORDER BY VersionNumber DESC
  );
