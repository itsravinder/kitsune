// ============================================================
// KITSUNE – Main App (Final Complete Version)
// All 15 tabs, all components, all hooks
// ============================================================
import React from 'react';
import { useKitsune }          from './hooks/useKitsune';
import { TopBar }              from './components/TopBar';
import { LeftPane }            from './components/LeftPane';
import { SqlEditor }           from './components/SqlEditor';
import { ExportTab }           from './components/ExportPanel';
import { ScriptRunnerTab }     from './components/ScriptRunner';
import { T, NotificationToast, globalStyles } from './components/SharedComponents';
import {
  ResultsTab, ValidationTab, HistoryTab, DiffTab,
  RiskTab, ExplainTab, SchemaTab, OptimizerTab,
  MongoTab, ConnectionsTab, AuditTab, SchedulesTab, PreferencesTab,
} from './components/Panels';

const ALL_TABS = [
  { id: 'results',    label: 'Results'     },
  { id: 'validation', label: 'Validation'  },
  { id: 'history',    label: 'Versions'    },
  { id: 'diff',       label: 'Diff'        },
  { id: 'risk',       label: 'Risk'        },
  { id: 'explain',    label: 'Explain'     },
  { id: 'schema',     label: 'Schema'      },
  { id: 'optimizer',  label: 'Optimizer'   },
  { id: 'mongo',      label: 'MongoDB'     },
  { id: 'script',     label: 'Script Run'  },
  { id: 'export',     label: 'Export'      },
  { id: 'connections',label: 'Connections' },
  { id: 'audit',      label: 'Audit Log'   },
  { id: 'schedules',  label: 'Schedules'   },
  { id: 'preferences',label: 'Preferences' },
];

