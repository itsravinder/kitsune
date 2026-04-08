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
        public async Task EnsureTableAsync()
        public async Task<UserPreferences> GetAsync(string userId = "default")
        public async Task SaveAsync(UserPreferences prefs, string userId = "default")
    }
}
