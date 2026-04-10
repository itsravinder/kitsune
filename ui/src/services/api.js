// ============================================================
// KITSUNE – API Service Layer v5
// All endpoints including v5 enhancements
// ============================================================

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';
const AI_SVC  = process.env.REACT_APP_AI_URL      || 'http://localhost:8000';

const post = async (base, path, body) => {
  const res  = await fetch(`${base}${path}`, {
    method : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body   : JSON.stringify(body),
  });
  const data = await res.json();
  if (!res.ok) throw new Error(data.error || data.message || `HTTP ${res.status}`);
  return data;
};

const get = async (base, path) => {
  const res  = await fetch(`${base}${path}`);
  const data = await res.json();
  if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);
  return data;
};

const del = async (base, path) => {
  const res = await fetch(`${base}${path}`, { method: 'DELETE' });
  return res.json();
};

// ── Dependency Validation ─────────────────────────────────────
export const validateObject    = b  => post(BACKEND, '/api/validate',                   b);
export const getDependencies   = n  => get(BACKEND,  `/api/validate/dependencies/${encodeURIComponent(n)}`);
export const getParameters     = n  => get(BACKEND,  `/api/validate/parameters/${encodeURIComponent(n)}`);
export const checkExists       = n  => get(BACKEND,  `/api/validate/exists/${encodeURIComponent(n)}`);

// ── Backup / Versioning ───────────────────────────────────────
export const backupObject      = b  => post(BACKEND, '/api/backup',                     b);
export const getVersions       = n  => get(BACKEND,  `/api/versions/${encodeURIComponent(n)}`);
export const getDefinition     = n  => get(BACKEND,  `/api/versions/${encodeURIComponent(n)}/definition`);
export const rollbackObject    = b  => post(BACKEND, '/api/rollback',                   b);

// ── Preview & Apply ───────────────────────────────────────────
export const previewQuery      = b  => post(BACKEND, '/api/preview',                    b);
export const applyChange       = b  => post(BACKEND, '/api/apply',                      b);

// ── Schema ────────────────────────────────────────────────────
export const getSqlSchema      = db => get(BACKEND,  `/api/schema/sqlserver${db ? `?db=${encodeURIComponent(db)}` : ''}`);
export const getMongoSchema    = db => get(BACKEND,  `/api/schema/mongodb/${encodeURIComponent(db)}`);
export const getDdl            = db => get(BACKEND,  `/api/schema/ddl${db ? `?db=${encodeURIComponent(db)}` : ''}`);

// ── Schema Tree (NEW v5) ──────────────────────────────────────
export const getSchemaTree     = id => get(BACKEND,  `/api/connections/${id}/tree`);
export const getObjectDefn     = (id, name, type) =>
  get(BACKEND, `/api/connections/${id}/definition?name=${encodeURIComponent(name)}&type=${encodeURIComponent(type)}`);

// ── Object Lookup (NEW v5) ────────────────────────────────────
export const listObjects       = type => get(BACKEND, `/api/objects/list?type=${encodeURIComponent(type)}`);
export const getObjectDef      = name => get(BACKEND, `/api/objects/definition?name=${encodeURIComponent(name)}`);
export const objectExists      = name => get(BACKEND, `/api/objects/exists?name=${encodeURIComponent(name)}`);

// ── Change Summary / Diff ─────────────────────────────────────
export const compareScripts    = b  => post(BACKEND, '/api/changesummary/compare',      b);

// ── Optimizer ─────────────────────────────────────────────────
export const analyzeQuery      = b  => post(BACKEND, '/api/optimizer/analyze',          b);
export const getMissingIndexes = () => get(BACKEND,  '/api/optimizer/missing-indexes');

// ── Models (NEW v5 – fetched dynamically from Ollama) ─────────
export const getBackendModels  = ()  => get(BACKEND, '/api/models');

// ── Connections ───────────────────────────────────────────────
export const listConnections   = ()  => get(BACKEND, '/api/connections');
export const saveConnection    = b   => post(BACKEND, '/api/connections',               b);
export const testConnection    = id  => post(BACKEND, `/api/connections/${id}/test`,   {});
export const testRawConn       = b   => post(BACKEND, '/api/connections/test-raw',     b); // NEW v5
export const testConnString    = b   => post(BACKEND, '/api/connections/test-string',  b);
export const deleteConnection  = id  => del(BACKEND,  `/api/connections/${id}`);

// ── Audit Log ─────────────────────────────────────────────────
export const getAuditLogs      = (n, top = 100) =>
  get(BACKEND, `/api/audit?${n ? `objectName=${encodeURIComponent(n)}&` : ''}top=${top}`);

// ── Script Runner ─────────────────────────────────────────────
export const runScript         = b  => post(BACKEND, '/api/script/run',                b);
export const validateScript    = b  => post(BACKEND, '/api/script/validate',           b);
export const splitScript       = b  => post(BACKEND, '/api/script/split',              b);

// ── Export ────────────────────────────────────────────────────
export const exportData        = b  => post(BACKEND, '/api/export',                    b);

// ── Preferences ───────────────────────────────────────────────
export const getPreferences    = ()  => get(BACKEND, '/api/preferences');
export const savePreferences   = b   => post(BACKEND, '/api/preferences',              b);

// ── MongoDB ───────────────────────────────────────────────────
export const mongoQuery        = b  => post(BACKEND, '/api/mongo/query',               b);
export const mongoDatabases    = ()  => get(BACKEND, '/api/mongo/databases');
export const mongoCollections  = db  => get(BACKEND, `/api/mongo/databases/${encodeURIComponent(db)}/collections`);

// ── DB Info ──────────────────────────────────────────────
export const getDatabases = () => get(BACKEND, '/api/databases');

export const getDbInfo = () => get(BACKEND, '/api/db-info');

// ── AI Service ────────────────────────────────────────────
export const invalidateSchemaCache = (db) => post(AI_SVC, '/cache/invalidate', { database_name: db });
export const detectTables          = (b)  => post(AI_SVC, '/detect-tables',   b);
export const cacheStatus           = ()   => get(AI_SVC,  '/cache/status');
export const generateQuery     = b  => post(AI_SVC,  '/generate',                     b);
export const listModels        = ()  => get(AI_SVC,  '/models');          // AI service models
export const explainQuery      = b  => post(AI_SVC,  '/explain',                      b);
export const analyzeRisk       = b  => post(AI_SVC,  '/risk',                         b);
export const summarizeChange   = b  => post(AI_SVC,  '/summarize-change',             b);
export const schemaContext     = b  => post(AI_SVC,  '/schema-context',               b);
export const aiHealth          = ()  => get(AI_SVC,  '/health');
