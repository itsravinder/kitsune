// ============================================================
// KITSUNE – Complete Domain Models  (authoritative v6)
// All request/response types – single source of truth
// Services and Controllers MUST use these property names
// ============================================================
using System;
using System.Collections.Generic;

namespace Kitsune.Backend.Models
{
    // ── Dependency Validation ─────────────────────────────────

    public class ValidateRequest
    {
        public string ObjectName    { get; set; } = "";
        public string ObjectType    { get; set; } = "";
        public string NewDefinition { get; set; } = "";
    }

    public class ValidateResponse
    {
        public string               Status          { get; set; } = "PASS";
        public List<AffectedObject> AffectedObjects { get; set; } = new();
        public string               Message         { get; set; } = "";
        public List<string>         Warnings        { get; set; } = new();
        public List<string>         Errors          { get; set; } = new();
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
        public string ParameterName   { get; set; } = "";
        public string DataType        { get; set; } = "";
        public int    MaxLength       { get; set; }
        public bool   IsOutput        { get; set; }
        public bool   HasDefaultValue { get; set; }
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
        public string SqlQuery       { get; set; } = "";
        public bool   IsStoredProc   { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public int    TimeoutSeconds { get; set; } = 30;
    }

    public class PreviewResponse
    {
        public bool                             Success     { get; set; }
        public List<Dictionary<string,object?>> ResultSet   { get; set; } = new();
        public List<string>                     Columns     { get; set; } = new();
        public int                              RowCount    { get; set; }
        public double                           ExecutionMs { get; set; }
        public List<string>                     Errors      { get; set; } = new();
        public List<string>                     Messages    { get; set; } = new();
        public string                           Mode        { get; set; } = "SAFE_PREVIEW";
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
        public string  ColumnName    { get; set; } = "";   // matches SchemaExtractionService
        public string  DataType      { get; set; } = "";
        public bool    IsNullable    { get; set; }
        public bool    IsPrimaryKey  { get; set; }
        public bool    IsIdentity    { get; set; }
        public string? DefaultValue  { get; set; }
        public int?    MaxLength     { get; set; }
    }

    public class IndexSchema
    {
        public string IndexName    { get; set; } = "";   // matches SchemaExtractionService
        public string IndexType    { get; set; } = "";   // e.g. CLUSTERED, NONCLUSTERED
        public bool   IsUnique     { get; set; }
        public bool   IsPrimaryKey { get; set; }
        public string Columns      { get; set; } = "";   // comma-separated string
    }

    public class FKSchema
    {
        public string ConstraintName { get; set; } = "";
        public string ColumnName     { get; set; } = "";  // matches SchemaExtractionService
        public string RefSchema      { get; set; } = "";  // referenced schema
        public string RefTable       { get; set; } = "";  // matches SchemaExtractionService
        public string RefColumn      { get; set; } = "";  // matches SchemaExtractionService
    }

    public class TableSchema
    {
        public string             SchemaName  { get; set; } = "dbo";  // matches SchemaExtractionService
        public string             TableName   { get; set; } = "";      // matches SchemaExtractionService
        public List<ColumnSchema> Columns     { get; set; } = new();
        public List<IndexSchema>  Indexes     { get; set; } = new();
        public List<FKSchema>     ForeignKeys { get; set; } = new();
        public long               RowCount    { get; set; }
    }

    public class ViewSchema
    {
        public string SchemaName { get; set; } = "dbo";  // matches SchemaExtractionService
        public string ViewName   { get; set; } = "";      // matches SchemaExtractionService
        public string Definition { get; set; } = "";
    }

    public class ProcedureSchema
    {
        public string             SchemaName    { get; set; } = "dbo";  // matches service
        public string             ProcedureName { get; set; } = "";      // matches service
        public string             Definition    { get; set; } = "";
        public List<ParameterInfo> Parameters   { get; set; } = new();
        public DateTime           ModifiedAt    { get; set; }
    }

    public class FunctionSchema
    {
        public string             SchemaName   { get; set; } = "dbo";  // matches service
        public string             FunctionName { get; set; } = "";      // matches service
        public string             Definition   { get; set; } = "";
        public string             ReturnType   { get; set; } = "";
        public List<ParameterInfo> Parameters  { get; set; } = new();
    }

    public class MongoFieldSchema
    {
        public string FieldName { get; set; } = "";  // matches SchemaExtractionService
        public string BsonType  { get; set; } = "";  // matches SchemaExtractionService
    }

    public class MongoIndexSchema
    {
        public string Name   { get; set; } = "";
        public bool   Unique { get; set; }
        public string Keys   { get; set; } = "";
    }

