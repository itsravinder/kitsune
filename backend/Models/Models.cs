// ============================================================
// KITSUNE – Complete Domain Models
// All request/response types for every service
// ============================================================
using System;
using System.Collections.Generic;

namespace Kitsune.Backend.Models
{
    // ── Dependency Validation ─────────────────────────────────

    public class ValidateRequest
    {
        public string ObjectName    { get; set; } = "";
        public string ObjectType    { get; set; } = ""; // TABLE|VIEW|PROCEDURE|FUNCTION
        public string NewDefinition { get; set; } = "";
    }

    public class ValidateResponse
    {
        public string              Status          { get; set; } = "PASS";
        public List<AffectedObject> AffectedObjects { get; set; } = new();
        public string              Message         { get; set; } = "";
        public List<string>        Warnings        { get; set; } = new();
        public List<string>        Errors          { get; set; } = new();
    }

    public class AffectedObject
    {
        public string AffectedName      { get; set; } = "";
        public string AffectedType      { get; set; } = "";
        public string SchemaName        { get; set; } = "";
        public string ReferencingObject { get; set; } = "";
        public string ReferencingType   { get; set; } = "";
        public int    Depth             { get; set; }
        public string DependencyPath    { get; set; } = "";
        public bool   IsAmbiguous       { get; set; }
    }

    public class ParameterInfo
    {
        public string ParameterName  { get; set; } = "";
        public string DataType       { get; set; } = "";
        public int    MaxLength      { get; set; }
        public bool   IsOutput       { get; set; }
        public bool   HasDefaultValue{ get; set; }
    }

    // ── Backup / Versioning ───────────────────────────────────

    public class ObjectVersion
    {
        public int      Id            { get; set; }
        public string   ObjectName    { get; set; } = "";
        public string   ObjectType    { get; set; } = "";
        public int      VersionNumber { get; set; }
        public string   ScriptContent { get; set; } = "";
        public DateTime CreatedAt     { get; set; }
    }

    public class BackupRequest
    {
        public string ObjectName { get; set; } = "";
        public string ObjectType { get; set; } = "";
    }

    public class BackupResponse
    {
        public bool   Success       { get; set; }
        public int    VersionNumber { get; set; }
        public string Message       { get; set; } = "";
        public string ObjectName    { get; set; } = "";
    }

    public class RollbackRequest
    {
        public string ObjectName    { get; set; } = "";
        public int    VersionNumber { get; set; }
    }

    public class RollbackResponse
    {
        public bool   Success        { get; set; }
        public string Message        { get; set; } = "";
        public string RestoredScript { get; set; } = "";
    }

    // ── Preview Execution ─────────────────────────────────────

    public class PreviewRequest
    {
        public string SqlQuery      { get; set; } = "";
        public bool   IsStoredProc  { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public int    TimeoutSeconds{ get; set; } = 30;
    }

    public class PreviewResponse
    {
        public bool                          Success     { get; set; }
        public List<Dictionary<string,object?>> ResultSet { get; set; } = new();
        public List<string>                  Columns     { get; set; } = new();
        public int                           RowCount    { get; set; }
        public double                        ExecutionMs { get; set; }
        public List<string>                  Errors      { get; set; } = new();
        public List<string>                  Messages    { get; set; } = new();
        public string                        Mode        { get; set; } = "SAFE_PREVIEW";
    }

    // ── AI Generate ───────────────────────────────────────────

    public class GenerateRequest
    {
        public string  NaturalLanguage { get; set; } = "";
        public string  DatabaseName    { get; set; } = "";
        public string  DatabaseType    { get; set; } = "SqlServer";
        public string  Model           { get; set; } = "auto";
        public string? Schema          { get; set; }
    }

    public class GenerateResponse
    {
        public string GeneratedQuery  { get; set; } = "";
        public string ModelUsed       { get; set; } = "";
        public string Explanation     { get; set; } = "";
        public double ConfidenceScore { get; set; }
        public int    TokensUsed      { get; set; }
    }

    // ── Schema Models ─────────────────────────────────────────

