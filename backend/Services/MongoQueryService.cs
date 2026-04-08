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
        public async Task<MongoQueryResponse> ExecuteAsync(MongoQueryRequest request)
        private static async Task<MongoQueryResponse> RunFindAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        private static async Task<MongoQueryResponse> RunAggregateAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        private static async Task<MongoQueryResponse> RunCountAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        private static async Task<MongoQueryResponse> RunDistinctAsync(
            IMongoCollection<BsonDocument> col, MongoQueryRequest req, Stopwatch sw)
        public async Task<List<string>> ListDatabasesAsync()
        public async Task<List<string>> ListCollectionsAsync(string databaseName)
    }
}
