// ============================================================
// KITSUNE – Main App v6
// Mode-aware UI: READ (safe) vs WRITE (risky)
// Dynamic tabs, intent detection, confidence score,
// server-specific explorer, fixed text contrast
// ============================================================
import React, { useEffect, useCallback } from 'react';
import { useKitsune }          from './hooks/useKitsune';
import { useModeManager }      from './hooks/useModeManager';
import { ConnectionScreen }    from './components/ConnectionScreen';
import { SchemaExplorer }      from './components/SchemaExplorer';
import { ObjectSelector }      from './components/ObjectSelector';
import { DependencyMap }       from './components/DependencyMap';
import { ResizablePanel }      from './components/ResizablePanel';
import { ExportTab }           from './components/ExportPanel';
import { ScriptRunnerTab }     from './components/ScriptRunner';
import { SqlEditor }           from './components/SqlEditor';
import {
  ModeBadge, ConfidenceBar, SafetyWarning,
  DynamicTabBar, AILoading, ReadModeResults, EmptyQueryState,
} from './components/ModeComponents';
import { T, NotificationToast, globalStyles } from './components/SharedComponents';
import {
  ValidationTab, HistoryTab, DiffTab,
  RiskTab, ExplainTab, SchemaTab, OptimizerTab,
  MongoTab, ConnectionsTab, AuditTab, SchedulesTab, PreferencesTab,
} from './components/Panels';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

// ── DB Type → header label (only show connected type) ─────────
const DB_DISPLAY = {
  SqlServer:  { label: 'SQL Server', icon: '🗄', color: '#4a8eff' },
  MongoDB:    { label: 'MongoDB',    icon: '🍃', color: '#3dba6e' },
  MySQL:      { label: 'MySQL',      icon: '🐬', color: '#f59e0b' },
  PostgreSQL: { label: 'PostgreSQL', icon: '🐘', color: '#38bdf8' },
};

