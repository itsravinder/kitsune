// ============================================================
// KITSUNE – Connection Manager Service
// Multi-database connection profiles (SQL Server + MongoDB)
// Stored encrypted in database; tested before use
// ============================================================
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Kitsune.Backend.Services
{
    public class ConnectionProfile
    {
        public int    Id             { get; set; }
        public string Name           { get; set; } = "";
        public string DatabaseType   { get; set; } = "SqlServer"; // SqlServer | MongoDB
        public string Host           { get; set; } = "localhost";
        public int    Port           { get; set; }
        public string DatabaseName   { get; set; } = "";
        public string Username       { get; set; } = "";
        public string PasswordHint   { get; set; } = ""; // masked: ****
        public bool   TrustCert      { get; set; } = true;
        public bool   IsActive       { get; set; } = true;
        public string Tags           { get; set; } = "";
        public DateTime CreatedAt    { get; set; }
        public DateTime? LastTestedAt{ get; set; }
        public bool   LastTestOk     { get; set; }
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
        public string Password     { get; set; } = ""; // plaintext – encrypted before storage
        public bool   TrustCert    { get; set; } = true;
        public string Tags         { get; set; } = "";
    }

    public interface IConnectionManagerService
    {
        Task<List<ConnectionProfile>> ListProfilesAsync();
        Task<int>                     SaveProfileAsync(SaveProfileRequest req);
        Task<ConnectionTestResult>    TestProfileAsync(int id);
        Task<ConnectionTestResult>    TestConnectionStringAsync(string connStr, string dbType);
        Task<bool>                    DeleteProfileAsync(int id);
        Task<string>                  GetConnectionStringAsync(int id);
        Task                          EnsureTableAsync();
    }

    public class ConnectionManagerService : IConnectionManagerService
    {
        private readonly string _masterConn;
        private readonly ILogger<ConnectionManagerService> _log;

        // Simple AES key derived from machine name – rotate via config in production
        private static readonly byte[] _aesKey =
            SHA256.HashData(Encoding.UTF8.GetBytes("KITSUNE_ENCRYPTION_KEY_CHANGE_ME"));

        public ConnectionManagerService(IConfiguration cfg, ILogger<ConnectionManagerService> log)
        {
            _masterConn = cfg.GetConnectionString("SqlServer") ?? "";
            _log        = log;
        }

        // ── Bootstrap ─────────────────────────────────────────
        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneConnections')
                BEGIN
                    CREATE TABLE dbo.KitsuneConnections (
                        Id             INT IDENTITY(1,1) PRIMARY KEY,
                        Name           NVARCHAR(128) NOT NULL,
                        DatabaseType   NVARCHAR(32)  NOT NULL DEFAULT 'SqlServer',
                        Host           NVARCHAR(256) NOT NULL,
                        Port           INT           NOT NULL DEFAULT 1433,
                        DatabaseName   NVARCHAR(128) NOT NULL DEFAULT '',
                        Username       NVARCHAR(128) NOT NULL DEFAULT '',
                        PasswordEnc    NVARCHAR(MAX) NOT NULL DEFAULT '',
                        TrustCert      BIT           NOT NULL DEFAULT 1,
                        IsActive       BIT           NOT NULL DEFAULT 1,
                        Tags           NVARCHAR(256) NOT NULL DEFAULT '',
                        CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
                        LastTestedAt   DATETIME2     NULL,
                        LastTestOk     BIT           NOT NULL DEFAULT 0
                    );
                END";

            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        // ── List all profiles (passwords masked) ──────────────
        public async Task<List<ConnectionProfile>> ListProfilesAsync()
        {
            const string sql = @"
                SELECT Id, Name, DatabaseType, Host, Port, DatabaseName,
                       Username, TrustCert, IsActive, Tags,
                       CreatedAt, LastTestedAt, LastTestOk
                FROM dbo.KitsuneConnections
                WHERE IsActive = 1
                ORDER BY Name;";

            var results = new List<ConnectionProfile>();
            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var r = await new SqlCommand(sql, conn).ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(new ConnectionProfile
                {
                    Id           = Convert.ToInt32(r["Id"]),
                    Name         = r["Name"].ToString()!,
                    DatabaseType = r["DatabaseType"].ToString()!,
                    Host         = r["Host"].ToString()!,
                    Port         = Convert.ToInt32(r["Port"]),
                    DatabaseName = r["DatabaseName"].ToString()!,
                    Username     = r["Username"].ToString()!,
                    PasswordHint = "••••••••",
                    TrustCert    = Convert.ToBoolean(r["TrustCert"]),
                    IsActive     = Convert.ToBoolean(r["IsActive"]),
                    Tags         = r["Tags"].ToString()!,
                    CreatedAt    = Convert.ToDateTime(r["CreatedAt"]),
                    LastTestedAt = r["LastTestedAt"] == DBNull.Value ? null : Convert.ToDateTime(r["LastTestedAt"]),
                    LastTestOk   = Convert.ToBoolean(r["LastTestOk"]),
                });
            return results;
        }

        // ── Save / upsert a profile ───────────────────────────
        public async Task<int> SaveProfileAsync(SaveProfileRequest req)
        {
            var encPwd = Encrypt(req.Password);
            const string sql = @"
                INSERT INTO dbo.KitsuneConnections
                    (Name, DatabaseType, Host, Port, DatabaseName, Username, PasswordEnc, TrustCert, Tags)
                VALUES
                    (@Name, @DbType, @Host, @Port, @DbName, @User, @Pwd, @Trust, @Tags);
                SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name",   req.Name);
            cmd.Parameters.AddWithValue("@DbType", req.DatabaseType);
            cmd.Parameters.AddWithValue("@Host",   req.Host);
            cmd.Parameters.AddWithValue("@Port",   req.Port);
            cmd.Parameters.AddWithValue("@DbName", req.DatabaseName);
            cmd.Parameters.AddWithValue("@User",   req.Username);
            cmd.Parameters.AddWithValue("@Pwd",    encPwd);
            cmd.Parameters.AddWithValue("@Trust",  req.TrustCert);
            cmd.Parameters.AddWithValue("@Tags",   req.Tags);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // ── Test a saved profile by ID ────────────────────────
        public async Task<ConnectionTestResult> TestProfileAsync(int id)
        {
            var connStr = await GetConnectionStringAsync(id);
            var dbType  = await GetDatabaseTypeAsync(id);
            var result  = await TestConnectionStringAsync(connStr, dbType);

            // Update last-tested timestamp
            const string upd = @"
                UPDATE dbo.KitsuneConnections
                SET LastTestedAt = SYSUTCDATETIME(), LastTestOk = @Ok
                WHERE Id = @Id;";
            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(upd, conn);
            cmd.Parameters.AddWithValue("@Ok", result.Success ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            return result;
        }

        // ── Test any connection string ────────────────────────
        public async Task<ConnectionTestResult> TestConnectionStringAsync(string connStr, string dbType)
        {
            var result = new ConnectionTestResult();
            var sw     = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (dbType == "MongoDB")
                {
                    var client = new MongoClient(connStr);
                    await client.ListDatabaseNamesAsync();
                    result.Success       = true;
                    result.ServerVersion = "MongoDB";
                }
                else
                {
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    await using var cmd = new SqlCommand("SELECT @@VERSION;", conn);
                    var ver = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
                    result.Success       = true;
                    result.ServerVersion = ver.Split('\n')[0].Trim();
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
            }
            finally
            {
                sw.Stop();
                result.LatencyMs = sw.Elapsed.TotalMilliseconds;
                if (result.Success)
                    result.Message = $"Connected successfully in {result.LatencyMs:0.0}ms";
            }

            return result;
        }

        // ── Delete (soft) ─────────────────────────────────────
        public async Task<bool> DeleteProfileAsync(int id)
        {
            const string sql = "UPDATE dbo.KitsuneConnections SET IsActive = 0 WHERE Id = @Id;";
            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ── Build connection string from stored profile ────────
        public async Task<string> GetConnectionStringAsync(int id)
        {
            const string sql = @"
                SELECT DatabaseType, Host, Port, DatabaseName, Username, PasswordEnc, TrustCert
                FROM dbo.KitsuneConnections WHERE Id = @Id;";

            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync())
                throw new Exception($"Connection profile {id} not found.");

            var dbType   = r["DatabaseType"].ToString()!;
            var host     = r["Host"].ToString()!;
            var port     = Convert.ToInt32(r["Port"]);
            var dbName   = r["DatabaseName"].ToString()!;
            var user     = r["Username"].ToString()!;
            var pwd      = Decrypt(r["PasswordEnc"].ToString()!);
            var trust    = Convert.ToBoolean(r["TrustCert"]);

            return dbType == "MongoDB"
                ? $"mongodb://{user}:{pwd}@{host}:{port}/{dbName}"
                : $"Server={host},{port};Database={dbName};User Id={user};Password={pwd};" +
                  $"TrustServerCertificate={(trust ? "True" : "False")};";
        }

        private async Task<string> GetDatabaseTypeAsync(int id)
        {
            const string sql = "SELECT DatabaseType FROM dbo.KitsuneConnections WHERE Id = @Id;";
            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return (await cmd.ExecuteScalarAsync())?.ToString() ?? "SqlServer";
        }

        // ── AES-256 encrypt / decrypt ─────────────────────────
        private static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var cipher     = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            var combined   = new byte[aes.IV.Length + cipher.Length];
            aes.IV.CopyTo(combined, 0);
            cipher.CopyTo(combined, aes.IV.Length);
            return Convert.ToBase64String(combined);
        }

        private static string Decrypt(string cipherB64)
        {
            if (string.IsNullOrEmpty(cipherB64)) return "";
            try
            {
                var combined = Convert.FromBase64String(cipherB64);
                using var aes = Aes.Create();
                aes.Key = _aesKey;
                var iv    = new byte[16];
                var cipher = new byte[combined.Length - 16];
                Array.Copy(combined, 0,  iv,     0, 16);
                Array.Copy(combined, 16, cipher, 0, cipher.Length);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return ""; }
        }
    }
}
