// ============================================================
// KITSUNE – Connection Manager Service
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
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public class SchemaTreeNode
    {
        public string               Id          { get; set; } = "";
        public string               Label       { get; set; } = "";
        public string               Type        { get; set; } = "";
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
            { "SqlServer",  1433  },
            { "MongoDB",    27017 },
            { "MySQL",      3306  },
            { "PostgreSQL", 5432  },
        };

        public ConnectionManagerService(IConfiguration cfg, ILogger<ConnectionManagerService> log)
        {
            _conn = cfg.GetConnectionString("SqlServer") ?? "";
            _log  = log;
        }

        public async Task EnsureTableAsync()
        {
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KitsuneConnections')
                    CREATE TABLE dbo.KitsuneConnections (
                        Id             INT IDENTITY(1,1) PRIMARY KEY,
                        ConnectionName NVARCHAR(200)  NOT NULL,
                        DatabaseType   NVARCHAR(50)   NOT NULL DEFAULT 'SqlServer',
                        Host           NVARCHAR(300)  NOT NULL,
                        Port           INT            NOT NULL DEFAULT 1433,
                        DatabaseName   NVARCHAR(200)  NOT NULL,
                        Username       NVARCHAR(200)  NOT NULL DEFAULT '',
                        PasswordEnc    NVARCHAR(500)  NOT NULL DEFAULT '',
                        TrustCert      BIT            NOT NULL DEFAULT 1,
                        LastTestedAt   DATETIME2      NULL,
                        LastTestOk     BIT            NOT NULL DEFAULT 0,
                        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
                    );";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await new SqlCommand(ddl, conn).ExecuteNonQueryAsync();
        }

        public async Task<List<ConnectionProfile>> ListProfilesAsync()
        {
            const string sql = @"
                SELECT Id, ConnectionName, DatabaseType, Host, Port, DatabaseName,
                       Username, TrustCert, LastTestedAt, LastTestOk, CreatedAt
                FROM dbo.KitsuneConnections ORDER BY ConnectionName;";
            var list = new List<ConnectionProfile>();
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new ConnectionProfile
                {
                    Id             = Convert.ToInt32(r["Id"]),
                    ConnectionName = r["ConnectionName"].ToString()!,
                    DatabaseType   = r["DatabaseType"].ToString()!,
                    Host           = r["Host"].ToString()!,
                    Port           = Convert.ToInt32(r["Port"]),
                    DatabaseName   = r["DatabaseName"].ToString()!,
                    Username       = r["Username"].ToString()!,
                    TrustCert      = Convert.ToBoolean(r["TrustCert"]),
                    LastTestedAt   = r["LastTestedAt"] == DBNull.Value ? null : Convert.ToDateTime(r["LastTestedAt"]),
                    LastTestOk     = Convert.ToBoolean(r["LastTestOk"]),
                    CreatedAt      = Convert.ToDateTime(r["CreatedAt"]),
                });
            return list;
        }

        public async Task<int> SaveProfileAsync(SaveProfileRequest req)
        {
            int port = req.Port > 0 ? req.Port
                       : DefaultPorts.GetValueOrDefault(req.DatabaseType, 1433);
            string enc = string.IsNullOrEmpty(req.Password) ? "" : Encrypt(req.Password);

            const string sql = @"
                INSERT INTO dbo.KitsuneConnections
                    (ConnectionName, DatabaseType, Host, Port, DatabaseName, Username, PasswordEnc, TrustCert)
                VALUES (@Name, @Type, @Host, @Port, @Db, @User, @Pwd, @Trust);
                SELECT SCOPE_IDENTITY();";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name",  req.Name);
            cmd.Parameters.AddWithValue("@Type",  req.DatabaseType);
            cmd.Parameters.AddWithValue("@Host",  req.Host);
            cmd.Parameters.AddWithValue("@Port",  port);
            cmd.Parameters.AddWithValue("@Db",    req.Database);
            cmd.Parameters.AddWithValue("@User",  req.Username ?? "");
            cmd.Parameters.AddWithValue("@Pwd",   enc);
            cmd.Parameters.AddWithValue("@Trust", req.TrustCertificate);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<ConnectionTestResult> TestProfileAsync(int id)
        {
            string cs = await GetConnectionStringAsync(id);
            string dt = await GetDatabaseTypeAsync(id);
            return await TestConnectionStringAsync(cs, dt);
        }

        public async Task<ConnectionTestResult> TestRawAsync(SaveProfileRequest req)
        {
            int port = req.Port > 0 ? req.Port : DefaultPorts.GetValueOrDefault(req.DatabaseType, 1433);
            string cs = BuildConnectionString(req.DatabaseType, req.Host, port,
                req.Database, req.Username ?? "", req.Password ?? "", req.TrustCertificate);
            return await TestConnectionStringAsync(cs, req.DatabaseType);
        }

        public async Task<ConnectionTestResult> TestConnectionStringAsync(string connStr, string dbType)
        {
            var result = new ConnectionTestResult();
            var sw     = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (dbType == "MongoDB")
                {
                    var client = new MongoClient(connStr);
                    var dbs    = await client.ListDatabaseNamesAsync();
                    await dbs.MoveNextAsync();
                    sw.Stop();
                    result.Success      = true;
                    result.Message      = "MongoDB connection successful.";
                    result.ResponseMs   = sw.Elapsed.TotalMilliseconds;
                    result.ServerVersion= "MongoDB";
                }
                else
                {
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    await using var cmd  = new SqlCommand("SELECT @@VERSION, DB_NAME();", conn);
                    await using var r    = await cmd.ExecuteReaderAsync();
                    string version = "", dbName = "";
                    if (await r.ReadAsync()) { version = r[0].ToString()!; dbName = r[1].ToString()!; }
                    sw.Stop();
                    result.Success      = true;
                    result.Message      = $"Connected to {dbName}.";
                    result.ServerVersion= version.Split('\n')[0].Trim();
                    result.ResponseMs   = sw.Elapsed.TotalMilliseconds;
                    result.DatabaseName = dbName;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success    = false;
                result.Message    = ex.Message;
                result.ResponseMs = sw.Elapsed.TotalMilliseconds;
            }
            return result;
        }

        public async Task<bool> DeleteProfileAsync(int id)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM dbo.KitsuneConnections WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<string> GetConnectionStringAsync(int id)
        {
            const string sql = "SELECT DatabaseType,Host,Port,DatabaseName,Username,PasswordEnc,TrustCert FROM dbo.KitsuneConnections WHERE Id=@Id;";
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) throw new InvalidOperationException($"Connection {id} not found.");
            string dbType = r["DatabaseType"].ToString()!;
            string host   = r["Host"].ToString()!;
            int    port   = Convert.ToInt32(r["Port"]);
            string db     = r["DatabaseName"].ToString()!;
            string user   = r["Username"].ToString()!;
            string enc    = r["PasswordEnc"].ToString()!;
            bool   trust  = Convert.ToBoolean(r["TrustCert"]);
            string pwd    = string.IsNullOrEmpty(enc) ? "" : Decrypt(enc);
            return BuildConnectionString(dbType, host, port, db, user, pwd, trust);
        }

        public async Task<SchemaTreeNode> GetSchemaTreeAsync(int connectionId)
        {
            string cs = await GetConnectionStringAsync(connectionId);
            string dt = await GetDatabaseTypeAsync(connectionId);
            var root  = new SchemaTreeNode { Id = "root", Label = dt, Type = "database", HasChildren = true };

            if (dt == "MongoDB")
            {
                var client = new MongoClient(cs);
                var dbList = await (await client.ListDatabaseNamesAsync()).ToListAsync();
                foreach (var d in dbList)
                    root.Children.Add(new SchemaTreeNode { Id = d, Label = d, Type = "folder", HasChildren = true });
                return root;
            }

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            root.Children.Add(await BuildFolderAsync(conn, "Tables", "table",
                "SELECT TABLE_SCHEMA AS s, TABLE_NAME AS n FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA,TABLE_NAME;"));
            root.Children.Add(await BuildFolderAsync(conn, "Views", "view",
                "SELECT TABLE_SCHEMA AS s, TABLE_NAME AS n FROM INFORMATION_SCHEMA.VIEWS ORDER BY TABLE_SCHEMA,TABLE_NAME;"));
            root.Children.Add(await BuildFolderAsync(conn, "Stored Procedures", "procedure",
                "SELECT ROUTINE_SCHEMA AS s, ROUTINE_NAME AS n FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE' ORDER BY ROUTINE_SCHEMA,ROUTINE_NAME;"));
            root.Children.Add(await BuildFolderAsync(conn, "Functions", "function",
                "SELECT ROUTINE_SCHEMA AS s, ROUTINE_NAME AS n FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='FUNCTION' ORDER BY ROUTINE_SCHEMA,ROUTINE_NAME;"));

            return root;
        }

        public async Task<string?> GetObjectDefinitionAsync(int connectionId, string objectName, string objectType)
        {
            string cs = await GetConnectionStringAsync(connectionId);
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT m.definition FROM sys.objects o INNER JOIN sys.sql_modules m ON m.object_id=o.object_id WHERE o.object_id=OBJECT_ID(@N);",
                conn);
            cmd.Parameters.AddWithValue("@N", objectName);
            return (await cmd.ExecuteScalarAsync())?.ToString();
        }

        private static async Task<SchemaTreeNode> BuildFolderAsync(SqlConnection conn, string label, string type, string sql)
        {
            var folder = new SchemaTreeNode { Id = label.ToLower(), Label = label, Type = "folder", HasChildren = true };
            await using var cmd = new SqlCommand(sql, conn);
            await using var r   = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string schema = r["s"].ToString()!;
                string name   = r["n"].ToString()!;
                folder.Children.Add(new SchemaTreeNode
                {
                    Id     = $"{schema}.{name}",
                    Label  = name,
                    Type   = type,
                    Schema = schema,
                });
            }
            return folder;
        }

        private async Task<string> GetDatabaseTypeAsync(int id)
        {
            await using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT DatabaseType FROM dbo.KitsuneConnections WHERE Id=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return (await cmd.ExecuteScalarAsync())?.ToString() ?? "SqlServer";
        }

        private static string BuildConnectionString(string dbType, string host, int port, string db, string user, string pwd, bool trustCert)
        {
            if (dbType == "MongoDB")
            {
                if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pwd))
                    return $"mongodb://{user}:{pwd}@{host}:{port}/{db}";
                return $"mongodb://{host}:{port}/{db}";
            }
            string auth = !string.IsNullOrEmpty(user)
                ? $"User Id={user};Password={pwd};"
                : "Trusted_Connection=True;";
            return $"Server={host},{port};Database={db};{auth}TrustServerCertificate={(trustCert ? "True" : "False")};MultipleActiveResultSets=True;";
        }

        private static string Encrypt(string plain)
        {
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc64 = enc.TransformFinalBlock(bytes, 0, bytes.Length);
            var result = new byte[aes.IV.Length + enc64.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(enc64, 0, result, aes.IV.Length, enc64.Length);
            return Convert.ToBase64String(result);
        }

        private static string Decrypt(string b64)
        {
            try
            {
                var raw = Convert.FromBase64String(b64);
                using var aes = Aes.Create();
                aes.Key = _aesKey;
                var iv  = new byte[16];
                Buffer.BlockCopy(raw, 0, iv, 0, 16);
                aes.IV  = iv;
                using var dec = aes.CreateDecryptor();
                var decBytes  = dec.TransformFinalBlock(raw, 16, raw.Length - 16);
                return Encoding.UTF8.GetString(decBytes);
            }
            catch { return ""; }
        }
    }
}
