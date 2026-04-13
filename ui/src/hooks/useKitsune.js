// ============================================================
// KITSUNE – useKitsune hook v5
// Dynamic model loading from backend /api/models (Ollama live)
// All existing state + handlers preserved
// ============================================================
import { useState, useEffect, useCallback } from 'react';
import * as api from '../services/api';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';
const fmtMs   = ms => (ms < 1000 ? `${ms.toFixed(0)}ms` : `${(ms / 1000).toFixed(2)}s`);

export function useKitsune() {
  // ── Core inputs ───────────────────────────────────────────
  const [nlQuery,          setNlQuery]          = useState('Show all customers who placed orders in the last 30 days with their total spend');
  const [sqlQuery,         setSqlQuery]         = useState('');
  const [objectName,       setObjectName]       = useState('');
  const [objectType,       setObjectType]       = useState('PROCEDURE');
  const [dbType,           setDbType]           = useState('SqlServer');
  const [model,            setModel]            = useState('auto');
  const [activeTab,        setActiveTab]        = useState('results');
  // ── Single source of truth for database context ───────────
  const [selectedDatabase, setSelectedDatabase] = useState('');

  // ── Data ─────────────────────────────────────────────────
  const [models,          setModels]          = useState([]);
  const [modelsLoading,   setModelsLoading]   = useState(false);
  const [validation,      setValidation]      = useState(null);
  const [preview,         setPreview]         = useState(null);
  const [applyResult,     setApplyResult]     = useState(null);
  const [versions,        setVersions]        = useState([]);
  const [backupResult,    setBackupResult]    = useState(null);
  const [rollbackResult,  setRollbackResult]  = useState(null);
  const [riskResult,      setRiskResult]      = useState(null);
  const [explanation,     setExplanation]     = useState('');
  const [genMeta,         setGenMeta]         = useState('');
  const [diffResult,      setDiffResult]      = useState(null);
  const [diffVA,          setDiffVA]          = useState(0);
  const [diffVB,          setDiffVB]          = useState(0);
  const [schema,          setSchema]          = useState(null);
  const [connections,     setConnections]     = useState([]);
  const [connTestResult,  setConnTestResult]  = useState(null);
  const [auditLogs,       setAuditLogs]       = useState([]);
  const [schedules,       setSchedules]       = useState([]);
  const [preferences,     setPreferences]     = useState(null);
  const [missingIndexes,  setMissingIndexes]  = useState([]);
  const [optimizerResult, setOptimizerResult] = useState(null);
  const [mongoResult,     setMongoResult]     = useState(null);
  const [mongoDb,         setMongoDb]         = useState('');
  const [mongoCollection, setMongoCollection] = useState('');
  const [mongoQuery,      setMongoQuery]      = useState('{}');
  const [mongoQueryType,  setMongoQueryType]  = useState('find');

  // ── Connection form ───────────────────────────────────────
  const [connForm, setConnForm] = useState({
    name: '', databaseType: 'SqlServer', host: 'localhost',
    port: 1433, databaseName: '', username: 'sa', password: '', trustCert: true,
    connectionStringOverride: '',
  });

  // ── Notifications ─────────────────────────────────────────
  const [notifications, setNotifications] = useState([]);

  const notify = useCallback((msg, type = 'info') => {
    const id = Date.now();
    setNotifications(p => [...p.slice(-4), { id, msg, type }]);
    setTimeout(() => setNotifications(p => p.filter(n => n.id !== id)), 4000);
  }, []);

  // ── Loading map ───────────────────────────────────────────
  const [loading, setLoading] = useState({});
  const setLoad   = useCallback((k, v) => setLoading(p => ({ ...p, [k]: v })), []);

  // ── Load models DYNAMICALLY from backend ──────────────────
  // Backend fetches from Ollama /api/tags – never hardcoded
  const loadModels = useCallback(async () => {
    setModelsLoading(true);
    try {
      // Try backend /api/models first (fetches from Ollama dynamically)
      const res = await fetch(`${BACKEND}/api/models`);
      if (res.ok) {
        const data = await res.json();
        if (Array.isArray(data) && data.length > 0) {
          // Ensure Auto-Route is always first
          const hasAuto = data.some(m => m.id === 'auto');
          const models  = hasAuto ? data : [
            { id: 'auto', name: 'auto', displayName: 'Auto-Route', type: 'system', available: true },
            ...data,
          ];
          setModels(models.map(m => ({
            ...m,
            display_name: m.displayName || m.display_name || m.name,
          })));
          return;
        }
      }
    } catch { /* fall through to AI service */ }

    // Fallback: try AI service /models endpoint
    try {
      const data = await api.listModels();
      setModels([
        { id: 'auto', display_name: '⚡ Auto-Route', type: 'system', available: true },
        ...data.map(m => ({ ...m, display_name: m.display_name || m.name })),
      ]);
    } catch {
      // Final fallback: static list
      setModels([
        { id: 'auto',        display_name: '⚡ Auto-Route',         type: 'system', available: true },
        { id: 'sqlcoder',    display_name: 'SQLCoder (Local)',      type: 'local',  available: false },
        { id: 'qwen3-coder', display_name: 'Qwen3 480B (Cloud)',   type: 'cloud',  available: false },
      ]);
    } finally {
      setModelsLoading(false);
    }
  }, []);

  useEffect(() => { loadModels(); }, [loadModels]);

  // ── Load preferences ──────────────────────────────────────
  useEffect(() => {
    fetch(`${BACKEND}/api/preferences`)
      .then(r => r.ok ? r.json() : null)
      .then(p => { if (p) { setPreferences(p); if (p.defaultModel) setModel(p.defaultModel); } })
      .catch(() => {});
  }, []);

  // ── Generic handler factory ───────────────────────────────
  const handle = useCallback((key, fn) => async (...args) => {
    setLoad(key, true);
    try { await fn(...args); }
    catch (e) { notify(`${key}: ${e.message}`, 'error'); console.error(key, e); }
    finally { setLoad(key, false); }
  }, [setLoad, notify]);

  // ── All action handlers ───────────────────────────────────
  const handleGenerate = handle('gen', async () => {
    setGenMeta('');
    const res = await api.generateQuery({
      natural_language: nlQuery,
      database_type:    dbType,
      model,
      database_name:    selectedDatabase,
    });

    // Use .query or .generated_query (both are set by v5 backend)
    const sqlOut = res.query || res.generated_query || '';
    setSqlQuery(sqlOut);

    // Populate state for all UI tabs
    setSchema(prev => ({
      ...prev,
      aiQuery:     sqlOut,
      explanation: res.explanation  || '',
      schemaUsed:  res.schema       || res.schema_used || '',
      deepmap:     res.deepmap      || '',
      tablesUsed:  res.tables_used  || [],
    }));

    if (res.explanation) setExplanation(res.explanation);

    // Build genMeta confirmation line (shown below the editor)
    const confLabel  = res.confidence ? ` · ${res.confidence.toUpperCase()}` : '';
    const tablesInfo = res.tables_used?.length
      ? ` · Tables: ${res.tables_used.slice(0,4).join(', ')}${res.tables_used.length > 4 ? '…' : ''}`
      : '';
    const fbNote = res.fallback_used ? ' · (fallback)' : '';
    setGenMeta(
      `${res.display_name || 'AI'}${confLabel} · ` +
      `${fmtMs(res.execution_ms || 0)} · ${res.tokens_used || 0} tokens` +
      `${fbNote}${tablesInfo}`
    );

    // Show generation_log entries as the main notification (confirmation logging)
    const logSummary = res.generation_log?.length
      ? res.generation_log[res.generation_log.length - 1]   // last log entry
      : null;
    const notifyMsg = res.tables_used?.length
      ? `Schema sent to LLM: ${res.tables_used.slice(0,3).join(', ')}${res.tables_used.length > 3 ? '…' : ''}`
      : 'Query generated';

    notify(logSummary || notifyMsg, res.confidence_score > 0 ? 'success' : 'error');

    // Log all confirmation entries to console for debugging
    if (res.generation_log?.length) {
      console.group('[KITSUNE] Generation Log');
      res.generation_log.forEach(l => console.log(l));
      console.groupEnd();
    }
  });

  const handleValidate = handle('validate', async () => {
    const res = await api.validateObject({ objectName, objectType, newDefinition: sqlQuery });
    setValidation(res);
    setActiveTab('validation');
    notify(`Validation: ${res.status}`, res.status === 'FAIL' ? 'error' : 'success');
  });

  const handlePreview = handle('preview', async () => {
    console.log('[KITSUNE] Preview request:', { sqlQuery, databaseName: selectedDatabase });

    const res = await api.previewQuery({
      sqlQuery,
      isStoredProc:   false,
      timeoutSeconds: 30,
      databaseName:   selectedDatabase,   // ← single source of truth
    });

    // Debug: log raw response
    console.log('[KITSUNE] Preview response:', {
      success:    res.success,
      rowCount:   res.rowCount,
      columns:    res.columns,
      resultRows: res.resultSet?.length,
      errors:     res.errors,
      messages:   res.messages,
      executionMs:res.executionMs,
    });

    // Normalise: handle both camelCase and PascalCase from server
    const normalised = {
      ...res,
      success:   res.success   ?? false,
      rowCount:  res.rowCount  ?? res.RowCount  ?? 0,
      columns:   res.columns   ?? res.Columns   ?? [],
      resultSet: res.resultSet ?? res.ResultSet ?? [],
      errors:    res.errors    ?? res.Errors    ?? [],
      messages:  res.messages  ?? res.Messages  ?? [],
    };

    setPreview(normalised);
    setActiveTab('results');
    notify(
      `Preview: ${normalised.rowCount} rows in ${fmtMs(res.executionMs)}`,
      normalised.success ? 'success' : 'error'
    );
  });

  const handleBackup = handle('backup', async () => {
    const res = await api.backupObject({ objectName, objectType });
    setBackupResult(res);
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
    setActiveTab('history');
    notify(res.success ? `Backed up as v${res.versionNumber}` : 'Backup failed', res.success ? 'success' : 'error');
  });

  const handleVersions = handle('versions', async () => {
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
    setActiveTab('history');
  });

  const handleRollback = handle('rollback', async (vNum) => {
    const res = await api.rollbackObject({ objectName, versionNumber: vNum });
    setRollbackResult(res);
    if (res.restoredScript) setSqlQuery(res.restoredScript);
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
    notify(res.success ? `Rolled back to v${vNum}` : 'Rollback failed', res.success ? 'success' : 'error');
  });

  const handleApply = handle('apply', async () => {
    const res = await api.applyChange({ objectName, objectType, sqlScript: sqlQuery, skipValidation: false, skipBackup: false });
    setApplyResult(res);
    setActiveTab('results');
    notify(res.success ? 'Change applied' : `Apply failed: ${res.status}`, res.success ? 'success' : 'error');
  });

  const handleRisk = handle('risk', async () => {
    const res = await api.analyzeRisk({ query: sqlQuery, object_type: objectType });
    setRiskResult(res);
    setActiveTab('risk');
    notify(`Risk: ${res.riskLevel || 'assessed'}`, 'info');
  });

  const handleExplain = handle('explain', async () => {
    const res = await api.explainQuery({ query: sqlQuery, model });
    setExplanation(res.explanation || '');
    setActiveTab('explain');
  });

  const handleSchema = handle('schema', async () => {
    const res = await api.getSqlSchema();
    setSchema(res);
    setActiveTab('schema');
    notify(`Schema loaded: ${res.tables?.length} tables`, 'success');
  });

  const handleDiff = handle('diff', async () => {
    if (versions.length < 2) { notify('Need at least 2 versions', 'error'); return; }
    const va = versions.find(v => v.versionNumber === diffVA);
    const vb = versions.find(v => v.versionNumber === diffVB);
    if (!va || !vb) { notify('Select valid versions', 'error'); return; }
    const res = await api.compareScripts({ objectName, oldScript: va.scriptContent, newScript: vb.scriptContent, oldVersion: diffVA, newVersion: diffVB, model });
    setDiffResult(res);
    setActiveTab('diff');
  });

  const handleLoadConnections = handle('conns', async () => {
    const res = await api.listConnections();
    setConnections(res || []);
    setActiveTab('connections');
  });

  const handleSaveConn = handle('saveConn', async () => {
    await api.saveConnection({ ...connForm });
    const res = await api.listConnections();
    setConnections(res || []);
    notify('Connection saved', 'success');
  });

  const handleTestConn = handle('testConn', async (id) => {
    const res = await api.testConnection(id);
    setConnTestResult(res);
    notify(res.success ? `Connected in ${fmtMs(res.latencyMs)}` : 'Connection failed', res.success ? 'success' : 'error');
  });

  const handleLoadAudit = handle('audit', async () => {
    const res = await api.getAuditLogs(objectName || null, 100);
    setAuditLogs(res.logs || []);
    setActiveTab('audit');
  });

  const handleLoadSchedules = handle('schedules', async () => {
    const res = await fetch(`${BACKEND}/api/schedules`).then(r => r.json());
    setSchedules(Array.isArray(res) ? res : []);
    setActiveTab('schedules');
  });

  const handleAddSchedule = handle('addSched', async (intervalMinutes) => {
    if (!objectName) { notify('Set an object name first', 'error'); return; }
    await fetch(`${BACKEND}/api/schedules`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ objectName, objectType, intervalMinutes }),
    });
    notify(`Schedule added for ${objectName}`, 'success');
    await handleLoadSchedules();
  });

  const handleOptimize = handle('optimize', async () => {
    const res = await api.analyzeQuery({ sqlQuery, getPlan: true, getIndexes: true });
    setOptimizerResult(res);
    setMissingIndexes(res.missingIndexes || []);
    setActiveTab('optimizer');
  });

  const handleMongoQuery = handle('mongo', async () => {
    const res = await fetch(`${BACKEND}/api/mongo/query`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ databaseName: mongoDb, collectionName: mongoCollection, queryJson: mongoQuery, queryType: mongoQueryType, limit: 200, safeMode: true }),
    }).then(r => r.json());
    setMongoResult(res);
    setActiveTab('mongo');
    notify(`MongoDB: ${res.rowCount} docs`, res.success ? 'success' : 'error');
  });

  const handleSavePreferences = handle('savePrefs', async (prefs) => {
    await fetch(`${BACKEND}/api/preferences`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(prefs),
    });
    setPreferences(prefs);
    notify('Preferences saved', 'success');
  });

  return {
    // Inputs
    nlQuery, setNlQuery, sqlQuery, setSqlQuery,
    objectName, setObjectName, objectType, setObjectType,
    dbType, setDbType, model, setModel,
    activeTab, setActiveTab,
    // DB context (single source of truth)
    selectedDatabase, setSelectedDatabase,
    // Models (dynamic)
    models, modelsLoading, loadModels,
    // Data
    validation, preview, applyResult,
    versions, backupResult, rollbackResult,
    riskResult, explanation, genMeta,
    diffResult, diffVA, setDiffVA, diffVB, setDiffVB,
    schema, connections, connTestResult, connForm, setConnForm,
    auditLogs, schedules, preferences,
    missingIndexes, optimizerResult, mongoResult,
    mongoDb, setMongoDb, mongoCollection, setMongoCollection,
    mongoQuery, setMongoQuery, mongoQueryType, setMongoQueryType,
    notifications, notify,
    // Loading
    loading,
    // Handlers
    handleGenerate, handleValidate, handlePreview,
    handleBackup, handleVersions, handleRollback, handleApply,
    handleRisk, handleExplain, handleSchema, handleDiff,
    handleLoadConnections, handleSaveConn, handleTestConn,
    handleLoadAudit, handleLoadSchedules, handleAddSchedule,
    handleOptimize, handleMongoQuery, handleSavePreferences,
  };
}
