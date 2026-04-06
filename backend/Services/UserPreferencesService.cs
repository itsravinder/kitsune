// ============================================================
// KITSUNE – User Preferences Service
// Stores per-user settings: theme, default model, shortcuts
// ============================================================
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitsune.Backend.Services
{
    public class UserPreferences
    {
        public string Theme              { get; set; } = "dark";
        public string DefaultModel       { get; set; } = "auto";
        public string DefaultDbType      { get; set; } = "SqlServer";
        public int    DefaultConnectionId { get; set; } = 0;
        public bool   AutoBackupOnApply  { get; set; } = true;
        public bool   ShowExecutionPlan  { get; set; } = false;
        public int    PreviewRowLimit    { get; set; } = 500;
        public int    AuditLogRetainDays { get; set; } = 30;
        public bool   ShowLineNumbers    { get; set; } = true;
        public string FontSize           { get; set; } = "12px";
        public Dictionary<string, string> CustomShortcuts { get; set; } = new();
    }

    public interface IUserPreferencesService
    {
        Task<UserPreferences> GetAsync(string userId = "default");
        Task               SaveAsync(UserPreferences prefs, string userId = "default");
        Task               EnsureTableAsync();
    }

    public class UserPreferencesService : IUserPreferencesService
    {
        private readonly string _conn;
        private readonly ILogger<UserPreferencesService> _log;

        public UserPreferencesService(IConfiguration cfg, ILogger<UserPreferencesService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneUserPrefs')
                BEGIN
                    CREATE TABLE dbo.KitsuneUserPrefs (
                        UserId      NVARCHAR(128) NOT NULL PRIMARY KEY,
                        PrefsJson   NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                        UpdatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                    INSERT INTO dbo.KitsuneUserPrefs (UserId, PrefsJson)
                    VALUES ('default', '{}');
                END";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<UserPreferences> GetAsync(string userId = "default")
        {
            const string sql = "SELECT PrefsJson FROM dbo.KitsuneUserPrefs WHERE UserId=@UserId;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var json = (await cmd.ExecuteScalarAsync())?.ToString();
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return new UserPreferences();
            try { return JsonSerializer.Deserialize<UserPreferences>(json) ?? new(); }
            catch { return new UserPreferences(); }
        }

        public async Task SaveAsync(UserPreferences prefs, string userId = "default")
        {
            var json    = JsonSerializer.Serialize(prefs);
            const string sql = @"
                MERGE dbo.KitsuneUserPrefs AS tgt
                USING (SELECT @UserId AS UserId) AS src ON tgt.UserId = src.UserId
                WHEN MATCHED THEN
                    UPDATE SET PrefsJson=@Json, UpdatedAt=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, PrefsJson) VALUES (@UserId, @Json);";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Json",   json);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
