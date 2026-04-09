// ============================================================
// KITSUNE – MongoDB Query Service
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
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
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
            var cs   = cfg.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
            _client  = new MongoClient(cs);
            _log     = log;
        }

        public async Task<MongoQueryResponse> ExecuteAsync(MongoQueryRequest request)
        {
            var db  = _client.GetDatabase(request.Database);
            var col = db.GetCollection<BsonDocument>(request.Collection);
            var sw  = Stopwatch.StartNew();
            try
            {
                MongoQueryResponse resp = request.QueryType.ToLower() switch
                {
                    "aggregate" => await RunAggregateAsync(col, request, sw),
                    "count"     => await RunCountAsync(col, request, sw),
                    "distinct"  => await RunDistinctAsync(col, request, sw),
                    _           => await RunFindAsync(col, request, sw),
                };
                return resp;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _log.LogError(ex, "Mongo query failed");
                return new MongoQueryResponse
                {
                    Success     = false,
                    Error       = ex.Message,
                    ExecutionMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        }

        private static async Task<MongoQueryResponse> RunFindAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var filter = string.IsNullOrWhiteSpace(req.Query)
                ? FilterDefinition<BsonDocument>.Empty
                : new JsonFilterDefinition<BsonDocument>(req.Query);

            var cursor = await col.Find(filter)
                .Limit(req.Limit > 0 ? req.Limit : 100)
                .ToCursorAsync();

            var docs = await cursor.ToListAsync();
            sw.Stop();
            return new MongoQueryResponse
            {
                Success     = true,
                Documents   = docs.ConvertAll(d => BsonToJson(d)),
                Count       = docs.Count,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        private static async Task<MongoQueryResponse> RunAggregateAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var pipeline = BsonSerializer.Deserialize<BsonArray>(req.Query ?? "[]")
                .Select(s => BsonDocument.Parse(s.ToJson())).ToList();
            var cursor = await col.AggregateAsync<BsonDocument>(pipeline);
            var docs   = await cursor.ToListAsync();
            sw.Stop();
            return new MongoQueryResponse
            {
                Success     = true,
                Documents   = docs.ConvertAll(d => BsonToJson(d)),
                Count       = docs.Count,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        private static async Task<MongoQueryResponse> RunCountAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var filter = string.IsNullOrWhiteSpace(req.Query)
                ? FilterDefinition<BsonDocument>.Empty
                : new JsonFilterDefinition<BsonDocument>(req.Query);
            long count = await col.CountDocumentsAsync(filter);
            sw.Stop();
            return new MongoQueryResponse
            {
                Success     = true,
                Documents   = new List<string> { $"{{ \"count\": {count} }}" },
                Count       = (int)count,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        private static async Task<MongoQueryResponse> RunDistinctAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        {
            var field   = req.Query ?? "_id";
            var cursor  = await col.DistinctAsync<BsonValue>(field, FilterDefinition<BsonDocument>.Empty);
            var values  = await cursor.ToListAsync();
            sw.Stop();
            return new MongoQueryResponse
            {
                Success     = true,
                Documents   = values.ConvertAll(v => v.ToJson()),
                Count       = values.Count,
                ExecutionMs = sw.Elapsed.TotalMilliseconds,
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

        private static string BsonToJson(BsonDocument doc)
        {
            var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson };
            return doc.ToJson(settings);
        }
    }
}
