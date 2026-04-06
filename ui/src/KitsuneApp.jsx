// ============================================================
// KITSUNE – Main App v5 (Enhanced)
// New: Connection screen → Schema explorer → Main UI
// Added: dynamic models, dependency map, object selector,
//        resizable panel, schema tree sidebar
// All existing functionality preserved.
// ============================================================
import React, { useState, useEffect } from 'react';
import { ConnectionScreen }    from './components/ConnectionScreen';
import { SchemaExplorer }      from './components/SchemaExplorer';
import { ObjectSelector }      from './components/ObjectSelector';
import { DependencyMap }       from './components/DependencyMap';
import { ResizablePanel }      from './components/ResizablePanel';
import { TopBar }              from './components/TopBar';
import { ExportTab }           from './components/ExportPanel';
import { ScriptRunnerTab }     from './components/ScriptRunner';
import { SqlEditor }           from './components/SqlEditor';
import { T, NotificationToast, globalStyles } from './components/SharedComponents';
import {
  ResultsTab, ValidationTab, HistoryTab, DiffTab,
  RiskTab, ExplainTab, SchemaTab, OptimizerTab,
  MongoTab, ConnectionsTab, AuditTab, SchedulesTab, PreferencesTab,
} from './components/Panels';
import { useKitsune } from './hooks/useKitsune';

const ALL_TABS = [
  { id: 'results',     label: 'Results'      },
  { id: 'validation',  label: 'Validation'   },
  { id: 'history',     label: 'Versions'     },
  { id: 'diff',        label: 'Diff'         },
  { id: 'dependencies',label: 'Dep Map'      },
  { id: 'risk',        label: 'Risk'         },
  { id: 'explain',     label: 'Explain'      },
  { id: 'schema',      label: 'Schema'       },
  { id: 'optimizer',   label: 'Optimizer'    },
  { id: 'mongo',       label: 'MongoDB'      },
  { id: 'script',      label: 'Script Run'   },
  { id: 'export',      label: 'Export'       },
  { id: 'connections', label: 'Connections'  },
  { id: 'audit',       label: 'Audit Log'    },
  { id: 'schedules',   label: 'Schedules'    },
  { id: 'preferences', label: 'Preferences'  },
];

