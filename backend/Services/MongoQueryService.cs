// ============================================================
// KITSUNE – MongoDB Query Service
// Execute aggregation pipelines and find queries safely
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Kitsune.Backend.Services
{
    public class MongoQueryRequest
    {
        public string DatabaseName   { get; set; } = "";
        public string CollectionName { get; set; } = "";
        public string QueryJson      { get; set; } = "{}";   // find filter OR pipeline array
        public string QueryType      { get; set; } = "find"; // find | aggregate | count | distinct
        public string DistinctField  { get; set; } = "";
        public int    Limit          { get; set; } = 100;
        public bool   SafeMode       { get; set; } = true;   // read-only enforcement
    }

    public class MongoQueryResponse
    {
        public bool              Success      { get; set; }
        public string            Mode         { get; set; } = "SAFE_READ";
        public List<string>      ResultJson   { get; set; } = new(); // each doc as JSON string
        public int               RowCount     { get; set; }
        public double            ExecutionMs  { get; set; }
        public List<string>      Errors       { get; set; } = new();
        public List<string>      Columns      { get; set; } = new(); // inferred field names
    }

    public interface IMongoQueryService
    {
        Task<MongoQueryResponse> ExecuteAsync(MongoQueryRequest request);
        Task<List<string>>       ListDatabasesAsync();
        Task<List<string>>       ListCollectionsAsync(string databaseName);
    }

    public class MongoQueryService : IMongoQueryService
    {
        private readonly MongoClient _client;
        private readonly ILogger<MongoQueryService> _log;

        public MongoQueryService(IConfiguration cfg, ILogger<MongoQueryService> log)
        {
            var connStr = cfg.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
            _client = new MongoClient(connStr);
            _log    = log;
        }

        public async Task<MongoQueryResponse> ExecuteAsync(MongoQueryRequest request)
        {
            var sw       = Stopwatch.StartNew();
            var response = new MongoQueryResponse { Mode = request.SafeMode ? "SAFE_READ" : "LIVE" };

            try
            {
                var db  = _client.GetDatabase(request.DatabaseName);
                var col = db.GetCollection<BsonDocument>(request.CollectionName);

                switch (request.QueryType.ToLowerInvariant())
                {
                    case "aggregate":
                        response = await RunAggregateAsync(col, request, sw);
                        break;
                    case "count":
                        response = await RunCountAsync(col, request, sw);
                        break;
                    case "distinct":
                        response = await RunDistinctAsync(col, request, sw);
                        break;
                    default:
                        response = await RunFindAsync(col, request, sw);
                        break;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                response.Success     = false;
                response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
                response.Errors.Add($"MongoDB error: {ex.Message}");
                _log.LogError(ex, "MongoDB query failed");
            }

            return response;
        }

        private static async Task<MongoQueryResponse> RunFindAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var response = new MongoQueryResponse { Mode = "SAFE_READ" };
            var filter   = BsonDocument.Parse(req.QueryJson);
            var docs     = await col.Find(filter).Limit(req.Limit).ToListAsync();
            sw.Stop();

            var fieldSet = new HashSet<string>();
            foreach (var doc in docs)
            {
                var json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson });
                response.ResultJson.Add(json);
                foreach (var elem in doc.Elements) fieldSet.Add(elem.Name);
            }

            response.Success     = true;
            response.RowCount    = docs.Count;
            response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
            response.Columns     = new List<string>(fieldSet);
            return response;
        }

        private static async Task<MongoQueryResponse> RunAggregateAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var response  = new MongoQueryResponse { Mode = "SAFE_READ" };
            var pipeArray = BsonSerializer.Deserialize<BsonArray>(req.QueryJson);
            var pipeline  = new List<BsonDocument>();
            foreach (BsonDocument stage in pipeArray) pipeline.Add(stage);

            var cursor = await col.AggregateAsync<BsonDocument>(pipeline);
            var docs   = await cursor.ToListAsync();
            sw.Stop();

            var fieldSet = new HashSet<string>();
            foreach (var doc in docs)
            {
                var json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson });
                response.ResultJson.Add(json);
                foreach (var elem in doc.Elements) fieldSet.Add(elem.Name);
            }

            response.Success     = true;
            response.RowCount    = docs.Count;
            response.ExecutionMs = sw.Elapsed.TotalMilliseconds;
            response.Columns     = new List<string>(fieldSet);
            return response;
        }

        private static async Task<MongoQueryResponse> RunCountAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var filter = BsonDocument.Parse(req.QueryJson);
            var count  = await col.CountDocumentsAsync(filter);
            sw.Stop();
            return new MongoQueryResponse
            {
                Success     = true,
                Mode        = "SAFE_READ",
                ResultJson  = new List<string> { $"{{\"count\":{count}}}" },
                RowCount    = 1,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
                Columns     = new List<string> { "count" },
            };
        }

        private static async Task<MongoQueryResponse> RunDistinctAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var filter  = BsonDocument.Parse(req.QueryJson);
            var values  = await col.DistinctAsync<BsonValue>(req.DistinctField, filter);
            var list    = await values.ToListAsync();
            sw.Stop();

            var results = new List<string>();
            foreach (var v in list)
                results.Add(v.ToJson());

            return new MongoQueryResponse
            {
                Success     = true,
                Mode        = "SAFE_READ",
                ResultJson  = results,
                RowCount    = results.Count,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
                Columns     = new List<string> { req.DistinctField },
            };
        }

        public async Task<List<string>> ListDatabasesAsync()
        {
            var cursor = await _client.ListDatabaseNamesAsync();
            return await cursor.ToListAsync();
        }

        public async Task<List<string>> ListCollectionsAsync(string databaseName)
        {
            var db     = _client.GetDatabase(databaseName);
            var cursor = await db.ListCollectionNamesAsync();
            return await cursor.ToListAsync();
        }
    }
}
