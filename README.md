# 🦊 KITSUNE – AI Database Intelligence System

Enterprise-grade AI-powered database management: NL→SQL/NoSQL, dependency validation, versioning, safe preview, risk analysis, query optimization, scheduled backups, MongoDB support, and more.

---

## 📁 Complete Project Structure

```
kitsune/
├── backend/                                  # .NET 8 Web API
│   ├── Controllers/
│   │   ├── KitsuneControllers.cs             # Validate / Backup / Preview / Rollback
│   │   ├── SchemaAuditControllers.cs         # Schema / Audit
│   │   ├── ApplyChangeSummaryControllers.cs  # Apply / Diff / Connections
│   │   ├── OptimizerController.cs            # Query plan + missing indexes
│   │   └── ExtendedControllers.cs            # MongoDB / Schedules / Prefs / Health
│   ├── Services/
│   │   ├── DependencyValidationService.cs
│   │   ├── BackupVersioningService.cs
│   │   ├── PreviewExecutionService.cs
│   │   ├── SchemaExtractionService.cs
│   │   ├── AuditLogService.cs
│   │   ├── ApplyService.cs
│   │   ├── ChangeSummaryService.cs
│   │   ├── ConnectionManagerService.cs
│   │   ├── QueryOptimizerService.cs
│   │   ├── MongoQueryService.cs
│   │   ├── ScheduledBackupService.cs
│   │   └── UserPreferencesService.cs
│   ├── Models/Models.cs
│   ├── Data/ValidationQueries.sql
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Kitsune.Backend.csproj
├── ai-service/
│   ├── main.py
│   ├── requirements.txt
│   ├── Dockerfile
│   └── .env.example
├── ui/
│   ├── src/
│   │   ├── KitsuneApp.jsx
│   │   ├── App.js / index.js
│   │   ├── hooks/useKitsune.js
│   │   ├── components/
│   │   │   ├── SharedComponents.jsx
│   │   │   ├── LeftPane.jsx
│   │   │   └── Panels.jsx
│   │   └── services/api.js
│   ├── public/index.html
│   ├── package.json / Dockerfile / nginx.conf
│   └── .env.example
├── setup-kitsune.ps1      # Windows one-click setup
├── setup-kitsune.sh       # Linux/Mac one-click setup
├── docker-compose.yml
├── .gitignore
└── README.md
```

---

## 🚀 Quick Start

### One-command setup

**Windows (PowerShell):**
```powershell
pwsh -ExecutionPolicy Bypass -File setup-kitsune.ps1
```

**Linux/Mac:**
```bash
chmod +x setup-kitsune.sh && ./setup-kitsune.sh
```

### Manual start
```bash
# Pull AI models first
ollama pull defog/sqlcoder
ollama pull qwen3-coder:480b-cloud

# Backend (.NET 8)
cd backend && dotnet restore && dotnet run
# http://localhost:5000/swagger

# AI Service (Python)
cd ai-service && pip install -r requirements.txt
uvicorn main:app --port 8000 --reload

# UI (React)
cd ui && npm install && npm start
# http://localhost:3000

# OR: everything via Docker
docker compose up -d
```

---

## 📡 Complete API Reference

### Dependency Validation
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/validate` | Validate change + recursive dependency tree |
| GET | `/api/validate/dependencies/{name}` | Full dependency graph |
| GET | `/api/validate/parameters/{name}` | SP/Function parameters |
| GET | `/api/validate/exists/{name}` | Object existence check |

### Backup & Versioning
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/backup` | Backup current object (keeps last 3 versions) |
| GET | `/api/versions/{name}` | Version history |
| GET | `/api/versions/{name}/definition` | Current live definition |
| POST | `/api/rollback` | Restore version (auto-backs up current first) |

### Preview & Apply
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/preview` | Safe execute: BEGIN TRAN → ROLLBACK, no persistence |
| POST | `/api/apply` | Live: validate → backup → execute → audit |

### Schema
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/schema/sqlserver?db=X` | Full SQL Server schema |
| GET | `/api/schema/mongodb/{db}` | MongoDB schema via sampling |
| GET | `/api/schema/table/{name}` | Single table detail |
| GET | `/api/schema/ddl?db=X` | DDL string for AI context |

### Change Summary / Diff
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/changesummary/compare` | LCS diff + AI summary |
| GET | `/api/changesummary/{name}/{vA}/{vB}` | Compare stored versions |

### Query Optimizer
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/optimizer/analyze` | Execution plan XML + heuristic tips |
| GET | `/api/optimizer/missing-indexes` | DMV missing index hints |

### MongoDB
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/mongo/query` | find / aggregate / count / distinct |
| GET | `/api/mongo/databases` | List databases |
| GET | `/api/mongo/databases/{db}/collections` | List collections |

### Connection Manager
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/connections` | List profiles (passwords masked) |
| POST | `/api/connections` | Save profile (AES-256 encrypted) |
| POST | `/api/connections/{id}/test` | Test + measure latency |
| POST | `/api/connections/test-string` | Test raw connection string |
| DELETE | `/api/connections/{id}` | Soft-delete |

### Scheduled Backups
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/schedules` | List schedules |
| POST | `/api/schedules` | Add schedule |
| PATCH | `/api/schedules/{id}/toggle?enabled=true` | Enable/disable |
| DELETE | `/api/schedules/{id}` | Delete |

### Preferences & Audit
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/preferences` | Load preferences |
| PUT | `/api/preferences` | Save preferences |
| GET | `/api/audit?objectName=X&top=100` | Audit log |
| GET | `/api/healthdashboard` | System snapshot |
| GET | `/health` | Health check |

### AI Service (port 8000)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/generate` | NL → SQL/MongoDB (with auto-routing + fallback) |
| GET | `/models` | Available Ollama models |
| POST | `/explain` | Plain-English query explanation |
| POST | `/risk` | Risk: data loss / performance / security |
| POST | `/summarize-change` | AI diff summary |
| POST | `/schema-context` | Answer questions using schema |
| GET | `/health` | AI + Ollama status |

---

## 🗄 Auto-Created Database Tables

| Table | Purpose |
|-------|---------|
| `dbo.ObjectVersions` | Object version history (last 3) |
| `dbo.KitsuneAuditLog` | Full audit trail |
| `dbo.KitsuneConnections` | Encrypted connection profiles |
| `dbo.KitsuneBackupSchedules` | Scheduled backup jobs |
| `dbo.KitsuneUserPrefs` | User preferences JSON |

---

## 🤖 AI Model Routing

| Scenario | Model |
|----------|-------|
| Simple SELECT / JOINs | SQLCoder (local, fast) |
| Stored procedures, CTEs, optimization | Qwen3 480B (cloud) |
| MongoDB / NoSQL | Qwen3 480B |
| Either model fails | Auto-fallback to the other |
| Manual | User override via dropdown |

---

## 📦 GitHub

```bash
cd kitsune
git remote add origin https://github.com/itsravinder/kitsune.git
git push -u origin main
```

---

MIT © KITSUNE Project
