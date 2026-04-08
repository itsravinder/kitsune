// ============================================================
// KITSUNE – Apply Service
// Live execution with: pre-flight validation → auto-backup →
// apply DDL/DML → audit log → rollback on failure
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IApplyService
    {
        Task<ApplyResponse> ApplyAsync(ApplyRequest request);
    }

    public class ApplyService : IApplyService
    {
        private readonly string _conn;
        private readonly IDependencyValidationService _validation;
        private readonly IBackupVersioningService     _backup;
        private readonly IAuditLogService             _audit;
        private readonly ILogger<ApplyService>        _log;

        public ApplyService(
            IConfiguration cfg,
            IDependencyValidationService validation,
            IBackupVersioningService backup,
            IAuditLogService audit,
            ILogger<ApplyService> log)
        public async Task<ApplyResponse> ApplyAsync(ApplyRequest request)
        // Split T-SQL script on GO statements (batch separator)
        private static List<string> SplitOnGo(string script)
    }
}