export default function KitsuneApp() {
  const k    = useKitsune();
  const mode = useModeManager();

  // ── Connection state ──────────────────────────────────────
  const [connected,    setConnected]    = React.useState(false);
  const [connInfo,     setConnInfo]     = React.useState(null);
  const [showExplorer, setShowExplorer] = React.useState(true);
  const [genLoading,   setGenLoading]   = React.useState(false);

  const handleConnected = (info) => {
    setConnInfo(info);
    setConnected(true);
    if (info.databaseType) k.setDbType(info.databaseType);
  };

  // ── Analyze query whenever SQL changes ────────────────────
  useEffect(() => {
    mode.analyze(k.sqlQuery, k.dbType);
  }, [k.sqlQuery, k.dbType]);

  // ── Schema explorer select → load definition ──────────────
  const handleSchemaObjectSelect = (obj) => {
    k.setObjectName(obj.fullName || obj.name);
    k.setObjectType(obj.type?.toUpperCase() || 'TABLE');
    if (obj.definition) {
      k.setSqlQuery(obj.definition);
      k.notify?.(`Loaded: ${obj.name}`, 'success');
    }
  };

  // ── Generate with AI loading indicator ───────────────────
  const handleGenerate = async () => {
    if (!k.nlQuery?.trim()) return;
    setGenLoading(true);
    await k.handleGenerate();
    setGenLoading(false);
  };

  // ── Render panel content based on active tab ──────────────
  const renderPanel = () => {
    switch (mode.activeTab) {
      case 'results':
        // READ mode: results with AI explanation above
        if (mode.mode === 'read') {
          return <ReadModeResults
            preview={k.preview}
            explanation={k.explanation}
            loadingExplain={k.loading?.explain}
          />;
        }
        // WRITE mode: standard results
        return k.preview ? (
          <div>
            <div style={{ display:'flex', gap:14, padding:'8px 14px', background:T.bg1, borderBottom:`1px solid ${T.border}`, fontSize:11 }}>
              <span style={{ color: k.preview.success ? T.green : T.red }}>
                {k.preview.success ? '✓ Preview' : '✗ Error'}
              </span>
              <span style={{ color: T.txt2 }}>Rows: <b style={{ color: T.txt }}>{k.preview.rowCount}</b></span>
              <span style={{ color: T.txt2 }}>Time: <b style={{ color: T.txt }}>{k.preview.executionMs?.toFixed(0)}ms</b></span>
              <span style={{ marginLeft:'auto', fontSize:10, color: T.amber, fontWeight:700 }}>
                🔴 WRITE MODE
              </span>
            </div>
            {k.preview.errors?.length > 0 && (
              <div style={{ margin:'8px 10px', padding:'8px 12px', background:'#180808', border:`1px solid #7f1d1d`, borderRadius:6, fontSize:11, color:'#fca5a5' }}>
                {k.preview.errors.map((e,i) => <div key={i}>{e}</div>)}
              </div>
            )}
            {k.preview.resultSet?.length > 0 && (
              <div style={{ margin:'0 10px 10px', overflowX:'auto' }}>
                <table style={{ width:'100%', borderCollapse:'collapse', fontSize:11 }}>
                  <thead><tr>{k.preview.columns.map(c =>
                    <th key={c} style={{ background:T.bg3, color:T.blue, padding:'5px 10px', textAlign:'left', fontWeight:700, borderBottom:`1px solid ${T.border}` }}>{c}</th>
                  )}</tr></thead>
                  <tbody>{k.preview.resultSet.map((row,i) => (
                    <tr key={i} style={{ background: i%2===0 ? T.bg2 : T.bg1 }}>
                      {k.preview.columns.map(c => (
                        <td key={c} style={{ padding:'5px 10px', borderBottom:`1px solid ${T.bg0}`, color: row[c]===null ? T.txt3 : T.txt }}>
                          {row[c]===null ? <i style={{ color:T.txt3 }}>NULL</i> : String(row[c])}
                        </td>
                      ))}
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            )}
          </div>
        ) : <EmptyQueryState dbType={k.dbType} />;

      case 'validation':
        return <ValidationTab validation={k.validation} />;

      case 'history':
        return <HistoryTab
          versions={k.versions} backupResult={k.backupResult} rollbackResult={k.rollbackResult}
          diffVA={k.diffVA} setDiffVA={k.setDiffVA} diffVB={k.diffVB} setDiffVB={k.setDiffVB}
          handleRollback={k.handleRollback} handleDiff={k.handleDiff}
          setSqlQuery={k.setSqlQuery} loading={k.loading}
        />;

      case 'diff':
        return <DiffTab diffResult={k.diffResult} />;

      case 'depmap':
        return <DependencyMap objectName={k.objectName} objectType={k.objectType} />;

      case 'risk':
        return <RiskTab riskResult={k.riskResult} />;

      case 'explain':
        return <ExplainTab explanation={k.explanation} />;

      case 'schema':
        return <SchemaTab schema={k.schema} />;

      case 'optimizer':
        return <OptimizerTab optimizerResult={k.optimizerResult} missingIndexes={k.missingIndexes} />;

      case 'mongo':
        return <MongoTab
          mongoResult={k.mongoResult} mongoDb={k.mongoDb} setMongoDb={k.setMongoDb}
          mongoCollection={k.mongoCollection} setMongoCollection={k.setMongoCollection}
          mongoQuery={k.mongoQuery} setMongoQuery={k.setMongoQuery}
          mongoQueryType={k.mongoQueryType} setMongoQueryType={k.setMongoQueryType}
          handleMongoQuery={k.handleMongoQuery} loading={k.loading}
        />;

      case 'script':
        return <ScriptRunnerTab sqlQuery={k.sqlQuery} />;

      case 'export':
        return <ExportTab sqlQuery={k.sqlQuery} />;

      case 'connections':
        return <ConnectionsTab
          connections={k.connections} connForm={k.connForm} setConnForm={k.setConnForm}
          connTestResult={k.connTestResult} handleSaveConn={k.handleSaveConn}
          handleTestConn={k.handleTestConn} loading={k.loading}
          handleLoadConnections={k.handleLoadConnections}
        />;

      case 'audit':
        return <AuditTab auditLogs={k.auditLogs} />;

      case 'schedules':
        return <SchedulesTab
          schedules={k.schedules} objectName={k.objectName}
          handleLoadSchedules={k.handleLoadSchedules}
          handleAddSchedule={k.handleAddSchedule} loading={k.loading}
        />;

      case 'preferences':
        return <PreferencesTab
          preferences={k.preferences} handleSavePreferences={k.handleSavePreferences}
          loading={k.loading}
        />;

      default:
        return <EmptyQueryState dbType={k.dbType} />;
    }
  };

  if (!connected) return <ConnectionScreen onConnected={handleConnected} />;

  const dbInfo = DB_DISPLAY[k.dbType] || DB_DISPLAY.SqlServer;
  const anyLoading = Object.values(k.loading || {}).some(Boolean) || genLoading;

  return (
    <div style={{
      display: 'flex', flexDirection: 'column', height: '100vh',
      background: T.bg0, color: T.txt,
      fontFamily: "'JetBrains Mono',monospace", fontSize: 12, overflow: 'hidden',
    }}>
      <style>{globalStyles}</style>

      {/* ── TOP BAR ──────────────────────────────────────── */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 10, padding: '0 14px', height: 46,
        background: '#040c18', borderBottom: `1px solid ${T.border}`, flexShrink: 0,
      }}>
        {/* Logo */}
        <div style={{ display:'flex', alignItems:'center', gap:8, color:T.gold, fontWeight:800, fontSize:14, letterSpacing:'.09em', flexShrink:0 }}>
          <span style={{ fontSize:20 }}>🦊</span> KITSUNE
          <span style={{ color:T.txt3, fontSize:9, fontWeight:400 }}>AI DATABASE INTELLIGENCE</span>
        </div>

        {/* Connected DB type indicator – ONLY shows current type */}
        <div style={{
          display:'flex', alignItems:'center', gap:6, padding:'3px 10px', borderRadius:5,
          background: `${dbInfo.color}18`, border:`1px solid ${dbInfo.color}44`, flexShrink:0,
        }}>
          <span style={{ fontSize:14 }}>{dbInfo.icon}</span>
          <span style={{ fontSize:11, fontWeight:700, color: dbInfo.color }}>{dbInfo.label}</span>
        </div>

        {/* Mode badge */}
        <ModeBadge
          mode={mode.mode}
          modeLabel={mode.modeLabel}
          modeColor={mode.modeColor}
          primaryStmt={mode.primaryStmt}
        />

        {/* Connection pill */}
        {connInfo && (
          <div style={{
            display:'flex', alignItems:'center', gap:6, padding:'3px 10px', borderRadius:5,
            background:T.bg3, border:`1px solid ${T.green}44`, color:T.green, fontSize:10,
          }}>
            <span style={{ width:6, height:6, borderRadius:'50%', background:T.green, flexShrink:0 }} />
            {connInfo.connectionName}
            <button onClick={() => setConnected(false)} style={{ background:'none', border:'none', color:T.txt3, cursor:'pointer', fontSize:11, padding:0, marginLeft:2 }}>×</button>
          </div>
        )}

        {/* Explorer toggle */}
        <button onClick={() => setShowExplorer(e => !e)} style={{
          background: showExplorer ? T.bg4 : 'none',
          border: `1px solid ${showExplorer ? T.border2 : 'transparent'}`,
          color: showExplorer ? T.txt : T.txt3,
          padding:'3px 10px', borderRadius:4, cursor:'pointer', fontFamily:'inherit', fontSize:10, fontWeight:700,
        }}>
          ⑃ Explorer
        </button>

        <div style={{ flex:1 }} />

        {/* Model selector */}
        <div style={{ display:'flex', alignItems:'center', gap:7, flexShrink:0 }}>
          <span style={{ color:T.txt3, fontSize:10 }}>MODEL</span>
          <select value={k.model} onChange={e => k.setModel(e.target.value)} style={{
            background:T.bg2, border:`1px solid ${T.gold}55`, color:T.gold,
            padding:'4px 8px', borderRadius:5, fontSize:11, fontFamily:'inherit',
            cursor:'pointer', outline:'none', minWidth:180,
          }}>
            {k.models.map(m => (
              <option key={m.id} value={m.id}>
                {m.display_name}{m.sizeFormatted ? ` (${m.sizeFormatted})` : ''}
              </option>
            ))}
          </select>
          <span style={{ width:7, height:7, borderRadius:'50%', background:T.green }} />
        </div>
      </div>

      {/* ── MAIN LAYOUT ───────────────────────────────────── */}
      <div style={{ display:'flex', flex:1, overflow:'hidden' }}>

        {/* Schema Explorer – server-aware */}
        <SchemaExplorer
          connectionId={connInfo?.profileId}
          dbType={k.dbType}
          onObjectSelect={handleSchemaObjectSelect}
          visible={showExplorer}
        />

        {/* Left Editor Pane */}
        <div style={{
          display:'flex', flexDirection:'column',
          width:380, borderRight:`1px solid ${T.border}`, flexShrink:0, overflow:'hidden',
        }}>
          {/* NL Query */}
          <div style={{ padding:'8px 10px', borderBottom:`1px solid ${T.border}`, flexShrink:0 }}>
            <div style={{ color:T.txt2, fontSize:10, fontWeight:700, marginBottom:5, letterSpacing:'.07em' }}>
              ◆ NATURAL LANGUAGE QUERY
            </div>
            <textarea
              style={{
                width:'100%', height:68, background:T.bg2, border:`1px solid ${T.border2}`,
                color:T.txt, padding:'7px 9px', borderRadius:6,
                fontFamily:'inherit', fontSize:11.5, resize:'none', outline:'none', lineHeight:1.6,
              }}
              value={k.nlQuery} onChange={e => k.setNlQuery(e.target.value)}
              placeholder="Ask in plain English: Show top customers by order value…"
            />
            <div style={{ display:'flex', gap:7, marginTop:7 }}>
              <button onClick={handleGenerate} disabled={genLoading} style={{
                display:'inline-flex', alignItems:'center', gap:5, padding:'6px 14px',
                borderRadius:6, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer',
                border:`1px solid ${T.green}44`, background:'#0a1e10', color:T.green,
                opacity: genLoading ? 0.5 : 1,
              }}>
                {genLoading
                  ? <span style={{ display:'inline-block', width:12, height:12, border:`2px solid #162840`, borderTop:`2px solid ${T.gold}`, borderRadius:'50%', animation:'kspin .8s linear infinite' }} />
                  : '▶'
                } {genLoading ? 'Generating…' : 'Generate'}
              </button>
            </div>
            {k.genMeta && <div style={{ marginTop:5, fontSize:10, color:T.txt3, fontStyle:'italic' }}>{k.genMeta}</div>}
          </div>

          {/* AI Loading */}
          <AILoading visible={genLoading} />

          {/* SQL Editor with line numbers */}
          <div style={{ flex:1, display:'flex', flexDirection:'column', overflow:'hidden', minHeight:0 }}>
            <SqlEditor value={k.sqlQuery} onChange={k.setSqlQuery} />
          </div>

          {/* Object Selector (enhanced) */}
          <ObjectSelector
            objectName={k.objectName}  setObjectName={k.setObjectName}
            objectType={k.objectType}  setObjectType={k.setObjectType}
            onDefinitionLoaded={(def) => { k.setSqlQuery(def); }}
          />

          {/* Safety Warning */}
          <SafetyWarning
            warnings={mode.warnings}
            syntaxErrors={mode.syntaxErrors}
            safetyMsg={mode.safetyMsg}
            mode={mode.mode}
            requiresValidation={mode.requiresValidation}
          />

          {/* Action buttons – context-aware */}
          <div style={{ display:'flex', gap:4, padding:'6px 10px', flexWrap:'wrap', borderTop:`1px solid ${T.border}`, flexShrink:0 }}>
            {mode.mode === 'read' ? (
              // READ MODE actions
              <>
                <button onClick={() => { k.handlePreview(); mode.setActiveTab('results'); }} disabled={k.loading?.preview}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.green}44`, background:'#0a1e10', color:T.green }}>
                  ▶ Run Query
                </button>
                <button onClick={() => { k.handleExplain(); mode.setActiveTab('explain'); }} disabled={k.loading?.explain}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.cyan}44`, background:'#071828', color:T.cyan }}>
                  💡 Explain
                </button>
                <button onClick={() => { k.handleSchema(); mode.setActiveTab('schema'); }} disabled={k.loading?.schema}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.amber}44`, background:'#1a1000', color:T.amber }}>
                  🗃 Schema
                </button>
                <button onClick={() => mode.setActiveTab('export')}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.border}`, background:T.bg3, color:T.txt2 }}>
                  📥 Export
                </button>
              </>
            ) : (
              // WRITE MODE actions
              <>
                <button onClick={() => { k.handleValidate(); mode.setActiveTab('validation'); }} disabled={k.loading?.validate}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.green}44`, background:'#0a1e10', color:T.green }}>
                  🛡 Validate
                </button>
                <button onClick={() => { k.handleRisk(); mode.setActiveTab('risk'); }} disabled={k.loading?.risk}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.red}44`, background:'#180808', color:T.red }}>
                  ⚠ Risk
                </button>
                <button onClick={() => { k.handleBackup(); mode.setActiveTab('history'); }} disabled={k.loading?.backup}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.gold}44`, background:'#1a1200', color:T.gold }}>
                  💾 Backup
                </button>
                <button onClick={() => { k.handlePreview(); mode.setActiveTab('results'); }} disabled={k.loading?.preview}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.purple}44`, background:'#120e2a', color:T.purple }}>
                  👁 Preview
                </button>
                <button onClick={() => { k.handleVersions(); mode.setActiveTab('history'); }}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.border}`, background:T.bg3, color:T.txt2 }}>
                  🕐 Versions
                </button>
                <button onClick={() => k.handleOptimize()}
                  style={{ display:'inline-flex', alignItems:'center', gap:4, padding:'5px 12px', borderRadius:5, fontFamily:'inherit', fontSize:11, fontWeight:700, cursor:'pointer', border:`1px solid ${T.blue}44`, background:T.bg3, color:T.blue }}>
                  ⚡ Optimize
                </button>
              </>
            )}
          </div>

          {/* APPLY — only in write mode */}
          {mode.mode === 'write' && (
            <button onClick={k.handleApply} disabled={k.loading?.apply || (mode.requiresValidation && !k.validation)} style={{
              display:'flex', justifyContent:'center', alignItems:'center', gap:7,
              margin:'0 10px 8px', padding:'8px', borderRadius:6, fontFamily:'inherit',
              fontSize:12, fontWeight:700,
              cursor: (k.loading?.apply || (mode.requiresValidation && !k.validation)) ? 'not-allowed' : 'pointer',
              border:'1px solid #7f1d1d', background:'#180808', color:T.red,
              width:'calc(100% - 20px)',
              opacity: (mode.requiresValidation && !k.validation) ? 0.5 : 1,
            }}>
              ⚡ APPLY CHANGE (LIVE)
              {mode.requiresValidation && !k.validation && (
                <span style={{ fontSize:10, color:T.txt3 }}>— validate first</span>
              )}
            </button>
          )}
        </div>

        {/* Right Panel */}
        <ResizablePanel>
          {/* Confidence bar */}
          <ConfidenceBar
            score={mode.confidenceScore}
            riskLevel={mode.riskLevel}
            analyzing={mode.analyzing}
          />

          {/* Dynamic tab bar – only shows relevant tabs */}
          <DynamicTabBar
            tabs={mode.tabs}
            activeTab={mode.activeTab}
            setActiveTab={mode.setActiveTab}
            mode={mode.mode}
          />

          {/* Panel content */}
          <div style={{ flex:1, overflow:'auto' }}>
            {renderPanel()}
          </div>
        </ResizablePanel>
      </div>

      {/* Status bar */}
      <div style={{
        display:'flex', alignItems:'center', gap:14, padding:'3px 12px',
        background:'#030810', borderTop:`1px solid ${T.border}`, fontSize:10, color:T.txt3, flexShrink:0,
      }}>
        <span>🦊 KITSUNE v6.0</span>
        {connInfo && <span style={{ color:T.green }}>✓ {connInfo.connectionName} ({k.dbType})</span>}
        <span>Confidence: <span style={{
          color: mode.confidenceScore >= 80 ? T.green : mode.confidenceScore >= 55 ? T.amber : T.red
        }}>{mode.confidenceScore}%</span></span>
        {anyLoading && <span style={{ color:T.gold }}>⟳ Processing…</span>}
        <span style={{ marginLeft:'auto' }}>
          Mode: <span style={{
            color: mode.mode === 'read' ? T.green : mode.mode === 'write' ? T.red : T.txt3,
            fontWeight:700,
          }}>
            {mode.mode === 'read' ? '🟢 READ' : mode.mode === 'write' ? '🔴 WRITE' : 'IDLE'}
          </span>
        </span>
        <span>Tab: <span style={{ color:T.txt2 }}>{mode.activeTab}</span></span>
      </div>

      <NotificationToast notifications={k.notifications} />
    </div>
  );
}
