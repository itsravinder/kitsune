# 🦊 KITSUNE – AI Database Intelligence System

An enterprise-grade AI-powered database management tool with natural language → SQL generation,
dependency validation, versioning, safe preview execution, and risk analysis.

---

## 📁 Project Structure

```
kitsune/
├── backend/                    # .NET 8 Web API
│   ├── Controllers/
│   │   └── KitsuneControllers.cs   # Validate / Backup / Preview / Rollback endpoints
│   ├── Services/
│   │   ├── DependencyValidationService.cs
│   │   ├── BackupVersioningService.cs
│   │   └── PreviewExecutionService.cs
│   ├── Models/
│   │   └── Models.cs
│   ├── Data/
│   │   └── ValidationQueries.sql   # Reference SQL queries
│   ├── Program.cs
│   ├── appsettings.json
│   └── Kitsune.Backend.csproj
│
├── ai-service/                 # Python FastAPI
│   ├── main.py                 # Model routing + Ollama integration
│   └── requirements.txt
│
└── ui/                         # React (SSMS-like interface)
    ├── src/
    │   ├── KitsuneApp.jsx      # Main UI component
    │   └── services/api.js     # API layer
    └── package.json
```

---

## 🚀 Quick Start

### 1. Prerequisites

| Tool              | Version  |
|-------------------|----------|
| .NET SDK          | 8.0+     |
| Python            | 3.11+    |
| Node.js           | 18+      |
| SQL Server        | 2019+    |
| Ollama            | latest   |

### 2. Pull AI Models via Ollama

```bash
ollama pull defog/sqlcoder
ollama pull qwen3-coder:480b-cloud
```

### 3. Backend (.NET)

```bash
cd backend
# Update connection string in appsettings.json
dotnet restore
dotnet run
# Swagger UI: http://localhost:5000/swagger
```

### 4. AI Service (Python)

```bash
cd ai-service
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
# Health check: http://localhost:8000/health
```

### 5. UI (React)

```bash
cd ui
npm install
npm start
# App: http://localhost:3000
```

---

## 📡 API Reference

### Dependency Validation

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST   | `/api/validate` | Validate object change + dependency tree |
| GET    | `/api/validate/dependencies/{name}` | Full dependency tree |
| GET    | `/api/validate/parameters/{name}` | SP/Function parameters |
| GET    | `/api/validate/exists/{name}` | Object existence check |

**POST /api/validate**
```json
{
  "objectName": "usp_GetCustomerOrders",
  "objectType": "PROCEDURE",
  "newDefinition": "CREATE PROCEDURE usp_GetCustomerOrders ..."
}
```
**Response:**
```json
{
  "status": "WARN",
  "affectedObjects": [
    {
      "affectedName": "usp_CustomerReport",
      "affectedType": "SQL_STORED_PROCEDURE",
      "schemaName": "dbo",
      "depth": 1,
      "dependencyPath": "usp_CustomerReport"
    }
  ],
  "message": "1 dependent object(s) will be affected.",
  "warnings": []
}
```

---

### Backup & Versioning

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST   | `/api/backup` | Backup current object definition |
| GET    | `/api/versions/{name}` | Get last 3 versions |
| GET    | `/api/versions/{name}/definition` | Get current live definition |
| POST   | `/api/rollback` | Restore object to selected version |

**POST /api/backup**
```json
{ "objectName": "usp_GetOrders", "objectType": "PROCEDURE" }
```

**POST /api/rollback**
```json
{ "objectName": "usp_GetOrders", "versionNumber": 2 }
```

---

### Preview Execution (Safe Mode)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST   | `/api/preview` | Execute in BEGIN TRAN / ROLLBACK – zero persistence |

**POST /api/preview**
```json
{
  "sqlQuery": "UPDATE Orders SET Status='Shipped' WHERE OrderId=1",
  "isStoredProc": false,
  "timeoutSeconds": 30
}
```
**Response:**
```json
{
  "success": true,
  "resultSet": [],
  "columns": [],
  "rowCount": 0,
  "executionMs": 12.4,
  "errors": [],
  "messages": ["(1 row affected)"],
  "mode": "SAFE_PREVIEW"
}
```

---

### AI Service

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST   | `/generate` | NL → SQL/NoSQL |
| GET    | `/models` | List available models |
| POST   | `/explain` | Explain a query in plain English |
| POST   | `/risk` | Risk analysis (data loss, perf, security) |

---

## 🧪 Model Routing Logic

| Query Complexity | Model Selected |
|-----------------|----------------|
| MongoDB queries | Qwen3 480B (cloud) |
| Stored procedures / CTEs | Qwen3 480B (cloud) |
| Simple SELECT / JOINs | SQLCoder (local) |
| Manual selection | User choice |

---

## 🛡️ Safety Features

- **Preview Mode**: Every execution wrapped in `BEGIN TRAN / ROLLBACK` – no data ever persists
- **Destructive DDL Blocking**: `DROP DATABASE`, `TRUNCATE`, `DROP TABLE` blocked in preview
- **Dependency Validation**: Recursive CTE walks the full dependency tree before any change
- **Auto-Backup**: Current definition always backed up before rollback
- **Version Pruning**: Max 3 versions stored per object (configurable)
- **Syntax Check**: `SET PARSEONLY ON` validates new definitions before touching the database

---

## 🗃️ Database Setup

The `ObjectVersions` table is created **automatically on startup**. To create it manually:

```sql
CREATE TABLE dbo.ObjectVersions (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    ObjectName    NVARCHAR(256)  NOT NULL,
    ObjectType    NVARCHAR(64)   NOT NULL,
    VersionNumber INT            NOT NULL,
    ScriptContent NVARCHAR(MAX)  NOT NULL,
    CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ObjectVersions UNIQUE (ObjectName, VersionNumber)
);
CREATE INDEX IX_OV_ObjectName ON dbo.ObjectVersions (ObjectName, VersionNumber DESC);
```

---

## 📦 GitHub Setup

```bash
git init
git add .
git commit -m "feat: Initial KITSUNE setup – validation, backup, preview"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/kitsune.git
git push -u origin main
```

---

## 🗺️ Roadmap (Phase 2+)

- [ ] MongoDB schema extraction + NL → aggregation pipeline
- [ ] AI Change Summary (diff between versions)
- [ ] Full audit log with user attribution
- [ ] Multi-database connection manager
- [ ] Query optimizer suggestions
- [ ] Scheduled backup jobs
- [ ] Role-based access control

---

## License

MIT © KITSUNE Project