    public class ColumnSchema
    {
        public string  Name          { get; set; } = "";
        public string  DataType      { get; set; } = "";
        public bool    IsNullable    { get; set; }
        public bool    IsPrimaryKey  { get; set; }
        public bool    IsIdentity    { get; set; }
        public string? DefaultValue  { get; set; }
        public int?    MaxLength     { get; set; }
        public int?    Precision     { get; set; }
        public int?    Scale         { get; set; }
    }

    public class IndexSchema
    {
        public string       Name        { get; set; } = "";
        public bool         IsUnique    { get; set; }
        public bool         IsClustered { get; set; }
        public List<string> Columns     { get; set; } = new();
    }

    public class FKSchema
    {
        public string ConstraintName   { get; set; } = "";
        public string ForeignKeyColumn { get; set; } = "";
        public string ReferencedTable  { get; set; } = "";
        public string ReferencedColumn { get; set; } = "";
    }

    public class TableSchema
    {
        public string           Schema      { get; set; } = "dbo";
        public string           Name        { get; set; } = "";
        public List<ColumnSchema> Columns   { get; set; } = new();
        public List<IndexSchema>  Indexes   { get; set; } = new();
        public List<FKSchema>     ForeignKeys { get; set; } = new();
        public long             RowCount    { get; set; }
    }

    public class ViewSchema
    {
        public string Schema     { get; set; } = "dbo";
        public string Name       { get; set; } = "";
        public string Definition { get; set; } = "";
    }

    public class ProcedureSchema
    {
        public string             Schema     { get; set; } = "dbo";
        public string             Name       { get; set; } = "";
        public string             Definition { get; set; } = "";
        public List<ParameterInfo> Parameters { get; set; } = new();
        public DateTime           ModifiedAt { get; set; }
    }

    public class FunctionSchema
    {
        public string             Schema     { get; set; } = "dbo";
        public string             Name       { get; set; } = "";
        public string             Definition { get; set; } = "";
        public string             ReturnType { get; set; } = "";
        public List<ParameterInfo> Parameters { get; set; } = new();
    }

    public class MongoFieldSchema
    {
        public string Name         { get; set; } = "";
        public string InferredType { get; set; } = "";
        public double Frequency    { get; set; }
        public bool   IsNested     { get; set; }
    }

    public class MongoIndexSchema
    {
        public string Name   { get; set; } = "";
        public bool   Unique { get; set; }
        public string Keys   { get; set; } = "";
    }

    public class CollectionSchema
    {
        public string                  Name          { get; set; } = "";
        public long                    DocumentCount { get; set; }
        public List<MongoFieldSchema>  Fields        { get; set; } = new();
        public List<MongoIndexSchema>  Indexes       { get; set; } = new();
    }

    public class DatabaseSchema
    {
        public string               DatabaseName { get; set; } = "";
        public string               DatabaseType { get; set; } = "";
        public List<TableSchema>    Tables       { get; set; } = new();
        public List<ViewSchema>     Views        { get; set; } = new();
        public List<ProcedureSchema> Procedures  { get; set; } = new();
        public List<FunctionSchema> Functions    { get; set; } = new();
        public List<CollectionSchema> Collections { get; set; } = new();
        public DateTime             ExtractedAt  { get; set; } = DateTime.UtcNow;
        public string               DDLSummary   { get; set; } = "";
    }

    // ── Apply ─────────────────────────────────────────────────

    public class ApplyRequest
    {
        public string ObjectName    { get; set; } = "";
        public string ObjectType    { get; set; } = "";
        public string SqlScript     { get; set; } = "";
        public bool   SkipValidation { get; set; }
        public bool   SkipBackup    { get; set; }
        public string ChangeSummary { get; set; } = "";
    }

    public class ApplyResponse
    {
        public bool             Success       { get; set; }
        public string           Status        { get; set; } = "";
        public string           Message       { get; set; } = "";
        public int?             BackupVersion { get; set; }
        public ValidateResponse? Validation   { get; set; }
        public double           ExecutionMs   { get; set; }
        public List<string>     Warnings      { get; set; } = new();
        public List<string>     Errors        { get; set; } = new();
    }

