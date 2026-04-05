// ============================================================
// KITSUNE – Complete API Service Layer v2
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

export const validateObject    = (b) => post(BACKEND, '/api/validate', b);
export const getDependencies   = (n) => get(BACKEND, `/api/validate/dependencies/${encodeURIComponent(n)}`);
export const getParameters     = (n) => get(BACKEND, `/api/validate/parameters/${encodeURIComponent(n)}`);
export const checkExists       = (n) => get(BACKEND, `/api/validate/exists/${encodeURIComponent(n)}`);
export const backupObject      = (b) => post(BACKEND, '/api/backup', b);
export const getVersions       = (n) => get(BACKEND, `/api/versions/${encodeURIComponent(n)}`);
export const getDefinition     = (n) => get(BACKEND, `/api/versions/${encodeURIComponent(n)}/definition`);
export const rollbackObject    = (b) => post(BACKEND, '/api/rollback', b);
export const previewQuery      = (b) => post(BACKEND, '/api/preview', b);
export const applyChange       = (b) => post(BACKEND, '/api/apply', b);
export const getSqlSchema      = (db) => get(BACKEND, `/api/schema/sqlserver${db ? `?db=${encodeURIComponent(db)}` : ''}`);
export const getMongoSchema    = (db) => get(BACKEND, `/api/schema/mongodb/${encodeURIComponent(db)}`);
export const getDdl            = (db) => get(BACKEND, `/api/schema/ddl${db ? `?db=${encodeURIComponent(db)}` : ''}`);
export const compareScripts    = (b) => post(BACKEND, '/api/changesummary/compare', b);
export const analyzeQuery      = (b) => post(BACKEND, '/api/optimizer/analyze', b);
export const getMissingIndexes = ()  => get(BACKEND, '/api/optimizer/missing-indexes');
export const listConnections   = ()  => get(BACKEND, '/api/connections');
export const saveConnection    = (b) => post(BACKEND, '/api/connections', b);
export const testConnection    = (id) => post(BACKEND, `/api/connections/${id}/test`, {});
export const testConnString    = (b) => post(BACKEND, '/api/connections/test-string', b);
export const deleteConnection  = (id) => del(BACKEND, `/api/connections/${id}`);
export const getAuditLogs      = (n, top = 100) => get(BACKEND, `/api/audit?${n ? `objectName=${encodeURIComponent(n)}&` : ''}top=${top}`);
export const generateQuery     = (b) => post(AI_SVC, '/generate', b);
export const listModels        = ()  => get(AI_SVC, '/models');
export const explainQuery      = (b) => post(AI_SVC, '/explain', b);
export const analyzeRisk       = (b) => post(AI_SVC, '/risk', b);
export const summarizeChange   = (b) => post(AI_SVC, '/summarize-change', b);
export const schemaContext     = (b) => post(AI_SVC, '/schema-context', b);
export const aiHealth          = ()  => get(AI_SVC, '/health');
