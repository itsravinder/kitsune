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

using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
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
        public ConnectionManagerService(IConfiguration cfg, ILogger<ConnectionManagerService> log)
        public async Task EnsureTableAsync()
        public async Task<List<ConnectionProfile>> ListProfilesAsync()
        public async Task<int> SaveProfileAsync(SaveProfileRequest req)
        public async Task<ConnectionTestResult> TestProfileAsync(int id)
        public async Task<ConnectionTestResult> TestRawAsync(SaveProfileRequest req)
        public async Task<ConnectionTestResult> TestConnectionStringAsync(string connStr, string dbType)
        public async Task<bool> DeleteProfileAsync(int id)
        public async Task<string> GetConnectionStringAsync(int id)
        public async Task<SchemaTreeNode> GetSchemaTreeAsync(int connectionId)
        public async Task<string?> GetObjectDefinitionAsync(int connectionId, string objectName, string objectType)
        private static async Task<SchemaTreeNode> BuildFolderAsync(SqlConnection conn, string label, string type, string sql)
        private async Task<string> GetDatabaseTypeAsync(int id)
        private static string BuildConnectionString(string dbType, string host, int port, string db, string user, string pwd, bool trustCert) =>
            dbType switch
            {
                "MongoDB" => $"mongodb://{user}:{pwd}@{host}:{port}/{db}",
                _         => $"Server={host},{port};Database={db};User Id={user};Password={pwd};TrustServerCertificate={(trustCert?"True":"False")};",
            };

        private static string Encrypt(string plain)
        private static string Decrypt(string b64)
    }
}