    // ── Change Summary / Diff ─────────────────────────────────

    public class DiffLine
    {
        public string Type    { get; set; } = ""; // added|removed|unchanged
        public int    LineNum { get; set; }
        public string Content { get; set; } = "";
    }

    public class ChangeSummaryResult
    {
        public string        ObjectName   { get; set; } = "";
        public int           OldVersion   { get; set; }
        public int           NewVersion   { get; set; }
        public List<DiffLine> Diff        { get; set; } = new();
        public int           LinesAdded   { get; set; }
        public int           LinesRemoved { get; set; }
        public string        AiSummary    { get; set; } = "";
        public List<string>  KeyChanges   { get; set; } = new();
        public string        RiskLevel    { get; set; } = "LOW";
    }

    // ── Optimizer ─────────────────────────────────────────────

    public class MissingIndexHint
    {
        public string TableName         { get; set; } = "";
        public string EqualityColumns   { get; set; } = "";
        public string InequalityColumns { get; set; } = "";
        public string IncludedColumns   { get; set; } = "";
        public double ImprovementFactor { get; set; }
        public string CreateStatement   { get; set; } = "";
    }

    public class QueryPlanResult
    {
        public string             Query         { get; set; } = "";
        public string             PlanXml       { get; set; } = "";
        public double             EstimatedCost { get; set; }
        public int                EstimatedRows { get; set; }
        public List<MissingIndexHint> MissingIndexes { get; set; } = new();
        public List<string>       Warnings      { get; set; } = new();
        public List<string>       Suggestions   { get; set; } = new();
        public string             OverallRisk   { get; set; } = "LOW";
    }

    public class OptimizeRequest
    {
        public string SqlQuery   { get; set; } = "";
        public bool   GetPlan    { get; set; } = true;
        public bool   GetIndexes { get; set; } = true;
    }

    // ── MongoDB ───────────────────────────────────────────────

    public class MongoQueryRequest
    {
        public string DatabaseName   { get; set; } = "";
        public string CollectionName { get; set; } = "";
        public string QueryJson      { get; set; } = "{}";
        public string QueryType      { get; set; } = "find";
        public string DistinctField  { get; set; } = "";
        public int    Limit          { get; set; } = 100;
        public bool   SafeMode       { get; set; } = true;
    }

    public class MongoQueryResponse
    {
        public bool         Success     { get; set; }
        public string       Mode        { get; set; } = "SAFE_READ";
        public List<string> ResultJson  { get; set; } = new();
        public int          RowCount    { get; set; }
        public double       ExecutionMs { get; set; }
        public List<string> Errors      { get; set; } = new();
        public List<string> Columns     { get; set; } = new();
    }

    // ── Connections ───────────────────────────────────────────

    public class ConnectionProfile
    {
        public int      Id             { get; set; }
        public string   Name           { get; set; } = "";
        public string   DatabaseType   { get; set; } = "SqlServer";
        public string   Host           { get; set; } = "localhost";
        public int      Port           { get; set; }
        public string   DatabaseName   { get; set; } = "";
        public string   Username       { get; set; } = "";
        public string   PasswordHint   { get; set; } = "••••••••";
        public bool     TrustCert      { get; set; } = true;
        public bool     IsActive       { get; set; } = true;
        public string   Tags           { get; set; } = "";
        public DateTime CreatedAt      { get; set; }
        public DateTime? LastTestedAt  { get; set; }
        public bool     LastTestOk     { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool   Success       { get; set; }
        public string Message       { get; set; } = "";
        public double LatencyMs     { get; set; }
        public string ServerVersion { get; set; } = "";
    }

    public class SaveProfileRequest
    {
        public string Name         { get; set; } = "";
        public string DatabaseType { get; set; } = "SqlServer";
        public string Host         { get; set; } = "localhost";
        public int    Port         { get; set; } = 1433;
        public string DatabaseName { get; set; } = "";
        public string Username     { get; set; } = "";
        public string Password     { get; set; } = "";
        public bool   TrustCert    { get; set; } = true;
        public string Tags         { get; set; } = "";
    }

