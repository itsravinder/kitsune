// ============================================================
// KITSUNE – Domain Models
// ============================================================
using System;
using System.Collections.Generic;

namespace Kitsune.Backend.Models
{
    // ── Dependency Validation ────────────────────────────────

    public class ValidateRequest
    {
        public string ObjectName      { get; set; } = string.Empty;
        public string ObjectType      { get; set; } = string.Empty;  // TABLE | VIEW | PROCEDURE | FUNCTION
        public string NewDefinition   { get; set; } = string.Empty;
    }

    public class ValidateResponse
    {
        public string              Status          { get; set; } = "PASS"; // PASS | FAIL | WARN
        public List<AffectedObject> AffectedObjects { get; set; } = new();
        public string              Message         { get; set; } = string.Empty;
        public List<string>        Warnings        { get; set; } = new();
    }

    public class AffectedObject
    {
        public string AffectedName     { get; set; } = string.Empty;
        public string AffectedType     { get; set; } = string.Empty;
        public string SchemaName       { get; set; } = string.Empty;
        public string ReferencingObject{ get; set; } = string.Empty;
        public string ReferencingType  { get; set; } = string.Empty;
        public int    Depth            { get; set; }
        public string DependencyPath   { get; set; } = string.Empty;
        public bool   IsAmbiguous      { get; set; }
    }

    public class ParameterInfo
    {
        public string ParameterName    { get; set; } = string.Empty;
        public string DataType         { get; set; } = string.Empty;
        public int    MaxLength        { get; set; }
        public bool   IsOutput         { get; set; }
        public bool   HasDefaultValue  { get; set; }
    }

    // ── Backup / Versioning ──────────────────────────────────

    public class ObjectVersion
    {
        public int      Id            { get; set; }
        public string   ObjectName    { get; set; } = string.Empty;
        public string   ObjectType    { get; set; } = string.Empty;
        public int      VersionNumber { get; set; }
        public string   ScriptContent { get; set; } = string.Empty;
        public DateTime CreatedAt     { get; set; }
    }

    public class BackupRequest
    {
        public string ObjectName { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
    }

    public class BackupResponse
    {
        public bool   Success       { get; set; }
        public int    VersionNumber { get; set; }
        public string Message       { get; set; } = string.Empty;
        public string ObjectName    { get; set; } = string.Empty;
    }

    public class RollbackRequest
    {
        public string ObjectName    { get; set; } = string.Empty;
        public int    VersionNumber { get; set; }
    }

    public class RollbackResponse
    {
        public bool   Success       { get; set; }
        public string Message       { get; set; } = string.Empty;
        public string RestoredScript{ get; set; } = string.Empty;
    }

    // ── Preview Execution ────────────────────────────────────

    public class PreviewRequest
    {
        public string SqlQuery       { get; set; } = string.Empty;
        public bool   IsStoredProc   { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public int    TimeoutSeconds { get; set; } = 30;
    }

    public class PreviewResponse
    {
        public bool                       Success       { get; set; }
        public List<Dictionary<string,object?>> ResultSet { get; set; } = new();
        public List<string>               Columns       { get; set; } = new();
        public int                        RowCount      { get; set; }
        public double                     ExecutionMs   { get; set; }
        public List<string>               Errors        { get; set; } = new();
        public List<string>               Messages      { get; set; } = new();
        public string                     Mode          { get; set; } = "SAFE_PREVIEW";
    }

    // ── AI Generate ──────────────────────────────────────────

    public class GenerateRequest
    {
        public string NaturalLanguage { get; set; } = string.Empty;
        public string DatabaseName    { get; set; } = string.Empty;
        public string DatabaseType    { get; set; } = "SqlServer"; // SqlServer | MongoDB
        public string Model           { get; set; } = "auto";
        public string? Schema         { get; set; }
    }

    public class GenerateResponse
    {
        public string GeneratedQuery  { get; set; } = string.Empty;
        public string ModelUsed       { get; set; } = string.Empty;
        public string Explanation     { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public int    TokensUsed      { get; set; }
    }
}
