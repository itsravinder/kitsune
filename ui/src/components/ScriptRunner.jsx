// ============================================================
// KITSUNE – ScriptRunner Component
// Runs multi-statement SQL scripts with GO batch splitting
// ============================================================
import React, { useState } from 'react';
import { T, Btn, Spinner, AlertBox, EmptyState, DataTable } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const post = (path, body) =>
  fetch(`${BACKEND}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }).then(r => r.json());

export function ScriptRunnerTab({ sqlQuery }) {
  const [script,      setScript]      = useState(sqlQuery || '');
  const [mode,        setMode]        = useState('live');     // live|transaction|dryrun
  const [timeout,     setTimeout_]    = useState(120);
  const [result,      setResult]      = useState(null);
  const [loading,     setLoading]     = useState(false);
  const [batches,     setBatches]     = useState([]);
  const [showBatches, setShowBatches] = useState(false);

  // Sync from prop
  React.useEffect(() => { if (sqlQuery) setScript(sqlQuery); }, [sqlQuery]);

  const handleRun = async () => {
    setLoading(true); setResult(null);
    try {
      const res = await post('/api/script/run', {
        sqlScript: script,
        dryRun: mode === 'dryrun',
        useTransaction: mode === 'transaction',
        timeoutSeconds: timeout,
      });
      setResult(res);
    } catch (e) {
      setResult({ success: false, messages: [e.message] });
    } finally {
      setLoading(false);
    }
  };

  const handlePreviewBatches = async () => {
    const res = await post('/api/script/split', { sqlScript: script });
    setBatches(res.batches || []);
    setShowBatches(true);
  };

  const handleValidate = async () => {
    setLoading(true); setResult(null);
    try {
      const res = await post('/api/script/validate', { sqlScript: script });
      setResult(res);
    } finally {
      setLoading(false);
    }
  };

  const inputStyle = {
    background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt,
    padding: '5px 9px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none',
  };

  return (
    <div>
      {/* Controls */}
      <div style={{ padding: '10px 12px', background: T.bg1, borderBottom: `1px solid ${T.border}` }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 8 }}>
          <span style={{ color: T.txt3, fontSize: 10, fontWeight: 700 }}>EXECUTION MODE</span>
          {[
            { id: 'live',        label: '⚡ Live',        color: T.red },
            { id: 'transaction', label: '🔒 Transaction', color: T.gold },
            { id: 'dryrun',      label: '✓ Dry Run',      color: T.green },
          ].map(m => (
            <button key={m.id} onClick={() => setMode(m.id)} style={{
              padding: '4px 12px', borderRadius: 5, fontSize: 11, fontWeight: 700,
              cursor: 'pointer', fontFamily: 'inherit',
              background: mode === m.id ? `${m.color}22` : T.bg2,
              color: mode === m.id ? m.color : T.txt3,
              border: `1px solid ${mode === m.id ? m.color + '88' : T.border}`,
            }}>
              {m.label}
            </button>
          ))}
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: T.txt3, marginLeft: 'auto' }}>
            Timeout (s):
            <input type="number" min={5} max={600} style={{ ...inputStyle, width: 70 }}
              value={timeout} onChange={e => setTimeout_(Number(e.target.value))} />
          </label>
        </div>

        <div style={{ display: 'flex', gap: 6 }}>
          <Btn color={mode === 'dryrun' ? T.green : T.red} bg={mode === 'dryrun' ? '#0a1e10' : '#180808'}
            onClick={handleRun} disabled={loading || !script.trim()}>
            {loading ? <Spinner /> : '▶'} Run Script
          </Btn>
          <Btn color={T.green} bg="#0a1e10" onClick={handleValidate} disabled={loading || !script.trim()}>
            ✓ Validate (Parse Only)
          </Btn>
          <Btn color={T.txt2} bg={T.bg3} onClick={handlePreviewBatches} disabled={!script.trim()}>
            ⊞ Preview Batches
          </Btn>
        </div>

        {mode === 'live' && (
          <div style={{ marginTop: 8, fontSize: 10, color: T.red }}>
            ⚠ LIVE mode executes directly against the database. Changes are permanent.
          </div>
        )}
        {mode === 'transaction' && (
          <div style={{ marginTop: 8, fontSize: 10, color: T.gold }}>
            🔒 All batches run in one transaction. Rolls back if any batch fails.
          </div>
        )}
        {mode === 'dryrun' && (
          <div style={{ marginTop: 8, fontSize: 10, color: T.green }}>
            ✓ DRY RUN uses SET PARSEONLY – syntax check only, nothing executes.
          </div>
        )}
      </div>

      {/* Script editor */}
      <div style={{ padding: '10px 12px', borderBottom: `1px solid ${T.border}` }}>
        <textarea
          style={{
            width: '100%', height: 140, background: '#050e1c', border: `1px solid ${T.border}`,
            color: T.txt, padding: 10, fontFamily: "'JetBrains Mono',monospace",
            fontSize: 11, resize: 'vertical', outline: 'none', lineHeight: 1.7, borderRadius: 6,
          }}
          value={script}
          onChange={e => setScript(e.target.value)}
          spellCheck={false}
          placeholder={"-- Multi-statement script with GO batch separators\nCREATE TABLE #Temp (Id INT);\nGO\nINSERT INTO #Temp VALUES (1);\nGO\nSELECT * FROM #Temp;\nGO"}
        />
      </div>

      {/* Batch preview */}
      {showBatches && batches.length > 0 && (
        <div style={{ margin: 10 }}>
          <div style={{ color: T.txt3, fontSize: 10, fontWeight: 700, marginBottom: 8 }}>
            DETECTED BATCHES ({batches.length})
          </div>
          {batches.map((b, i) => (
            <div key={i} style={{
              background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 6,
              marginBottom: 6, overflow: 'hidden',
            }}>
              <div style={{ padding: '4px 10px', background: T.bg3, fontSize: 10, color: T.txt3 }}>
                Batch {i + 1}
              </div>
              <pre style={{ margin: 0, padding: '6px 10px', fontSize: 10, color: T.txt2, whiteSpace: 'pre-wrap' }}>
                {b.slice(0, 300)}{b.length > 300 ? '…' : ''}
              </pre>
            </div>
          ))}
        </div>
      )}

      {/* Results */}
      {result && (
        <>
          <div style={{
            display: 'flex', gap: 14, padding: '8px 14px',
            background: result.success ? '#0a1e1099' : '#180808',
            borderBottom: `1px solid ${T.border}`,
          }}>
            <span style={{ color: result.success ? T.green : T.red, fontWeight: 700 }}>
              {result.success ? '✓ SUCCESS' : '✗ FAILED'}
            </span>
            <span style={{ color: T.txt2 }}>Batches: <b style={{ color: T.txt }}>{result.totalBatches}</b></span>
            <span style={{ color: T.green }}>✓ {result.successCount}</span>
            <span style={{ color: T.red }}>✗ {result.failureCount}</span>
            <span style={{ color: T.txt2 }}>Total: <b style={{ color: T.gold }}>{result.totalMs?.toFixed(0)}ms</b></span>
            <span style={{ color: T.txt2 }}>Mode: <b style={{ color: T.cyan }}>{result.mode}</b></span>
          </div>

          {result.messages?.length > 0 && (
            <div style={{ margin: 10, padding: '8px 12px', background: T.bg2, borderRadius: 6, fontSize: 11, color: T.txt3 }}>
              {result.messages.map((m, i) => <div key={i}>{m}</div>)}
            </div>
          )}

          {result.batches?.length > 0 && (
            <DataTable
              columns={['#', 'Status', 'Rows Affected', 'Time', 'Preview', 'Error']}
              rows={result.batches.map(b => ({
                '#': b.batchNumber,
                Status: b.success
                  ? <span style={{ color: T.green }}>✓ OK</span>
                  : <span style={{ color: T.red }}>✗ FAIL</span>,
                'Rows Affected': b.rowsAffected ?? '—',
                Time: `${b.executionMs?.toFixed(0)}ms`,
                Preview: b.batchSql?.slice(0, 40) + (b.batchSql?.length > 40 ? '…' : ''),
                Error: b.error || '—',
              }))}
            />
          )}
        </>
      )}

      {!result && !showBatches && (
        <EmptyState icon="⊞" message="No script run yet"
          sub="Paste a multi-statement SQL script above and click Run" />
      )}
    </div>
  );
}
