// ============================================================
// KITSUNE – Result Panel Tab Components
// One component per tab: Results, Validation, History, Diff,
// Risk, Explain, Schema, Optimizer, Mongo, Connections,
// Audit, Schedules, Preferences
// ============================================================
import React, { useState } from 'react';
import { T, StatusBadge, Pill, DataTable, AlertBox, EmptyState, Btn, Spinner } from './SharedComponents';

const fmtMs = ms => (ms < 1000 ? `${ms.toFixed(0)}ms` : `${(ms / 1000).toFixed(2)}s`);

// ── Results Tab ───────────────────────────────────────────────
export function ResultsTab({ preview, applyResult }) {
  return (
    <div>
      {applyResult && (
        <AlertBox type={applyResult.success ? 'success' : 'error'}>
          <StatusBadge status={applyResult.status} />&nbsp;&nbsp;
          {applyResult.message}
          {applyResult.backupVersion && ` · Auto-backed up as v${applyResult.backupVersion}`}
          {applyResult.errors?.map((e, i) => <div key={i} style={{ marginTop: 4 }}>• {e}</div>)}
        </AlertBox>
      )}
      {!preview && !applyResult && (
        <EmptyState icon="👁" message="No preview yet" sub="Click Preview (safe) or Apply (live)" />
      )}
      {preview && (
        <>
          <div style={{
            display: 'flex', gap: 16, padding: '8px 14px',
            background: T.bg1, borderBottom: `1px solid ${T.border}`, flexWrap: 'wrap',
          }}>
            <span style={{ color: preview.success ? T.green : T.red }}>
              {preview.success ? '✓ SAFE_PREVIEW' : '✗ Error'}
            </span>
            <span style={{ color: T.txt2 }}>Rows: <b style={{ color: T.txt }}>{preview.rowCount}</b></span>
            <span style={{ color: T.txt2 }}>Time: <b style={{ color: T.txt }}>{fmtMs(preview.executionMs)}</b></span>
            <span style={{ color: T.txt2 }}>Mode: <b style={{ color: T.gold }}>BEGIN TRAN / ROLLBACK</b></span>
          </div>
          {preview.errors?.length > 0 && (
            <AlertBox type="error">{preview.errors.map((e, i) => <div key={i}>{e}</div>)}</AlertBox>
          )}
          {preview.messages?.length > 0 && (
            <AlertBox type="success">{preview.messages.map((m, i) => <div key={i}>{m}</div>)}</AlertBox>
          )}
          {preview.resultSet?.length > 0
            ? <DataTable columns={preview.columns} rows={preview.resultSet} />
            : preview.success && (
              <div style={{ padding: '16px 14px', color: T.txt3, fontSize: 11 }}>
                Query executed successfully. No rows returned.
              </div>
            )}
          {preview.resultSet?.length > 0 && (
            <div style={{ padding: '6px 14px', fontSize: 10, color: T.txt3 }}>
              ⚠ Safe mode: transaction was rolled back. No data was persisted.
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ── Validation Tab ────────────────────────────────────────────
export function ValidationTab({ validation }) {
  if (!validation) return <EmptyState icon="🛡" message="No validation results" sub="Click Validate to analyze object dependencies" />;
  const bannerBg = validation.status === 'PASS' ? '#0a1e10' : validation.status === 'FAIL' ? '#180808' : '#1a1200';
  return (
    <div>
      <div style={{ padding: '12px 16px', display: 'flex', alignItems: 'center', gap: 10, background: bannerBg, borderBottom: `1px solid ${T.border}` }}>
        <StatusBadge status={validation.status} />
        <span style={{ fontSize: 11 }}>{validation.message}</span>
      </div>
      {validation.warnings?.length > 0 && (
        <AlertBox type="warning">
          <div style={{ fontWeight: 700, fontSize: 10, marginBottom: 6 }}>⚠ WARNINGS</div>
          {validation.warnings.map((w, i) => <div key={i} style={{ marginTop: 4 }}>• {w}</div>)}
        </AlertBox>
      )}
      {validation.errors?.length > 0 && (
        <AlertBox type="error">
          <div style={{ fontWeight: 700, marginBottom: 6 }}>SYNTAX ERRORS</div>
          {validation.errors.map((e, i) => <div key={i}>• {e}</div>)}
        </AlertBox>
      )}
      <div style={{ padding: '8px 12px 4px', color: T.txt3, fontSize: 10, fontWeight: 700 }}>
        AFFECTED OBJECTS ({validation.affectedObjects?.length || 0})
      </div>
      {validation.affectedObjects?.length > 0 ? (
        <DataTable
          columns={['Object', 'Type', 'Schema', 'Depth', 'Dependency Path']}
          rows={validation.affectedObjects.map(o => ({
            Object: o.affectedName, Type: o.affectedType,
            Schema: o.schemaName, Depth: o.depth, 'Dependency Path': o.dependencyPath,
          }))}
        />
      ) : (
        <div style={{ padding: '10px 14px', color: T.txt3, fontSize: 11 }}>
          ✓ No dependent objects found. Safe to apply.
        </div>
      )}
    </div>
  );
}

// ── Version History Tab ────────────────────────────────────────
export function HistoryTab({ versions, backupResult, rollbackResult, diffVA, setDiffVA, diffVB, setDiffVB, handleRollback, handleDiff, setSqlQuery, loading }) {
  return (
    <div>
      {backupResult && (
        <AlertBox type={backupResult.success ? 'success' : 'error'}>
          💾 {backupResult.success ? `Backed up as v${backupResult.versionNumber} · ${backupResult.message}` : backupResult.message}
        </AlertBox>
      )}
      {rollbackResult && (
        <AlertBox type={rollbackResult.success ? 'success' : 'error'}>
          ↩ {rollbackResult.message}
        </AlertBox>
      )}
      {versions.length === 0 ? (
        <EmptyState icon="🕐" message="No version history" sub="Click Backup or Versions to load history" />
      ) : (
        <>
          {versions.length >= 2 && (
            <div style={{ display: 'flex', gap: 8, padding: '8px 12px', background: T.bg1, borderBottom: `1px solid ${T.border}`, alignItems: 'center' }}>
              <span style={{ color: T.txt3, fontSize: 10 }}>COMPARE:</span>
              <select style={{ background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '4px 8px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit' }}
                value={diffVA} onChange={e => setDiffVA(Number(e.target.value))}>
                {versions.map(v => <option key={v.versionNumber} value={v.versionNumber}>v{v.versionNumber}</option>)}
              </select>
              <span style={{ color: T.txt3 }}>→</span>
              <select style={{ background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '4px 8px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit' }}
                value={diffVB} onChange={e => setDiffVB(Number(e.target.value))}>
                {versions.map(v => <option key={v.versionNumber} value={v.versionNumber}>v{v.versionNumber}</option>)}
              </select>
              <Btn color={T.purple} bg="#120e2a" onClick={handleDiff} disabled={loading.diff}>
                {loading.diff ? <Spinner /> : '⟷'} Diff
              </Btn>
            </div>
          )}
          {versions.map(v => (
            <div key={v.id} style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 8, margin: 10, overflow: 'hidden' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 12px', background: T.bg3, borderBottom: `1px solid ${T.border}` }}>
                <span style={{ color: T.gold, fontWeight: 800, fontSize: 13 }}>v{v.versionNumber}</span>
                <Pill label={v.objectType} color={T.purple} />
                <span style={{ color: T.txt3, fontSize: 10, marginLeft: 'auto' }}>
                  {new Date(v.createdAt).toLocaleString()}
                </span>
                <Btn color={T.gold} bg="#1a1200" style={{ padding: '3px 9px', fontSize: 10 }}
                  onClick={() => handleRollback(v.versionNumber)} disabled={loading.rollback}>
                  {loading.rollback ? <Spinner /> : '↩'} Restore
                </Btn>
                <Btn color={T.txt2} bg={T.bg3} style={{ padding: '3px 9px', fontSize: 10 }}
                  onClick={() => setSqlQuery(v.scriptContent)}>
                  ⎘ Load
                </Btn>
              </div>
              <pre style={{ margin: 0, padding: '9px 12px', fontSize: 10, color: T.txt3, maxHeight: 100, overflow: 'auto', whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                {v.scriptContent?.slice(0, 400)}{v.scriptContent?.length > 400 ? '…' : ''}
              </pre>
            </div>
          ))}
        </>
      )}
    </div>
  );
}

// ── Diff Tab ──────────────────────────────────────────────────
export function DiffTab({ diffResult }) {
  if (!diffResult) return <EmptyState icon="⟷" message="No diff yet" sub="Select 2 versions in the History tab and click Diff" />;
  return (
    <div>
      <div style={{ display: 'flex', gap: 12, padding: '10px 14px', background: T.bg1, borderBottom: `1px solid ${T.border}`, alignItems: 'center', flexWrap: 'wrap' }}>
        <span style={{ color: T.txt2, fontSize: 10 }}>v{diffResult.oldVersion} → v{diffResult.newVersion}</span>
        <StatusBadge status={diffResult.riskLevel || 'LOW'} />
        <span style={{ color: T.green, fontSize: 10 }}>+{diffResult.linesAdded} added</span>
        <span style={{ color: T.red,   fontSize: 10 }}>-{diffResult.linesRemoved} removed</span>
      </div>
      {diffResult.aiSummary && (
        <AlertBox type="warning">
          <div style={{ fontWeight: 700, fontSize: 10, marginBottom: 4, color: T.gold }}>AI CHANGE SUMMARY</div>
          <div>{diffResult.aiSummary}</div>
          {diffResult.keyChanges?.map((k, i) => <div key={i} style={{ marginTop: 4, color: T.txt }}>• {k}</div>)}
        </AlertBox>
      )}
      <div style={{ fontFamily: "'JetBrains Mono',monospace", fontSize: 11, margin: 10, border: `1px solid ${T.border}`, borderRadius: 6, overflow: 'auto', maxHeight: 500 }}>
        {diffResult.diff?.map((line, i) => (
          <div key={i} style={{
            padding: '1px 12px',
            background: line.type === 'added' ? '#0a2a1099' : line.type === 'removed' ? '#2a0a0a99' : 'transparent',
            color: line.type === 'added' ? T.green : line.type === 'removed' ? T.red : T.txt3,
          }}>
            {line.type === 'added' ? '+ ' : line.type === 'removed' ? '- ' : '  '}
            {line.content}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Risk Tab ──────────────────────────────────────────────────
export function RiskTab({ riskResult }) {
  if (!riskResult) return <EmptyState icon="⚠" message="No risk analysis yet" sub="Click Risk to analyze the current query" />;
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', background: T.bg1, borderBottom: `1px solid ${T.border}` }}>
        <span style={{ color: T.txt2, fontWeight: 700, fontSize: 11 }}>RISK LEVEL</span>
        <StatusBadge status={riskResult.riskLevel || 'UNKNOWN'} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, margin: 10 }}>
        <div style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 8 }}>
          <div style={{ padding: '8px 12px', color: T.red, fontWeight: 700, fontSize: 10, borderBottom: `1px solid ${T.border}` }}>⛔ IDENTIFIED RISKS</div>
          <div style={{ padding: '8px 12px' }}>
            {(riskResult.risks || []).map((r, i) => (
              <div key={i} style={{ fontSize: 11, color: '#fca5a5', marginBottom: 6, paddingLeft: 8, borderLeft: '2px solid #7f1d1d' }}>{r}</div>
            ))}
            {!riskResult.risks?.length && <span style={{ color: T.txt3 }}>None identified</span>}
          </div>
        </div>
        <div style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 8 }}>
          <div style={{ padding: '8px 12px', color: T.green, fontWeight: 700, fontSize: 10, borderBottom: `1px solid ${T.border}` }}>✅ RECOMMENDATIONS</div>
          <div style={{ padding: '8px 12px' }}>
            {(riskResult.recommendations || []).map((r, i) => (
              <div key={i} style={{ fontSize: 11, color: '#86efac', marginBottom: 6, paddingLeft: 8, borderLeft: '2px solid #14401e' }}>{r}</div>
            ))}
            {!riskResult.recommendations?.length && <span style={{ color: T.txt3 }}>None</span>}
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Explain Tab ───────────────────────────────────────────────
export function ExplainTab({ explanation }) {
  if (!explanation) return <EmptyState icon="💡" message="No explanation yet" sub="Click Explain for an AI breakdown of the query" />;
  return (
    <div style={{ margin: 14 }}>
      <div style={{ color: T.cyan, fontWeight: 700, fontSize: 10, marginBottom: 10, letterSpacing: '.07em' }}>💡 AI EXPLANATION</div>
      <div style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 7, padding: 16, lineHeight: 1.85, fontSize: 12, color: T.txt, whiteSpace: 'pre-wrap' }}>
        {explanation}
      </div>
    </div>
  );
}

// ── Schema Tab ────────────────────────────────────────────────
export function SchemaTab({ schema }) {
  const [search, setSearch] = useState('');
  if (!schema) return <EmptyState icon="🗃" message="No schema loaded" sub="Click Schema to extract database structure" />;
  const filtered = schema.tables?.filter(t =>
    !search || t.name.toLowerCase().includes(search.toLowerCase())
  ) || [];
  return (
    <div>
      <div style={{ display: 'flex', gap: 14, padding: '8px 14px', background: T.bg1, borderBottom: `1px solid ${T.border}`, flexWrap: 'wrap', alignItems: 'center' }}>
        <span style={{ color: T.txt2 }}>DB: <b style={{ color: T.gold }}>{schema.databaseName}</b></span>
        <span style={{ color: T.txt2 }}>Tables: <b style={{ color: T.txt }}>{schema.tables?.length}</b></span>
        <span style={{ color: T.txt2 }}>Views: <b style={{ color: T.txt }}>{schema.views?.length}</b></span>
        <span style={{ color: T.txt2 }}>Procs: <b style={{ color: T.txt }}>{schema.procedures?.length}</b></span>
        <input
          style={{ background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '4px 8px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none', marginLeft: 'auto' }}
          placeholder="🔍 Filter tables…" value={search} onChange={e => setSearch(e.target.value)}
        />
      </div>
      {filtered.map(tbl => (
        <div key={tbl.name} style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 8, margin: 10, overflow: 'hidden' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 12px', background: T.bg3, borderBottom: `1px solid ${T.border}` }}>
            <span style={{ color: T.blue, fontWeight: 700 }}>{tbl.schema}.{tbl.name}</span>
            <Pill label="TABLE" color={T.blue} />
            <span style={{ color: T.txt3, fontSize: 10, marginLeft: 'auto' }}>{tbl.rowCount?.toLocaleString()} rows</span>
            <span style={{ color: T.txt3, fontSize: 10 }}>{tbl.columns?.length} cols · {tbl.indexes?.length} idx</span>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, padding: '8px 12px' }}>
            {tbl.columns?.map(c => (
              <span key={c.name} style={{
                background: c.isPrimaryKey ? '#1a1000' : T.bg1,
                border: `1px solid ${c.isPrimaryKey ? T.gold : T.border}`,
                color: c.isPrimaryKey ? T.gold : T.txt2,
                padding: '2px 7px', borderRadius: 4, fontSize: 10,
              }}>
                {c.isPrimaryKey ? '🔑 ' : ''}{c.name}
                <span style={{ color: T.txt3 }}> {c.dataType}{c.isNullable ? '' : '!'}</span>
              </span>
            ))}
          </div>
          {tbl.foreignKeys?.length > 0 && (
            <div style={{ padding: '4px 12px 8px', fontSize: 10, color: T.txt3 }}>
              {tbl.foreignKeys.map((fk, i) => (
                <span key={i} style={{ marginRight: 10 }}>
                  FK: {fk.foreignKeyColumn} → {fk.referencedTable}.{fk.referencedColumn}
                </span>
              ))}
            </div>
          )}
        </div>
      ))}
      {schema.procedures?.length > 0 && (
        <div style={{ padding: '8px 12px' }}>
          <div style={{ color: T.txt3, fontSize: 10, fontWeight: 700, marginBottom: 8 }}>PROCEDURES ({schema.procedures.length})</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {schema.procedures.map(p => (
              <span key={p.name} style={{ background: T.bg2, border: `1px solid ${T.border}`, color: T.purple, padding: '3px 8px', borderRadius: 4, fontSize: 10 }}>
                {p.schema}.{p.name}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Optimizer Tab ─────────────────────────────────────────────
export function OptimizerTab({ optimizerResult, missingIndexes }) {
  if (!optimizerResult) return <EmptyState icon="⚡" message="No analysis yet" sub="Click Optimize to analyze query plan and missing indexes" />;
  return (
    <div>
      <div style={{ display: 'flex', gap: 14, padding: '8px 14px', background: T.bg1, borderBottom: `1px solid ${T.border}` }}>
        <span style={{ color: T.txt2 }}>Est. Cost: <b style={{ color: T.gold }}>{optimizerResult.estimatedCost?.toFixed(4)}</b></span>
        <span style={{ color: T.txt2 }}>Risk: <b style={{ color: T.red }}>{optimizerResult.overallRisk}</b></span>
        <span style={{ color: T.txt2 }}>Missing Indexes: <b style={{ color: T.txt }}>{missingIndexes.length}</b></span>
      </div>
      {optimizerResult.suggestions?.length > 0 && (
        <AlertBox type="warning">
          <div style={{ fontWeight: 700, fontSize: 10, marginBottom: 6 }}>💡 QUERY SUGGESTIONS</div>
          {optimizerResult.suggestions.map((s, i) => <div key={i} style={{ marginTop: 4 }}>• {s}</div>)}
        </AlertBox>
      )}
      {missingIndexes.length > 0 && (
        <div style={{ margin: 10 }}>
          <div style={{ color: T.txt3, fontSize: 10, fontWeight: 700, marginBottom: 8 }}>MISSING INDEX RECOMMENDATIONS</div>
          {missingIndexes.map((idx, i) => (
            <div key={i} style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 7, marginBottom: 8, overflow: 'hidden' }}>
              <div style={{ padding: '7px 12px', background: T.bg3, borderBottom: `1px solid ${T.border}`, display: 'flex', gap: 10, alignItems: 'center' }}>
                <span style={{ color: T.blue, fontWeight: 700 }}>{idx.tableName}</span>
                <span style={{ color: T.amber, fontSize: 10 }}>Impact: {idx.improvementFactor?.toFixed(0)}</span>
              </div>
              <pre style={{ margin: 0, padding: '8px 12px', fontSize: 10, color: T.green, whiteSpace: 'pre-wrap' }}>
                {idx.createStatement}
              </pre>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── MongoDB Tab ───────────────────────────────────────────────
export function MongoTab({ mongoResult, mongoDb, setMongoDb, mongoCollection, setMongoCollection, mongoQuery, setMongoQuery, mongoQueryType, setMongoQueryType, handleMongoQuery, loading }) {
  const inputStyle = { background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '5px 9px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none' };
  return (
    <div>
      <div style={{ padding: '10px 12px', background: T.bg1, borderBottom: `1px solid ${T.border}` }}>
        <div style={{ display: 'flex', gap: 8, marginBottom: 8, flexWrap: 'wrap' }}>
          <input style={{ ...inputStyle, flex: 1 }} placeholder="Database name" value={mongoDb} onChange={e => setMongoDb(e.target.value)} />
          <input style={{ ...inputStyle, flex: 1 }} placeholder="Collection name" value={mongoCollection} onChange={e => setMongoCollection(e.target.value)} />
          <select style={{ ...inputStyle }} value={mongoQueryType} onChange={e => setMongoQueryType(e.target.value)}>
            <option value="find">find</option>
            <option value="aggregate">aggregate</option>
            <option value="count">count</option>
            <option value="distinct">distinct</option>
          </select>
        </div>
        <textarea
          style={{ ...inputStyle, width: '100%', height: 80, resize: 'none', display: 'block', marginBottom: 8 }}
          value={mongoQuery} onChange={e => setMongoQuery(e.target.value)}
          placeholder={mongoQueryType === 'aggregate' ? '[{ "$match": {} }, { "$limit": 10 }]' : '{ "status": "active" }'}
          spellCheck={false}
        />
        <Btn color={T.green} bg="#0a1e10" onClick={handleMongoQuery} disabled={loading.mongo}>
          {loading.mongo ? <Spinner /> : '▶'} Execute (Safe Read)
        </Btn>
      </div>
      {mongoResult && (
        <>
          <div style={{ display: 'flex', gap: 14, padding: '8px 14px', background: T.bg1, borderBottom: `1px solid ${T.border}` }}>
            <span style={{ color: mongoResult.success ? T.green : T.red }}>{mongoResult.success ? '✓' : '✗'} {mongoResult.mode}</span>
            <span style={{ color: T.txt2 }}>Docs: <b style={{ color: T.txt }}>{mongoResult.rowCount}</b></span>
            <span style={{ color: T.txt2 }}>Time: <b style={{ color: T.txt }}>{fmtMs(mongoResult.executionMs)}</b></span>
          </div>
          {mongoResult.errors?.length > 0 && <AlertBox type="error">{mongoResult.errors.map((e, i) => <div key={i}>{e}</div>)}</AlertBox>}
          <div style={{ fontFamily: "'JetBrains Mono',monospace", fontSize: 11, margin: 10, border: `1px solid ${T.border}`, borderRadius: 6, overflow: 'auto', maxHeight: 400 }}>
            {mongoResult.resultJson?.map((doc, i) => (
              <div key={i} style={{ padding: '6px 12px', borderBottom: `1px solid ${T.bg1}`, color: T.txt, background: i % 2 === 0 ? 'transparent' : T.bg1 }}>
                {doc}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

// ── Connections Tab ────────────────────────────────────────────
export function ConnectionsTab({ connections, connForm, setConnForm, connTestResult, handleSaveConn, handleTestConn, loading, handleLoadConnections }) {
  const inputStyle = { background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '5px 9px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none', width: '100%' };
  return (
    <div>
      {connTestResult && (
        <AlertBox type={connTestResult.success ? 'success' : 'error'}>
          {connTestResult.success ? '✓' : '✗'} {connTestResult.message}
          {connTestResult.serverVersion && ` · ${connTestResult.serverVersion.slice(0, 80)}`}
        </AlertBox>
      )}
      <div style={{ background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 8, margin: 10, overflow: 'hidden' }}>
        <div style={{ padding: '8px 12px', background: T.bg3, borderBottom: `1px solid ${T.border}`, color: T.txt2, fontWeight: 700, fontSize: 10 }}>＋ NEW CONNECTION</div>
        <div style={{ padding: 12, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          {[['name', 'Name'], ['host', 'Host'], ['databaseName', 'Database'], ['username', 'Username']].map(([f, l]) => (
            <label key={f} style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: T.txt3 }}>
              {l}
              <input style={inputStyle} value={connForm[f] || ''} onChange={e => setConnForm(p => ({ ...p, [f]: e.target.value }))} />
            </label>
          ))}
          <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: T.txt3 }}>
            Password
            <input style={inputStyle} type="password" value={connForm.password || ''} onChange={e => setConnForm(p => ({ ...p, password: e.target.value }))} />
          </label>
          <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: T.txt3 }}>
            Port
            <input style={inputStyle} type="number" value={connForm.port || 1433} onChange={e => setConnForm(p => ({ ...p, port: Number(e.target.value) }))} />
          </label>
          <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: T.txt3 }}>
            Type
            <select style={{ ...inputStyle }} value={connForm.databaseType || 'SqlServer'} onChange={e => setConnForm(p => ({ ...p, databaseType: e.target.value }))}>
              <option>SqlServer</option><option>MongoDB</option>
            </select>
          </label>
        </div>
        <div style={{ padding: '0 12px 12px', display: 'flex', gap: 8 }}>
          <Btn color={T.green} bg="#0a1e10" onClick={handleSaveConn} disabled={loading.saveConn}>
            {loading.saveConn ? <Spinner /> : '＋'} Save Profile
          </Btn>
        </div>
      </div>
      {connections.length === 0 ? (
        <div style={{ padding: 20, textAlign: 'center', color: T.txt3, fontSize: 11 }}>
          No saved connections.
          <div style={{ marginTop: 10 }}>
            <Btn color={T.txt2} bg={T.bg3} onClick={handleLoadConnections}>🔄 Load Connections</Btn>
          </div>
        </div>
      ) : (
        <DataTable
          columns={['Name', 'Type', 'Host', 'Database', 'Status', 'Test']}
          rows={connections.map(c => ({
            Name: c.name, Type: c.databaseType,
            Host: `${c.host}:${c.port}`, Database: c.databaseName,
            Status: <StatusBadge status={c.lastTestOk ? 'SUCCESS' : 'UNKNOWN'} />,
            Test: <Btn color={T.green} bg="#0a1e10" style={{ padding: '3px 8px', fontSize: 10 }} onClick={() => handleTestConn(c.id)} disabled={loading.testConn}>{loading.testConn ? <Spinner /> : '▶'} Test</Btn>,
          }))}
        />
      )}
    </div>
  );
}

// ── Audit Log Tab ─────────────────────────────────────────────
export function AuditTab({ auditLogs }) {
  if (auditLogs.length === 0) return <EmptyState icon="📋" message="No audit logs" sub="Click Audit to load the activity log" />;
  return (
    <DataTable
      columns={['#', 'Action', 'Object', 'Type', 'Status', 'Model', 'Duration', 'Time']}
      rows={auditLogs.map(l => ({
        '#': l.id,
        Action: l.action,
        Object: l.objectName,
        Type: l.objectType || '—',
        Status: <StatusBadge status={l.status || '—'} />,
        Model: l.modelUsed || '—',
        Duration: fmtMs(l.durationMs || 0),
        Time: new Date(l.createdAt).toLocaleTimeString(),
      }))}
    />
  );
}

// ── Schedules Tab ─────────────────────────────────────────────
export function SchedulesTab({ schedules, handleLoadSchedules, handleAddSchedule, objectName, loading }) {
  const [interval, setInterval] = useState(60);
  return (
    <div>
      <div style={{ display: 'flex', gap: 8, padding: '10px 12px', background: T.bg1, borderBottom: `1px solid ${T.border}`, alignItems: 'center' }}>
        <span style={{ color: T.txt3, fontSize: 10 }}>Schedule backup for: <b style={{ color: T.txt }}>{objectName || '(set object name)'}</b></span>
        <input
          type="number" min="5" step="5"
          style={{ background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '4px 8px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none', width: 80 }}
          value={interval} onChange={e => setInterval(Number(e.target.value))}
        />
        <span style={{ color: T.txt3, fontSize: 10 }}>min</span>
        <Btn color={T.green} bg="#0a1e10" onClick={() => handleAddSchedule(interval)} disabled={loading.addSched || !objectName}>
          {loading.addSched ? <Spinner /> : '＋'} Add Schedule
        </Btn>
        <Btn color={T.txt2} bg={T.bg3} onClick={handleLoadSchedules} disabled={loading.schedules}>
          {loading.schedules ? <Spinner /> : '🔄'} Refresh
        </Btn>
      </div>
      {schedules.length === 0 ? (
        <EmptyState icon="🕐" message="No schedules configured" sub="Add a schedule above to auto-backup objects" />
      ) : (
        <DataTable
          columns={['Object', 'Type', 'Every (min)', 'Enabled', 'Last Run', 'Last Status']}
          rows={schedules.map(s => ({
            Object: s.objectName,
            Type: s.objectType,
            'Every (min)': s.intervalMinutes,
            Enabled: s.isEnabled ? '✓ Yes' : '✗ No',
            'Last Run': s.lastRunAt ? new Date(s.lastRunAt).toLocaleString() : 'Never',
            'Last Status': s.lastStatus || '—',
          }))}
        />
      )}
    </div>
  );
}

// ── Preferences Tab ────────────────────────────────────────────
export function PreferencesTab({ preferences, handleSavePreferences, loading }) {
  const [local, setLocal] = useState(preferences || {
    theme: 'dark', defaultModel: 'auto', defaultDbType: 'SqlServer',
    autoBackupOnApply: true, showExecutionPlan: false,
    previewRowLimit: 500, auditLogRetainDays: 30, showLineNumbers: true, fontSize: '12px',
  });
  const f = (key) => ({
    value: local[key],
    onChange: e => setLocal(p => ({ ...p, [key]: e.target.type === 'checkbox' ? e.target.checked : e.target.value })),
  });
  const inputStyle = { background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt, padding: '5px 9px', borderRadius: T.r, fontSize: 11, fontFamily: 'inherit', outline: 'none' };
  return (
    <div style={{ padding: 16, maxWidth: 500 }}>
      <div style={{ color: T.txt2, fontWeight: 700, fontSize: 10, marginBottom: 16, letterSpacing: '.07em' }}>⚙ USER PREFERENCES</div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {[
          ['defaultModel', 'Default Model', 'text'],
          ['defaultDbType', 'Default DB Type', 'text'],
          ['previewRowLimit', 'Preview Row Limit', 'number'],
          ['auditLogRetainDays', 'Audit Retain (days)', 'number'],
          ['fontSize', 'Font Size', 'text'],
        ].map(([key, label, type]) => (
          <label key={key} style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: T.txt3 }}>
            {label}
            <input style={{ ...inputStyle, width: '100%' }} type={type} {...f(key)} />
          </label>
        ))}
        {[
          ['autoBackupOnApply', 'Auto-backup before Apply'],
          ['showExecutionPlan', 'Show Execution Plan'],
          ['showLineNumbers', 'Show Line Numbers'],
        ].map(([key, label]) => (
          <label key={key} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11, color: T.txt, cursor: 'pointer' }}>
            <input type="checkbox" checked={local[key] || false} onChange={e => setLocal(p => ({ ...p, [key]: e.target.checked }))} />
            {label}
          </label>
        ))}
      </div>
      <div style={{ marginTop: 16 }}>
        <Btn color={T.green} bg="#0a1e10" onClick={() => handleSavePreferences(local)} disabled={loading.savePrefs}>
          {loading.savePrefs ? <Spinner /> : '💾'} Save Preferences
        </Btn>
      </div>
    </div>
  );
}