    public class CollectionSchema
    {
        public string                 CollectionName { get; set; } = "";  // matches service
        public long                   DocumentCount  { get; set; }
        public List<MongoFieldSchema> Fields         { get; set; } = new();
        public List<MongoIndexSchema> Indexes        { get; set; } = new();
    }

    public class DatabaseSchema
    {
        public string                DatabaseName { get; set; } = "";
        public string                DatabaseType { get; set; } = "";
        public List<TableSchema>     Tables       { get; set; } = new();
        public List<ViewSchema>      Views        { get; set; } = new();
        public List<ProcedureSchema> Procedures   { get; set; } = new();
        public List<FunctionSchema>  Functions    { get; set; } = new();
        public List<CollectionSchema> Collections { get; set; } = new();
        public DateTime              ExtractedAt  { get; set; } = DateTime.UtcNow;
        public string                DDLSummary   { get; set; } = "";
    }

    // ── Apply ─────────────────────────────────────────────────

    public class ApplyRequest
    {
        public string ObjectName    { get; set; } = "";
        public string ObjectType    { get; set; } = "";
        public string SqlScript     { get; set; } = "";
        public bool   SkipValidation{ get; set; }
        public bool   SkipBackup    { get; set; }
        public string ChangeSummary { get; set; } = "";
    }

    public class ApplyResponse
    {
        public bool              Success       { get; set; }
        public string            Status        { get; set; } = "";
        public string            Message       { get; set; } = "";
        public string            ObjectName    { get; set; } = "";
        public int?              BackupVersion { get; set; }
        public ValidateResponse? Validation    { get; set; }
        public double            ExecutionMs   { get; set; }
        public List<string>      Warnings      { get; set; } = new();
        public List<string>      Errors        { get; set; } = new();
    }

    // ── Change Summary / Diff ─────────────────────────────────

    public class DiffLine
    {
        public string Type       { get; set; } = "";  // add|remove|context
        public string Content    { get; set; } = "";
        public int    LineNumber { get; set; }         // matches ChangeSummaryService
    }

    public class ChangeSummaryResult
    {
        public string         ObjectName   { get; set; } = "";
        public int            OldVersion   { get; set; }
        public int            NewVersion   { get; set; }
        public List<DiffLine> DiffLines    { get; set; } = new();   // matches ChangeSummaryService
        public int            AddedLines   { get; set; }             // matches ChangeSummaryService
        public int            RemovedLines { get; set; }             // matches ChangeSummaryService
        public string         AiSummary   { get; set; } = "";
        public string         HeuristicSummary { get; set; } = "";  // matches ChangeSummaryService
        public string         RiskLevel   { get; set; } = "LOW";
    }

    // ── Optimizer ─────────────────────────────────────────────

    public class MissingIndexHint
    {
        public string  TableName         { get; set; } = "";
        public string  SchemaName        { get; set; } = "";  // matches QueryOptimizerService
        public string? EqualityColumns   { get; set; }
        public string? InequalityColumns { get; set; }
        public string? IncludedColumns   { get; set; }
        public double  ImpactFactor      { get; set; }        // matches QueryOptimizerService
    }

    public class QueryPlanResult
    {
        public string                 PlanXml       { get; set; } = "";
        public double                 EstimatedCost { get; set; }
        public List<string>           Suggestions   { get; set; } = new();
        public List<MissingIndexHint> MissingIndexes{ get; set; } = new();
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
        public string Database   { get; set; } = "";    // matches MongoQueryService
        public string Collection { get; set; } = "";    // matches MongoQueryService
        public string Query      { get; set; } = "{}";  // matches MongoQueryService
        public string QueryType  { get; set; } = "find";
        public int    Limit      { get; set; } = 100;
    }

    public class MongoQueryResponse
    {
        public bool         Success     { get; set; }
        public List<string> Documents   { get; set; } = new();  // matches MongoQueryService
        public int          Count       { get; set; }            // matches MongoQueryService
        public double       ExecutionMs { get; set; }
        public string       Error       { get; set; } = "";      // matches MongoQueryService
    }

    // ── Connections ───────────────────────────────────────────

