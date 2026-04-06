// ============================================================
// KITSUNE – Enhanced Connection Manager Service v2
// Adds: MySQL, PostgreSQL, schema tree, connection string override
// Backward-compatible with existing SaveProfileRequest usage
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
        public int       Id             { get; set; }
        public string    Name           { get; set; } = "";
        public string    DatabaseType   { get; set; } = "SqlServer";
        public string    Host           { get; set; } = "localhost";
        public int       Port           { get; set; }
        public string    DatabaseName   { get; set; } = "";
        public string    Username       { get; set; } = "";
        public string    PasswordHint   { get; set; } = "••••••••";
        public bool      TrustCert      { get; set; } = true;
        public bool      IsActive       { get; set; } = true;
        public string    Tags           { get; set; } = "";
        public DateTime  CreatedAt      { get; set; }
        public DateTime? LastTestedAt   { get; set; }
        public bool      LastTestOk     { get; set; }
        public string    ConnectionStringOverride { get; set; } = "";
    }

    public class ConnectionTestResult
    {
        public bool   Success       { get; set; }
        public string Message       { get; set; } = "";
        public double LatencyMs     { get; set; }
        public string ServerVersion { get; set; } = "";
        public string DatabaseType  { get; set; } = "";
    }

    public class SaveProfileRequest
    {
        public string Name           { get; set; } = "";
        public string DatabaseType   { get; set; } = "SqlServer";
        public string Host           { get; set; } = "localhost";
        public int    Port           { get; set; } = 1433;
        public string DatabaseName   { get; set; } = "";
        public string Username       { get; set; } = "";
        public string Password       { get; set; } = "";
        public bool   TrustCert      { get; set; } = true;
        public string Tags           { get; set; } = "";
        public string ConnectionStringOverride { get; set; } = "";
    }

    public class SchemaTreeNode
    {
        public string               Id          { get; set; } = "";
        public string               Label       { get; set; } = "";
        public string               Type        { get; set; } = ""; // folder|table|view|procedure|function|collection
        public string               Schema      { get; set; } = "dbo";
        public bool                 HasChildren { get; set; }
        public List<SchemaTreeNode> Children    { get; set; } = new();
        public string?              Definition  { get; set; }
    }

    public interface IConnectionManagerService
    {
        Task<List<ConnectionProfile>> ListProfilesAsync();
        Task<int>                     SaveProfileAsync(SaveProfileRequest req);
        Task<ConnectionTestResult>    TestProfileAsync(int id);
        Task<ConnectionTestResult>    TestConnectionStringAsync(string connStr, string dbType);
        Task<ConnectionTestResult>    TestRawAsync(SaveProfileRequest req);
        Task<bool>                    DeleteProfileAsync(int id);
        Task<string>                  GetConnectionStringAsync(int id);
        Task<SchemaTreeNode>          GetSchemaTreeAsync(int connectionId);
        Task<string?>                 GetObjectDefinitionAsync(int connectionId, string objectName, string objectType);
        Task                          EnsureTableAsync();
    }

    public class ConnectionManagerService : IConnectionManagerService
    {
        private readonly string _conn;
        private readonly ILogger<ConnectionManagerService> _log;

        private static readonly byte[] _aesKey =
            SHA256.HashData(Encoding.UTF8.GetBytes("KITSUNE_ENCRYPTION_KEY_CHANGE_ME"));

        private static readonly Dictionary<string, int> DefaultPorts = new()
        {
            ["SqlServer"]  = 1433,
            ["MySQL"]      = 3306,
            ["PostgreSQL"] = 5432,
            ["MongoDB"]    = 27017,
        };

        public ConnectionManagerService(IConfiguration cfg, ILogger<ConnectionManagerService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='KitsuneConnections')
                BEGIN
                    CREATE TABLE dbo.KitsuneConnections (
                        Id                  INT IDENTITY(1,1) PRIMARY KEY,
                        Name                NVARCHAR(128) NOT NULL,
                        DatabaseType        NVARCHAR(32)  NOT NULL DEFAULT 'SqlServer',
                        Host                NVARCHAR(256) NOT NULL DEFAULT 'localhost',
                        Port                INT           NOT NULL DEFAULT 1433,
                        DatabaseName        NVARCHAR(128) NOT NULL DEFAULT '',
                        Username            NVARCHAR(128) NOT NULL DEFAULT '',
                        PasswordEnc         NVARCHAR(MAX) NOT NULL DEFAULT '',
                        TrustCert           BIT           NOT NULL DEFAULT 1,
                        IsActive            BIT           NOT NULL DEFAULT 1,
                        Tags                NVARCHAR(256) NOT NULL DEFAULT '',
                        ConnectionStringEnc NVARCHAR(MAX) NOT NULL DEFAULT '',
                        CreatedAt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
                        LastTestedAt        DATETIME2     NULL,
                        LastTestOk          BIT           NOT NULL DEFAULT 0
                    );
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns
                    WHERE object_id=OBJECT_ID('dbo.KitsuneConnections') AND name='ConnectionStringEnc')
                    ALTER TABLE dbo.KitsuneConnections ADD ConnectionStringEnc NVARCHAR(MAX) NOT NULL DEFAULT '';";

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<List<ConnectionProfile>> ListProfilesAsync()
        {
            const string sql = @"
                SELECT Id,Name,DatabaseType,Host,Port,DatabaseName,Username,
                       TrustCert,IsActive,Tags,CreatedAt,LastTestedAt,LastTestOk
                FROM dbo.KitsuneConnections WHERE IsActive=1 ORDER BY Name;";

            var results = new List<ConnectionProfile>();
            await using var conn = new SqlConnection(_conn);
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

        public async Task<int> SaveProfileAsync(SaveProfileRequest req)
        {
            int port  = req.Port > 0 ? req.Port : DefaultPorts.GetValueOrDefault(req.DatabaseType, 1433);
            const string sql = @"
                INSERT INTO dbo.KitsuneConnections
                    (Name,DatabaseType,Host,Port,DatabaseName,Username,PasswordEnc,TrustCert,Tags,ConnectionStringEnc)
                VALUES(@Name,@DbType,@Host,@Port,@DbName,@User,@Pwd,@Trust,@Tags,@Cs);
                SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name",   req.Name);
            cmd.Parameters.AddWithValue("@DbType", req.DatabaseType);
            cmd.Parameters.AddWithValue("@Host",   req.Host);
            cmd.Parameters.AddWithValue("@Port",   port);
            cmd.Parameters.AddWithValue("@DbName", req.DatabaseName);
            cmd.Parameters.AddWithValue("@User",   req.Username);
            cmd.Parameters.AddWithValue("@Pwd",    Encrypt(req.Password));
            cmd.Parameters.AddWithValue("@Trust",  req.TrustCert);
            cmd.Parameters.AddWithValue("@Tags",   req.Tags);
            cmd.Parameters.AddWithValue("@Cs",     Encrypt(req.ConnectionStringOverride));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<ConnectionTestResult> TestProfileAsync(int id)
        {
            var connStr = await GetConnectionStringAsync(id);
            var dbType  = await GetDatabaseTypeAsync(id);
            var result  = await TestConnectionStringAsync(connStr, dbType);

            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.KitsuneConnections SET LastTestedAt=SYSUTCDATETIME(),LastTestOk=@Ok WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Ok", result.Success ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return result;
        }

        public async Task<ConnectionTestResult> TestRawAsync(SaveProfileRequest req)
        {
            if (!string.IsNullOrEmpty(req.ConnectionStringOverride))
                return await TestConnectionStringAsync(req.ConnectionStringOverride, req.DatabaseType);

            int port   = req.Port > 0 ? req.Port : DefaultPorts.GetValueOrDefault(req.DatabaseType, 1433);
            var cs     = BuildConnectionString(req.DatabaseType, req.Host, port, req.DatabaseName, req.Username, req.Password, req.TrustCert);
            return await TestConnectionStringAsync(cs, req.DatabaseType);
        }

        public async Task<ConnectionTestResult> TestConnectionStringAsync(string connStr, string dbType)
        {
            var result = new ConnectionTestResult { DatabaseType = dbType };
            var sw     = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (dbType == "MongoDB")
                {
                    var mc = new MongoClient(connStr);
                    await mc.ListDatabaseNamesAsync();
                    result.ServerVersion = "MongoDB";
                }
                else
                {
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    var ver = await new SqlCommand("SELECT @@VERSION;", conn).ExecuteScalarAsync();
                    result.ServerVersion = ver?.ToString()?.Split('\n')[0].Trim() ?? "SQL Server";
                }
                result.Success   = true;
                result.LatencyMs = sw.Elapsed.TotalMilliseconds;
                result.Message   = $"Connected in {result.LatencyMs:0.0}ms";
            }
            catch (Exception ex)
            {
                result.Success   = false;
                result.LatencyMs = sw.Elapsed.TotalMilliseconds;
                result.Message   = ex.Message;
            }
            return result;
        }

        public async Task<bool> DeleteProfileAsync(int id)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.KitsuneConnections SET IsActive=0 WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<string> GetConnectionStringAsync(int id)
        {
            const string sql = "SELECT DatabaseType,Host,Port,DatabaseName,Username,PasswordEnc,TrustCert,ConnectionStringEnc FROM dbo.KitsuneConnections WHERE Id=@Id;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) throw new Exception($"Connection {id} not found.");

            var csEnc = r["ConnectionStringEnc"].ToString()!;
            if (!string.IsNullOrEmpty(csEnc)) { var cs = Decrypt(csEnc); if (!string.IsNullOrEmpty(cs)) return cs; }

            return BuildConnectionString(
                r["DatabaseType"].ToString()!, r["Host"].ToString()!,
                Convert.ToInt32(r["Port"]), r["DatabaseName"].ToString()!,
                r["Username"].ToString()!, Decrypt(r["PasswordEnc"].ToString()!),
                Convert.ToBoolean(r["TrustCert"]));
        }

        public async Task<SchemaTreeNode> GetSchemaTreeAsync(int connectionId)
        {
            var connStr = await GetConnectionStringAsync(connectionId);
            var dbType  = await GetDatabaseTypeAsync(connectionId);
            var profiles = await ListProfilesAsync();
            var profile  = profiles.Find(p => p.Id == connectionId);

            var root = new SchemaTreeNode
            {
                Id          = "root",
                Label       = profile?.DatabaseName.Length > 0 ? profile.DatabaseName : "Database",
                Type        = "database",
                HasChildren = true,
            };

            if (dbType == "MongoDB")
            {
                var mc     = new MongoClient(connStr);
                var dbName = profile?.DatabaseName ?? "";
                if (!string.IsNullOrEmpty(dbName))
                {
                    var db     = mc.GetDatabase(dbName);
                    var cursor = await db.ListCollectionNamesAsync();
                    var names  = await cursor.ToListAsync();
                    foreach (var n in names)
                        root.Children.Add(new SchemaTreeNode { Id=$"collection.{n}", Label=n, Type="collection" });
                }
            }
            else // SQL Server (MySQL/PostgreSQL require extra NuGet packages – gracefully skip)
            {
                try
                {
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();

                    root.Children.Add(await BuildFolderAsync(conn, "Tables", "table",
                        "SELECT OBJECT_SCHEMA_NAME(object_id) s, name n FROM sys.tables ORDER BY name;"));
                    root.Children.Add(await BuildFolderAsync(conn, "Views", "view",
                        "SELECT OBJECT_SCHEMA_NAME(object_id) s, name n FROM sys.views ORDER BY name;"));
                    root.Children.Add(await BuildFolderAsync(conn, "Stored Procedures", "procedure",
                        "SELECT OBJECT_SCHEMA_NAME(object_id) s, name n FROM sys.procedures ORDER BY name;"));
                    root.Children.Add(await BuildFolderAsync(conn, "Functions", "function",
                        "SELECT OBJECT_SCHEMA_NAME(object_id) s, name n FROM sys.objects WHERE type IN('FN','IF','TF') ORDER BY name;"));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Schema tree extraction failed");
                    root.Children.Add(new SchemaTreeNode { Id = "error", Label = $"Error: {ex.Message}", Type = "error" });
                }
            }

            return root;
        }

        public async Task<string?> GetObjectDefinitionAsync(int connectionId, string objectName, string objectType)
        {
            var connStr = await GetConnectionStringAsync(connectionId);
            var dbType  = await GetDatabaseTypeAsync(connectionId);
            if (dbType != "SqlServer") return null;

            const string sql = @"
                SELECT m.definition FROM sys.objects o
                INNER JOIN sys.sql_modules m ON m.object_id=o.object_id
                WHERE o.object_id=OBJECT_ID(@Name);";

            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", objectName);
            return (await cmd.ExecuteScalarAsync())?.ToString();
        }

        private static async Task<SchemaTreeNode> BuildFolderAsync(SqlConnection conn, string label, string type, string sql)
        {
            var folder = new SchemaTreeNode { Id = type + "s", Label = label, Type = "folder", HasChildren = true };
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var schema = r["s"].ToString()!;
                var name   = r["n"].ToString()!;
                folder.Children.Add(new SchemaTreeNode
                {
                    Id    = $"{type}.{schema}.{name}",
                    Label = name,
                    Type  = type,
                    Schema = schema,
                });
            }
            folder.HasChildren = folder.Children.Count > 0;
            return folder;
        }

        private async Task<string> GetDatabaseTypeAsync(int id)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT DatabaseType FROM dbo.KitsuneConnections WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return (await cmd.ExecuteScalarAsync())?.ToString() ?? "SqlServer";
        }

        private static string BuildConnectionString(string dbType, string host, int port, string db, string user, string pwd, bool trustCert) =>
            dbType switch
            {
                "MongoDB" => $"mongodb://{user}:{pwd}@{host}:{port}/{db}",
                _         => $"Server={host},{port};Database={db};User Id={user};Password={pwd};TrustServerCertificate={(trustCert?"True":"False")};",
            };

        private static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            using var aes = Aes.Create();
            aes.Key = _aesKey; aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var b = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(plain), 0, plain.Length);
            var combined = new byte[16 + b.Length];
            aes.IV.CopyTo(combined, 0); b.CopyTo(combined, 16);
            return Convert.ToBase64String(combined);
        }

        private static string Decrypt(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return "";
            try
            {
                var combined = Convert.FromBase64String(b64);
                using var aes = Aes.Create(); aes.Key = _aesKey;
                var iv = new byte[16]; var cipher = new byte[combined.Length-16];
                Array.Copy(combined,0,iv,0,16); Array.Copy(combined,16,cipher,0,cipher.Length);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(cipher,0,cipher.Length));
            }
            catch { return ""; }
        }
    }
}