export default function KitsuneApp() {
  const k = useKitsune();

  const renderTab = () => {
    switch (k.activeTab) {
      case 'results':
        return <ResultsTab preview={k.preview} applyResult={k.applyResult} />;
      case 'validation':
        return <ValidationTab validation={k.validation} />;
      case 'history':
        return (
          <HistoryTab
            versions={k.versions}
            backupResult={k.backupResult}
            rollbackResult={k.rollbackResult}
            diffVA={k.diffVA} setDiffVA={k.setDiffVA}
            diffVB={k.diffVB} setDiffVB={k.setDiffVB}
            handleRollback={k.handleRollback}
            handleDiff={k.handleDiff}
            setSqlQuery={k.setSqlQuery}
            loading={k.loading}
          />
        );
      case 'diff':
        return <DiffTab diffResult={k.diffResult} />;
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
            mongoResult={k.mongoResult}
            mongoDb={k.mongoDb}             setMongoDb={k.setMongoDb}
            mongoCollection={k.mongoCollection} setMongoCollection={k.setMongoCollection}
            mongoQuery={k.mongoQuery}       setMongoQuery={k.setMongoQuery}
            mongoQueryType={k.mongoQueryType} setMongoQueryType={k.setMongoQueryType}
            handleMongoQuery={k.handleMongoQuery}
            loading={k.loading}
          />
        );
      case 'script':
        return <ScriptRunnerTab sqlQuery={k.sqlQuery} />;
      case 'export':
        return <ExportTab sqlQuery={k.sqlQuery} />;
      case 'connections':
        return (
          <ConnectionsTab
            connections={k.connections}
            connForm={k.connForm}         setConnForm={k.setConnForm}
            connTestResult={k.connTestResult}
            handleSaveConn={k.handleSaveConn}
            handleTestConn={k.handleTestConn}
            loading={k.loading}
            handleLoadConnections={k.handleLoadConnections}
          />
        );
      case 'audit':
        return <AuditTab auditLogs={k.auditLogs} />;
      case 'schedules':
        return (
          <SchedulesTab
            schedules={k.schedules}
            objectName={k.objectName}
            handleLoadSchedules={k.handleLoadSchedules}
            handleAddSchedule={k.handleAddSchedule}
            loading={k.loading}
          />
        );
      case 'preferences':
        return (
          <PreferencesTab
            preferences={k.preferences}
            handleSavePreferences={k.handleSavePreferences}
            loading={k.loading}
          />
        );
      default:
        return null;
    }
  };

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
        model={k.model}    setModel={k.setModel}
        models={k.models}
        dbType={k.dbType}  setDbType={k.setDbType}
      />

      {/* ── Main Layout ───────────────────────────────────── */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>

        {/* Left Pane */}
        <div style={{
          display: 'flex', flexDirection: 'column',
          width: 420, borderRight: `1px solid ${T.border}`,
          flexShrink: 0, overflow: 'hidden',
        }}>
          {/* NL Input */}
          <div style={{ padding: '9px 12px', borderBottom: `1px solid ${T.border}` }}>
            <div style={{ color: T.txt2, fontSize: 10, fontWeight: 700, marginBottom: 6, letterSpacing: '.07em' }}>
              ◆ NATURAL LANGUAGE QUERY
            </div>
            <textarea
              style={{
                width: '100%', height: 72,
                background: T.bg2, border: `1px solid ${T.border2}`,
                color: T.txt, padding: '7px 10px', borderRadius: 6,
                fontFamily: 'inherit', fontSize: 11.5, resize: 'none', outline: 'none', lineHeight: 1.6,
              }}
              value={k.nlQuery}
              onChange={e => k.setNlQuery(e.target.value)}
              placeholder="Describe your query in plain English…"
            />
            <div style={{ display: 'flex', gap: 7, marginTop: 7, alignItems: 'center' }}>
              <button
                onClick={k.handleGenerate}
                disabled={k.loading.gen}
                style={{
                  display: 'inline-flex', alignItems: 'center', gap: 5,
                  padding: '5px 14px', borderRadius: 6, fontFamily: 'inherit',
                  fontSize: 11, fontWeight: 700, cursor: 'pointer',
                  border: `1px solid ${T.green}44`, background: '#0a1e10', color: T.green,
                  opacity: k.loading.gen ? 0.5 : 1,
                }}
              >
                {k.loading.gen
                  ? <span style={{ display:'inline-block',width:12,height:12,border:`2px solid #162840`,borderTop:`2px solid ${T.gold}`,borderRadius:'50%',animation:'kspin .8s linear infinite'}}/>
                  : '▶'} Generate
              </button>
            </div>
            {k.genMeta && (
              <div style={{ marginTop: 5, fontSize: 10, color: T.txt3, fontStyle: 'italic' }}>
                {k.genMeta}
              </div>
            )}
          </div>

          {/* SQL Editor – using component */}
          <div style={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
            <SqlEditor value={k.sqlQuery} onChange={k.setSqlQuery} />
          </div>

          {/* Object Config + Actions */}
          <LeftPane
            nlQuery={k.nlQuery}         setNlQuery={k.setNlQuery}
            sqlQuery={k.sqlQuery}       setSqlQuery={k.setSqlQuery}
            objectName={k.objectName}   setObjectName={k.setObjectName}
            objectType={k.objectType}   setObjectType={k.setObjectType}
            dbType={k.dbType}           setDbType={k.setDbType}
            loading={k.loading}         genMeta={k.genMeta}
            handleGenerate={k.handleGenerate}
            handleValidate={k.handleValidate}
            handlePreview={k.handlePreview}
            handleBackup={k.handleBackup}
            handleVersions={k.handleVersions}
            handleRollback={k.handleRollback}
            handleApply={k.handleApply}
            handleRisk={k.handleRisk}
            handleExplain={k.handleExplain}
            handleSchema={k.handleSchema}
            handleLoadAudit={k.handleLoadAudit}
            handleOptimize={k.handleOptimize}
            handleMongoQuery={k.handleMongoQuery}
          />
        </div>

        {/* ── Right Pane ──────────────────────────────────── */}
        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>

          {/* Tab bar */}
          <div style={{
            display: 'flex', background: T.bg1,
            borderBottom: `1px solid ${T.border}`,
            flexShrink: 0, overflowX: 'auto',
          }}>
            {ALL_TABS.map(tab => (
              <div
                key={tab.id}
                onClick={() => k.setActiveTab(tab.id)}
                style={{
                  padding: '8px 13px', fontSize: 10, fontWeight: 700,
                  letterSpacing: '.05em', cursor: 'pointer', whiteSpace: 'nowrap',
                  color: k.activeTab === tab.id ? T.gold : T.txt3,
                  borderBottom: k.activeTab === tab.id
                    ? `2px solid ${T.gold}` : '2px solid transparent',
                  transition: 'color .15s',
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
        </div>
      </div>

      {/* ── Status Bar ────────────────────────────────────── */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 14,
        padding: '3px 12px', background: T.bg1,
        borderTop: `1px solid ${T.border}`,
        fontSize: 10, color: T.txt3, flexShrink: 0,
      }}>
        <span>🦊 KITSUNE v2.0.0</span>
        <span>Backend: localhost:5000</span>
        <span>AI: localhost:8000</span>
        {anyLoading && (
          <span style={{
            color: T.gold,
            display: 'inline-flex', alignItems: 'center', gap: 5,
          }}>
            <span style={{ display:'inline-block',width:10,height:10,border:`2px solid #162840`,borderTop:`2px solid ${T.gold}`,borderRadius:'50%',animation:'kspin .8s linear infinite'}}/>
            Processing…
          </span>
        )}
        <span style={{ marginLeft: 'auto' }}>
          Model:{' '}
          <span style={{ color: T.gold }}>
            {k.models.find(m => m.id === k.model)?.display_name || k.model}
          </span>
        </span>
        <span style={{ color: T.txt3 }}>Tab: {k.activeTab}</span>
      </div>

      <NotificationToast notifications={k.notifications} />
    </div>
  );
}
