// ============================================================
// KITSUNE – ModeManager
// Detects query mode (Read/Write), drives tab visibility,
// confidence score, and safety warnings.
// ============================================================
import { useState, useCallback, useRef } from 'react';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

// ── Write statement patterns (client-side fast check) ────────
const WRITE_RE = /\b(INSERT\s+INTO|UPDATE\s+\w|DELETE\s+FROM|DELETE\b|CREATE\s+(TABLE|VIEW|PROCEDURE|FUNCTION|INDEX|TRIGGER)|ALTER\s+(TABLE|VIEW|PROCEDURE|FUNCTION)|DROP\s+(TABLE|VIEW|PROCEDURE|FUNCTION|INDEX|DATABASE)|TRUNCATE\b|MERGE\b|EXEC(UTE)?\s+\w|SP_\w|USP_\w)/i;
const SELECT_RE = /^\s*(;?\s*)?(WITH\s+\w+\s+AS\s*\()?SELECT\b/i;
const DANGER_RE  = /\b(DROP\s+(TABLE|DATABASE)|TRUNCATE|DELETE\s+FROM\s+\w+\s*;?\s*$)/i;
const NO_WHERE_UPDATE = /\bUPDATE\b(?!.*WHERE)/si;
const NO_WHERE_DELETE = /\bDELETE\b(?!.*WHERE)/si;

// ── Tab definitions ───────────────────────────────────────────
export const READ_TABS = [
  { id: 'results',     label: 'Results',    icon: '▣', color: '#3dba6e' },
  { id: 'explain',     label: 'Explain',    icon: '💡', color: '#38bdf8' },
  { id: 'schema',      label: 'Schema',     icon: '🗃', color: '#f59e0b' },
  { id: 'depmap',      label: 'Dep Map',    icon: '⑃', color: '#a37eff' },
  { id: 'export',      label: 'Export',     icon: '📥', color: '#6b90b0' },
  { id: 'preferences', label: 'Prefs',      icon: '⚙', color: '#6b90b0' },
];

export const WRITE_TABS = [
  { id: 'validation',  label: 'Validation', icon: '🛡', color: '#3dba6e' },
  { id: 'diff',        label: 'Diff',       icon: '⟷', color: '#a37eff' },
  { id: 'depmap',      label: 'Dep Map',    icon: '⑃', color: '#a37eff' },
  { id: 'risk',        label: 'Risk',       icon: '⚠', color: '#e05252' },
  { id: 'history',     label: 'Versions',   icon: '🕐', color: '#e2a500' },
  { id: 'script',      label: 'Script Run', icon: '⊞', color: '#6b90b0' },
  { id: 'audit',       label: 'Audit Log',  icon: '📋', color: '#6b90b0' },
  { id: 'results',     label: 'Results',    icon: '▣', color: '#6b90b0' },
  { id: 'schema',      label: 'Schema',     icon: '🗃', color: '#6b90b0' },
  { id: 'explain',     label: 'Explain',    icon: '💡', color: '#6b90b0' },
  { id: 'optimizer',   label: 'Optimizer',  icon: '⚡', color: '#6b90b0' },
  { id: 'schedules',   label: 'Schedules',  icon: '🕐', color: '#6b90b0' },
];

export const UNKNOWN_TABS = [
  { id: 'results',     label: 'Results',    icon: '▣', color: '#6b90b0' },
  { id: 'explain',     label: 'Explain',    icon: '💡', color: '#6b90b0' },
  { id: 'schema',      label: 'Schema',     icon: '🗃', color: '#6b90b0' },
  { id: 'export',      label: 'Export',     icon: '📥', color: '#6b90b0' },
];

// ── Hook ──────────────────────────────────────────────────────
export function useModeManager() {
  const [mode,            setMode]            = useState('unknown'); // read|write|unknown
  const [modeLabel,       setModeLabel]       = useState('');
  const [modeColor,       setModeColor]       = useState('gray');
  const [confidenceScore, setConfidenceScore] = useState(0);
  const [riskLevel,       setRiskLevel]       = useState('LOW');
  const [warnings,        setWarnings]        = useState([]);
  const [syntaxErrors,    setSyntaxErrors]    = useState([]);
  const [activeTab,       setActiveTab]       = useState('results');
  const [tabs,            setTabs]            = useState(UNKNOWN_TABS);
  const [analyzing,       setAnalyzing]       = useState(false);
  const [primaryStmt,     setPrimaryStmt]     = useState('');
  const [safetyMsg,       setSafetyMsg]       = useState('');
  const [requiresValidation, setRequiresValidation] = useState(false);
  const debounceRef = useRef(null);

  // ── Fast heuristic (no network, instant) ──────────────────
  const detectLocal = useCallback((sql) => {
    if (!sql?.trim()) {
      setMode('unknown'); setModeLabel(''); setModeColor('gray');
      setTabs(UNKNOWN_TABS); setConfidenceScore(0); setWarnings([]);
      setSafetyMsg(''); setRequiresValidation(false);
      return;
    }

    const warns = [];
    const isWrite = WRITE_RE.test(sql);
    const isRead  = SELECT_RE.test(sql) && !isWrite;
    const isCrit  = DANGER_RE.test(sql) ||
                    NO_WHERE_UPDATE.test(sql) ||
                    NO_WHERE_DELETE.test(sql);

    if (NO_WHERE_UPDATE.test(sql)) warns.push('UPDATE without WHERE affects ALL rows!');
    if (NO_WHERE_DELETE.test(sql)) warns.push('DELETE without WHERE affects ALL rows!');

    if (isRead) {
      setMode('read');
      setModeLabel('Read Mode (Safe)');
      setModeColor('green');
      setTabs(READ_TABS);
      setActiveTab(prev => READ_TABS.find(t => t.id === prev) ? prev : 'results');
      setConfidenceScore(92);
      setRiskLevel('LOW');
      setPrimaryStmt('SELECT');
      setSafetyMsg('Read-only query. Safe to execute.');
      setRequiresValidation(false);
    } else if (isWrite) {
      const m = sql.toUpperCase().match(/\b(INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE|MERGE|EXEC)\b/);
      setMode('write');
      setModeLabel('Change Mode (Risky)');
      setModeColor(isCrit ? 'red' : 'amber');
      setTabs(WRITE_TABS);
      setActiveTab(prev => WRITE_TABS.find(t => t.id === prev) ? prev : 'validation');
      setConfidenceScore(isCrit ? 20 : 65);
      setRiskLevel(isCrit ? 'CRITICAL' : 'HIGH');
      setPrimaryStmt(m?.[1] || 'WRITE');
      setSafetyMsg(isCrit
        ? 'CRITICAL: This operation is destructive and may be irreversible.'
        : 'Write operation detected. Validate before applying.');
      setRequiresValidation(true);
    } else {
      setMode('unknown');
      setModeLabel('');
      setModeColor('gray');
      setTabs(UNKNOWN_TABS);
      setConfidenceScore(50);
      setSafetyMsg('');
      setRequiresValidation(false);
    }
    setWarnings(warns);
  }, []);

  // ── Full server-side analysis (debounced 600ms) ────────────
  const analyzeDeep = useCallback((sql, dbType = 'SqlServer') => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(async () => {
      if (!sql?.trim()) return;
      setAnalyzing(true);
      try {
        const res  = await fetch(`${BACKEND}/api/intent`, {
          method : 'POST',
          headers: { 'Content-Type': 'application/json' },
          body   : JSON.stringify({ sql, dbType, parseOnly: true }),
        });
        const data = await res.json();
        setConfidenceScore(data.confidenceScore ?? 50);
        setRiskLevel(data.riskLevel ?? 'LOW');
        setSyntaxErrors(data.syntaxErrors ?? []);
        setWarnings(prev => [...prev, ...(data.warnings ?? [])]);
        setSafetyMsg(data.safetyMessage || '');
      } catch { /* silent */ }
      finally { setAnalyzing(false); }
    }, 600);
  }, []);

  // ── Combined: local first, then deep ─────────────────────
  const analyze = useCallback((sql, dbType) => {
    detectLocal(sql);
    analyzeDeep(sql, dbType);
  }, [detectLocal, analyzeDeep]);

  return {
    mode, modeLabel, modeColor,
    confidenceScore, riskLevel,
    warnings, syntaxErrors,
    activeTab, setActiveTab,
    tabs, analyzing,
    primaryStmt, safetyMsg, requiresValidation,
    analyze, detectLocal,
  };
}