export default function KitsuneApp() {
  // ── Connection state ─────────────────────────────────────
  const [connected,   setConnected]   = useState(false);
  const [connInfo,    setConnInfo]    = useState(null); // { profileId, databaseType, … }
  const [showExplorer,setShowExplorer]= useState(true);

  const k = useKitsune();

  // ── Handle connection success ─────────────────────────────
  const handleConnected = (info) => {
    setConnInfo(info);
    setConnected(true);
    // Set dbType in hook to match connected DB
    if (info.databaseType) k.setDbType(info.databaseType);
  };

  // ── Schema explorer object click → load definition ────────
  const handleSchemaObjectSelect = (obj) => {
    k.setObjectName(obj.fullName || obj.name);
    k.setObjectType(obj.type?.toUpperCase() || 'TABLE');
    if (obj.definition) {
      k.setSqlQuery(obj.definition);
      k.notify?.(`Loaded: ${obj.name}`, 'success');
    }
  };

  // ── Render tab content ────────────────────────────────────
  const renderTab = () => {
    switch (k.activeTab) {
      case 'results':
        return <ResultsTab preview={k.preview} applyResult={k.applyResult} />;
      case 'validation':
        return <ValidationTab validation={k.validation} />;
      case 'history':
        return (
          <HistoryTab
            versions={k.versions} backupResult={k.backupResult} rollbackResult={k.rollbackResult}
            diffVA={k.diffVA} setDiffVA={k.setDiffVA} diffVB={k.diffVB} setDiffVB={k.setDiffVB}
            handleRollback={k.handleRollback} handleDiff={k.handleDiff}
            setSqlQuery={k.setSqlQuery} loading={k.loading}
          />
        );
      case 'diff':
        return <DiffTab diffResult={k.diffResult} />;
      case 'dependencies':
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
        return (
          <MongoTab
            mongoResult={k.mongoResult} mongoDb={k.mongoDb} setMongoDb={k.setMongoDb}
            mongoCollection={k.mongoCollection} setMongoCollection={k.setMongoCollection}
            mongoQuery={k.mongoQuery} setMongoQuery={k.setMongoQuery}
            mongoQueryType={k.mongoQueryType} setMongoQueryType={k.setMongoQueryType}
            handleMongoQuery={k.handleMongoQuery} loading={k.loading}
          />
        );
      case 'script':
        return <ScriptRunnerTab sqlQuery={k.sqlQuery} />;
      case 'export':
        return <ExportTab sqlQuery={k.sqlQuery} />;
      case 'connections':
        return (
          <ConnectionsTab
            connections={k.connections} connForm={k.connForm} setConnForm={k.setConnForm}
            connTestResult={k.connTestResult} handleSaveConn={k.handleSaveConn}
            handleTestConn={k.handleTestConn} loading={k.loading}
            handleLoadConnections={k.handleLoadConnections}
          />
        );
      case 'audit':
        return <AuditTab auditLogs={k.auditLogs} />;
      case 'schedules':
        return (
          <SchedulesTab
            schedules={k.schedules} objectName={k.objectName}
            handleLoadSchedules={k.handleLoadSchedules}
            handleAddSchedule={k.handleAddSchedule} loading={k.loading}
          />
        );
      case 'preferences':
        return (
          <PreferencesTab
            preferences={k.preferences} handleSavePreferences={k.handleSavePreferences}
            loading={k.loading}
          />
        );
      default: return null;
    }
  };

  // ── Show connection screen first ─────────────────────────
  if (!connected) {
    return <ConnectionScreen onConnected={handleConnected} />;
  }

  const anyLoading = Object.values(k.loading).some(Boolean);

  return (
    <div style={{
      display: 'flex', flexDirection: 'column', height: '100vh',
      background: T.bg0, color: T.txt,
      fontFamily: "'JetBrains Mono',monospace", fontSize: 12, overflow: 'hidden',
    }}>
      <style>{globalStyles}</style>

      {/* ── Top Bar ───────────────────────────────────────── */}
      <TopBar
        model={k.model}   setModel={k.setModel}
        models={k.models}
        dbType={k.dbType} setDbType={k.setDbType}
      >
        {/* Connection info pill */}
        {connInfo && (
          <div style={{
            display: 'flex', alignItems: 'center', gap: 8,
            padding: '3px 10px', borderRadius: 5, fontSize: 10,
            background: T.bg3, border: `1px solid ${T.green}44`, color: T.green,
            marginLeft: 10,
          }}>
            <span style={{ width: 6, height: 6, borderRadius: '50%', background: T.green, flexShrink: 0 }} />
            {connInfo.connectionName} · {connInfo.databaseType}
            <button
              onClick={() => setConnected(false)}
              style={{ background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 11, padding: 0, marginLeft: 4 }}
              title="Disconnect"
            >✕</button>
          </div>
        )}

        {/* Toggle schema explorer */}
        <button
          onClick={() => setShowExplorer(e => !e)}
          style={{
            background: showExplorer ? T.bg4 : 'none',
            border: `1px solid ${showExplorer ? T.bd2 : 'transparent'}`,
            color: showExplorer ? T.txt : T.txt3,
            padding: '3px 10px', borderRadius: 4, cursor: 'pointer',
            fontFamily: 'inherit', fontSize: 10, fontWeight: 700,
          }}
          title="Toggle Schema Explorer"
        >
          ⑃ Explorer
        </button>
      </TopBar>

      {/* ── Main Layout ───────────────────────────────────── */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>

        {/* Schema Explorer sidebar */}
        <SchemaExplorer
          connectionId={connInfo?.profileId}
          onObjectSelect={handleSchemaObjectSelect}
          visible={showExplorer}
        />

        {/* Left editor pane */}
        <div style={{
          display: 'flex', flexDirection: 'column',
          width: 390, borderRight: `1px solid ${T.border}`,
          flexShrink: 0, overflow: 'hidden',
        }}>
          {/* NL Query */}
          <div style={{ padding: '9px 11px', borderBottom: `1px solid ${T.border}` }}>
            <div style={{ color: T.txt2, fontSize: 10, fontWeight: 700, marginBottom: 6, letterSpacing: '.07em' }}>
              ◆ NATURAL LANGUAGE QUERY
            </div>
            <textarea
              style={{
                width: '100%', height: 68,
                background: T.bg2, border: `1px solid ${T.bd2}`,
                color: T.txt, padding: '7px 10px', borderRadius: 6,
                fontFamily: 'inherit', fontSize: 11.5, resize: 'none', outline: 'none', lineHeight: 1.6,
              }}
              value={k.nlQuery} onChange={e => k.setNlQuery(e.target.value)}
              placeholder="Describe your query in plain English…"
            />
            <div style={{ display: 'flex', gap: 7, marginTop: 7 }}>
              <button onClick={k.handleGenerate} disabled={k.loading.gen} style={{
                display: 'inline-flex', alignItems: 'center', gap: 5,
                padding: '5px 14px', borderRadius: 6, fontFamily: 'inherit',
                fontSize: 11, fontWeight: 700, cursor: 'pointer',
                border: `1px solid ${T.green}44`, background: '#0a1e10', color: T.green,
                opacity: k.loading.gen ? 0.5 : 1,
              }}>
                ▶ Generate
              </button>
            </div>
            {k.genMeta && <div style={{ marginTop: 5, fontSize: 10, color: T.txt3, fontStyle: 'italic' }}>{k.genMeta}</div>}
          </div>

          {/* SQL Editor */}
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
            <SqlEditor value={k.sqlQuery} onChange={k.setSqlQuery} />
          </div>

          {/* Object Selector (enhanced dynamic dropdown) */}
          <ObjectSelector
            objectName={k.objectName}   setObjectName={k.setObjectName}
            objectType={k.objectType}   setObjectType={k.setObjectType}
            onDefinitionLoaded={(def) => { k.setSqlQuery(def); }}
          />

          {/* Action buttons */}
          <div style={{ display: 'flex', gap: 4, padding: '7px 11px', flexWrap: 'wrap', borderTop: `1px solid ${T.border}` }}>
            {[
              { label: 'Validate',  color: T.green,  bg: '#0a1e10', key: 'validate',  fn: k.handleValidate,  tab: 'validation' },
              { label: 'Preview',   color: T.purple, bg: '#120e2a', key: 'preview',   fn: k.handlePreview,   tab: 'results'    },
              { label: 'Backup',    color: T.gold,   bg: '#1a1200', key: 'backup',    fn: k.handleBackup,    tab: 'history'    },
              { label: 'Versions',  color: T.txt2,   bg: T.bg3,     key: 'versions',  fn: k.handleVersions,  tab: 'history'    },
              { label: 'Risk',      color: T.red,    bg: '#180808', key: 'risk',      fn: k.handleRisk,      tab: 'risk'       },
              { label: 'Explain',   color: T.cyan,   bg: '#071828', key: 'explain',   fn: k.handleExplain,   tab: 'explain'    },
              { label: 'Schema',    color: T.amber,  bg: '#1a1000', key: 'schema',    fn: k.handleSchema,    tab: 'schema'     },
              { label: 'Dep Map',   color: T.purple, bg: '#120e2a', key: 'depmap',    fn: () => k.setActiveTab('dependencies'), tab: 'dependencies' },
              { label: 'Optimize',  color: T.blue,   bg: T.bg3,     key: 'optimize',  fn: k.handleOptimize,  tab: 'optimizer'  },
              { label: 'Audit',     color: T.txt2,   bg: T.bg3,     key: 'audit',     fn: k.handleLoadAudit, tab: 'audit'      },
            ].map(b => (
              <button key={b.key} disabled={k.loading[b.key]}
                onClick={() => { b.fn(); if (b.tab) k.setActiveTab(b.tab); }}
                style={{
                  display: 'inline-flex', alignItems: 'center', gap: 4,
                  padding: '4px 9px', borderRadius: 5, fontFamily: 'inherit',
                  fontSize: 10, fontWeight: 700, cursor: 'pointer',
                  border: `1px solid ${b.color}44`, background: b.bg, color: b.color,
                  opacity: k.loading[b.key] ? 0.4 : 1, whiteSpace: 'nowrap',
                }}
              >
                {b.label}
              </button>
            ))}
          </div>

          {/* Apply button */}
          <button onClick={k.handleApply} disabled={k.loading.apply} style={{
            display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 7,
            margin: '0 11px 9px', padding: '8px', borderRadius: 6,
            fontFamily: 'inherit', fontSize: 12, fontWeight: 700,
            cursor: k.loading.apply ? 'not-allowed' : 'pointer',
            border: '1px solid #7f1d1d', background: '#180808', color: T.red,
            width: 'calc(100% - 22px)', opacity: k.loading.apply ? 0.5 : 1,
          }}>
            ⚡ APPLY CHANGE (LIVE)
          </button>
        </div>

        {/* Right Panel – resizable + detachable */}
        <ResizablePanel>
          {/* Tab bar */}
          <div style={{
            display: 'flex', background: '#040c18',
            borderBottom: `1px solid ${T.border}`, flexShrink: 0, overflowX: 'auto',
          }}>
            {ALL_TABS.map(tab => (
              <div
                key={tab.id}
                onClick={() => k.setActiveTab(tab.id)}
                style={{
                  padding: '8px 12px', fontSize: 10, fontWeight: 700,
                  letterSpacing: '.05em', cursor: 'pointer', whiteSpace: 'nowrap',
                  color: k.activeTab === tab.id ? T.gold : T.txt3,
                  borderBottom: k.activeTab === tab.id ? `2px solid ${T.gold}` : '2px solid transparent',
                  transition: 'color .15s',
                  background: tab.id === 'dependencies' ? '#120e2a33' : 'transparent',
                }}
              >
                {tab.label}
              </div>
            ))}
          </div>

          {/* Tab content */}
          <div style={{ flex: 1, overflow: 'auto' }}>
            {renderTab()}
          </div>
        </ResizablePanel>
      </div>

      {/* Status bar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 14, padding: '3px 12px',
        background: '#030810', borderTop: `1px solid ${T.border}`,
        fontSize: 10, color: T.txt3, flexShrink: 0,
      }}>
        <span>🦊 KITSUNE v5.0</span>
        {connInfo && <span style={{ color: T.green }}>✓ {connInfo.connectionName}</span>}
        <span>API: localhost:5000</span>
        <span>AI: localhost:8000</span>
        {anyLoading && <span style={{ color: T.gold }}>⟳ Processing…</span>}
        <span style={{ marginLeft: 'auto' }}>
          Model: <span style={{ color: T.gold }}>{k.models.find(m => m.id === k.model)?.display_name || k.model}</span>
        </span>
        <span>Tab: <span style={{ color: T.txt2 }}>{k.activeTab}</span></span>
      </div>

      <NotificationToast notifications={k.notifications} />
    </div>
  );
}