    public class ConnectionProfile
    {
        public int      Id             { get; set; }
        public string   ConnectionName { get; set; } = "";  // matches ConnectionManagerService
        public string   DatabaseType   { get; set; } = "SqlServer";
        public string   Host           { get; set; } = "localhost";
        public int      Port           { get; set; }
        public string   DatabaseName   { get; set; } = "";
        public string   Username       { get; set; } = "";
        public bool     TrustCert      { get; set; } = true;
        public bool     LastTestOk     { get; set; }
        public DateTime  CreatedAt     { get; set; }
        public DateTime? LastTestedAt  { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool   Success       { get; set; }
        public string Message       { get; set; } = "";
        public double ResponseMs    { get; set; }    // matches ConnectionManagerService
        public string ServerVersion { get; set; } = "";
        public string DatabaseName  { get; set; } = "";  // matches ConnectionManagerService
    }

    public class SaveProfileRequest
    {
        public string Name             { get; set; } = "";
        public string DatabaseType     { get; set; } = "SqlServer";
        public string Host             { get; set; } = "localhost";
        public int    Port             { get; set; } = 1433;
        public string Database         { get; set; } = "";   // matches ConnectionManagerService
        public string Username         { get; set; } = "";
        public string Password         { get; set; } = "";
        public bool   TrustCertificate { get; set; } = true; // matches ConnectionManagerService
    }

    // ── Schedules ─────────────────────────────────────────────

    public class BackupSchedule
    {
        public int       Id            { get; set; }
        public string    ObjectName    { get; set; } = "";
        public string    ObjectType    { get; set; } = "";
        public int       FrequencyMins { get; set; } = 60;   // matches ScheduledBackupService
        public bool      IsEnabled     { get; set; } = true;
        public DateTime? LastRunAt     { get; set; }
        public string?   LastStatus    { get; set; }
        public DateTime  CreatedAt     { get; set; }
    }

    public class ScheduleRequest
    {
        public string ObjectName       { get; set; } = "";
        public string ObjectType       { get; set; } = "PROCEDURE";
        public int    FrequencyMinutes { get; set; } = 60;  // matches ScheduledBackupService
    }

    // ── Preferences ───────────────────────────────────────────

    public class UserPreferences
    {
        public string Theme              { get; set; } = "dark";
        public string DefaultModel       { get; set; } = "auto";
        public string DefaultDbType      { get; set; } = "SqlServer";
        public int    DefaultConnectionId { get; set; }
        public bool   AutoBackupOnApply  { get; set; } = true;
        public bool   ShowExecutionPlan  { get; set; }
        public int    PreviewRowLimit    { get; set; } = 500;
        public int    AuditLogRetainDays { get; set; } = 30;
        public bool   ShowLineNumbers    { get; set; } = true;
        public string FontSize           { get; set; } = "12px";
        public Dictionary<string, string> CustomShortcuts { get; set; } = new();
    }

    // ── Script Runner ─────────────────────────────────────────

    public class ScriptRunRequest
    {
        public string  Script            { get; set; } = "";   // matches SqlScriptRunnerService
        public string? Database          { get; set; }          // matches SqlScriptRunnerService
        public bool    ParseOnly         { get; set; }          // matches SqlScriptRunnerService
        public bool    WrapInTransaction { get; set; }          // matches SqlScriptRunnerService
        public bool    StopOnError       { get; set; } = true;  // matches SqlScriptRunnerService
        public int     TimeoutSeconds    { get; set; } = 120;
        // Keep SqlScript alias so ScriptController compiles (reads req.SqlScript)
        public string  SqlScript
        {
            get => Script;
            set => Script = value;
        }
    }

    public class BatchResult
    {
        public int     BatchIndex   { get; set; }   // matches SqlScriptRunnerService
        public string  Status       { get; set; } = ""; // "OK" or "ERROR"
        public int     RowsAffected { get; set; }
        public double  ExecutionMs  { get; set; }
        public string  Preview      { get; set; } = "";  // matches SqlScriptRunnerService
        public string? Error        { get; set; }
    }

    public class ScriptRunResult
    {
        public bool              Success     { get; set; }
        public List<BatchResult> Batches     { get; set; } = new();
        public double            ExecutionMs { get; set; }  // matches SqlScriptRunnerService
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
        public string Format      { get; set; } = "csv";   // matches DataExportService
        public string Message     { get; set; } = "";       // matches DataExportService
        public byte[] FileData    { get; set; } = Array.Empty<byte>(); // matches DataExportService
        public int    RowCount    { get; set; }
        public double ExecutionMs { get; set; }
        // Aliases so ExportController (which uses .Data, .ContentType, .FileName) still compiles
        public byte[]  Data        => FileData;
        public string  ContentType => Format == "json" ? "application/json"
                                    : Format == "tsv"  ? "text/tab-separated-values"
                                    : "text/csv";
        public string  FileName    => $"kitsune-export.{Format}";
        public string? Error       { get; set; }
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