    // ── Schedules ─────────────────────────────────────────────

    public class BackupSchedule
    {
        public int      Id              { get; set; }
        public string   ObjectName      { get; set; } = "";
        public string   ObjectType      { get; set; } = "";
        public int      IntervalMinutes { get; set; } = 60;
        public bool     IsEnabled       { get; set; } = true;
        public DateTime? LastRunAt      { get; set; }
        public string   LastStatus      { get; set; } = "";
        public DateTime CreatedAt       { get; set; }
    }

    public class ScheduleRequest
    {
        public string ObjectName      { get; set; } = "";
        public string ObjectType      { get; set; } = "PROCEDURE";
        public int    IntervalMinutes { get; set; } = 60;
    }

    // ── Preferences ───────────────────────────────────────────

    public class UserPreferences
    {
        public string Theme               { get; set; } = "dark";
        public string DefaultModel        { get; set; } = "auto";
        public string DefaultDbType       { get; set; } = "SqlServer";
        public int    DefaultConnectionId  { get; set; }
        public bool   AutoBackupOnApply   { get; set; } = true;
        public bool   ShowExecutionPlan   { get; set; }
        public int    PreviewRowLimit     { get; set; } = 500;
        public int    AuditLogRetainDays  { get; set; } = 30;
        public bool   ShowLineNumbers     { get; set; } = true;
        public string FontSize            { get; set; } = "12px";
        public Dictionary<string, string> CustomShortcuts { get; set; } = new();
    }

    // ── Script Runner ─────────────────────────────────────────

    public class ScriptRunRequest
    {
        public string SqlScript      { get; set; } = "";
        public bool   DryRun         { get; set; }
        public bool   UseTransaction { get; set; }
        public int    TimeoutSeconds { get; set; } = 120;
        public string? DatabaseName  { get; set; }
    }

    public class BatchResult
    {
        public int     BatchNumber  { get; set; }
        public string  BatchSql     { get; set; } = "";
        public bool    Success      { get; set; }
        public int     RowsAffected { get; set; }
        public double  ExecutionMs  { get; set; }
        public string? Error        { get; set; }
    }

    public class ScriptRunResult
    {
        public bool              Success      { get; set; }
        public int               TotalBatches { get; set; }
        public int               SuccessCount { get; set; }
        public int               FailureCount { get; set; }
        public double            TotalMs      { get; set; }
        public List<BatchResult> Batches      { get; set; } = new();
        public List<string>      Messages     { get; set; } = new();
        public string            Mode         { get; set; } = "LIVE";
    }

    // ── Export ────────────────────────────────────────────────

    public class ExportRequest
    {
        public string SqlQuery       { get; set; } = "";
        public string Format         { get; set; } = "csv";
        public bool   IncludeHeaders { get; set; } = true;
        public int    MaxRows        { get; set; } = 10_000;
        public string FileName       { get; set; } = "kitsune-export";
    }

    public class ExportResult
    {
        public bool   Success     { get; set; }
        public string ContentType { get; set; } = "text/csv";
        public string FileName    { get; set; } = "export.csv";
        public byte[] Data        { get; set; } = Array.Empty<byte>();
        public int    RowCount    { get; set; }
        public double ExecutionMs { get; set; }
        public string? Error      { get; set; }
    }

    // ── Audit ─────────────────────────────────────────────────

    public class AuditEntry
    {
        public long     Id           { get; set; }
        public string   Action       { get; set; } = "";
        public string   ObjectName   { get; set; } = "";
        public string   ObjectType   { get; set; } = "";
        public string   DatabaseName { get; set; } = "";
        public string   Status       { get; set; } = "";
        public string   RequestJson  { get; set; } = "";
        public string   ResultJson   { get; set; } = "";
        public string   ModelUsed    { get; set; } = "";
        public double   DurationMs   { get; set; }
        public DateTime CreatedAt    { get; set; }
    }
}
