// ============================================================
// KITSUNE – LeftPane Component
// NL input, SQL editor, object config, all action buttons
// ============================================================
import React from 'react';
import { T, Btn, SectionHeader, Spinner } from './SharedComponents';

const css = {
  pane: {
    display: 'flex', flexDirection: 'column',
    width: 420, borderRight: `1px solid ${T.border}`,
    flexShrink: 0, overflow: 'hidden',
  },
  textarea: {
    background: T.bg1, border: 'none', color: T.txt,
    padding: '9px 12px', fontFamily: "'JetBrains Mono',monospace",
    fontSize: 11.5, resize: 'none', outline: 'none', lineHeight: 1.7, width: '100%',
  },
  input: {
    background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt,
    padding: '5px 9px', borderRadius: T.r, fontSize: 11,
    fontFamily: 'inherit', outline: 'none', width: '100%',
  },
  select: {
    background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt,
    padding: '5px 8px', borderRadius: T.r, fontSize: 11,
    fontFamily: 'inherit', cursor: 'pointer', outline: 'none',
  },
  row: { display: 'flex', gap: 7, padding: '7px 12px', alignItems: 'center', flexWrap: 'wrap' },
};

const OBJECT_TYPES = ['PROCEDURE', 'FUNCTION', 'VIEW', 'TABLE', 'TRIGGER'];
const DB_TYPES     = ['SqlServer', 'MongoDB'];

export function LeftPane({
  nlQuery, setNlQuery, sqlQuery, setSqlQuery,
  objectName, setObjectName, objectType, setObjectType,
  dbType, setDbType, loading,
  handleGenerate, handleValidate, handlePreview, handleBackup,
  handleVersions, handleRollback, handleApply, handleRisk,
  handleExplain, handleSchema, handleLoadAudit,
  handleOptimize, handleMongoQuery,
  genMeta,
}) {
  return (
    <div style={css.pane}>
      {/* ── NL Input ─────────────────────────────────────── */}
      <SectionHeader label="◆ NATURAL LANGUAGE QUERY" />
      <div style={{ padding: '9px 12px' }}>
        <textarea
          style={{ ...css.textarea, height: 76, borderRadius: 6, border: `1px solid ${T.border2}` }}
          value={nlQuery}
          onChange={e => setNlQuery(e.target.value)}
          placeholder="Describe your query in plain English…"
        />
      </div>
      <div style={css.row}>
        <select style={{ ...css.select, flex: 1 }} value={dbType} onChange={e => setDbType(e.target.value)}>
          {DB_TYPES.map(t => <option key={t}>{t}</option>)}
        </select>
        <Btn color={T.green} bg="#0a1e10" onClick={handleGenerate} disabled={loading.gen}>
          {loading.gen ? <Spinner /> : '▶'} Generate
        </Btn>
      </div>
      {genMeta && (
        <div style={{ padding: '0 12px 8px', fontSize: 10, color: T.txt3, fontStyle: 'italic' }}>
          {genMeta}
        </div>
      )}

      {/* ── SQL Editor ───────────────────────────────────── */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', borderTop: `1px solid ${T.border}`, overflow: 'hidden' }}>
        <SectionHeader
          label="▣ SQL / QUERY EDITOR"
          right={
            <button
              onClick={() => navigator.clipboard?.writeText(sqlQuery)}
              style={{ background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 11 }}
            >
              ⎘ Copy
            </button>
          }
        />
        <textarea
          style={{ ...css.textarea, flex: 1, minHeight: 140, background: '#050e1c' }}
          value={sqlQuery}
          onChange={e => setSqlQuery(e.target.value)}
          placeholder={"-- Generated SQL appears here, or type your own…\n-- Preview runs in BEGIN TRAN/ROLLBACK (safe mode)"}
          spellCheck={false}
        />
      </div>

      {/* ── Object Config ────────────────────────────────── */}
      <div style={{ borderTop: `1px solid ${T.border}` }}>
        <SectionHeader label="⚙ OBJECT CONFIGURATION" />
        <div style={css.row}>
          <input
            style={{ ...css.input, flex: 1 }}
            placeholder="Object name (e.g. usp_GetOrders)"
            value={objectName}
            onChange={e => setObjectName(e.target.value)}
          />
          <select style={css.select} value={objectType} onChange={e => setObjectType(e.target.value)}>
            {OBJECT_TYPES.map(t => <option key={t}>{t}</option>)}
          </select>
        </div>
      </div>

      {/* ── Action Buttons ────────────────────────────────── */}
      <div style={{ display: 'flex', gap: 5, padding: '7px 12px', flexWrap: 'wrap', borderTop: `1px solid ${T.border}` }}>
        <Btn color={T.green}  bg="#0a1e10" onClick={handleValidate} disabled={loading.validate}>
          {loading.validate ? <Spinner /> : '🛡'} Validate
        </Btn>
        <Btn color={T.purple} bg="#120e2a" onClick={handlePreview}  disabled={loading.preview}>
          {loading.preview  ? <Spinner /> : '👁'} Preview
        </Btn>
        <Btn color={T.gold}   bg="#1a1200" onClick={handleBackup}   disabled={loading.backup}>
          {loading.backup   ? <Spinner /> : '💾'} Backup
        </Btn>
        <Btn color={T.txt2}   bg={T.bg3}   onClick={handleVersions} disabled={loading.versions}>
          {loading.versions ? <Spinner /> : '🕐'} Versions
        </Btn>
        <Btn color={T.red}    bg="#180808" onClick={handleRisk}     disabled={loading.risk}>
          {loading.risk     ? <Spinner /> : '⚠'} Risk
        </Btn>
        <Btn color={T.cyan}   bg="#071828" onClick={handleExplain}  disabled={loading.explain}>
          {loading.explain  ? <Spinner /> : '💡'} Explain
        </Btn>
        <Btn color={T.amber}  bg="#1a1000" onClick={handleSchema}   disabled={loading.schema}>
          {loading.schema   ? <Spinner /> : '🗃'} Schema
        </Btn>
        <Btn color={T.blue}   bg={T.bg3}   onClick={handleOptimize} disabled={loading.optimize}>
          {loading.optimize ? <Spinner /> : '⚡'} Optimize
        </Btn>
        <Btn color={T.txt2}   bg={T.bg3}   onClick={handleLoadAudit} disabled={loading.audit}>
          {loading.audit    ? <Spinner /> : '📋'} Audit
        </Btn>
      </div>

      {/* ── Apply Button ──────────────────────────────────── */}
      <button
        onClick={handleApply}
        disabled={loading.apply}
        style={{
          display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 7,
          margin: '0 12px 10px', padding: '8px', borderRadius: T.r,
          fontFamily: 'inherit', fontSize: 12, fontWeight: 700,
          cursor: loading.apply ? 'not-allowed' : 'pointer',
          border: '1px solid #7f1d1d', background: '#180808', color: T.red,
          width: 'calc(100% - 24px)', opacity: loading.apply ? 0.5 : 1,
        }}
      >
        {loading.apply ? <Spinner /> : '⚡'} APPLY CHANGE (LIVE)
      </button>
    </div>
  );
}
