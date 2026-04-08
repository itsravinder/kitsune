// ============================================================
// KITSUNE – User Preferences Service
// ============================================================
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
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
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KitsuneUserPrefs')
                    CREATE TABLE dbo.KitsuneUserPrefs (
                        UserId      NVARCHAR(128) NOT NULL PRIMARY KEY,
                        PrefsJson   NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                        UpdatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
                    );";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<UserPreferences> GetAsync(string userId = "default")
        {
            const string sql = "SELECT PrefsJson FROM dbo.KitsuneUserPrefs WHERE UserId=@U;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@U", userId);
            var json = (await cmd.ExecuteScalarAsync())?.ToString();
            if (string.IsNullOrEmpty(json)) return new UserPreferences();
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }

        public async Task SaveAsync(UserPreferences prefs, string userId = "default")
        {
            const string sql = @"
                MERGE dbo.KitsuneUserPrefs AS t
                USING (SELECT @U AS UserId) AS s ON t.UserId = s.UserId
                WHEN MATCHED THEN UPDATE SET PrefsJson=@J, UpdatedAt=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (UserId, PrefsJson) VALUES (@U, @J);";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@U", userId);
            cmd.Parameters.AddWithValue("@J", JsonSerializer.Serialize(prefs));
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
