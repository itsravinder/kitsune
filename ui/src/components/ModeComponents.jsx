// ============================================================
// KITSUNE – Mode UI Components
// ModeBadge, ConfidenceBar, SafetyWarning, TabBar
// ============================================================
import React from 'react';
import { T } from './SharedComponents';

const MODE_STYLES = {
  read:    { bg: '#0a2a18', border: '#1a5a2a88', text: '#4ade80', dot: '#3dba6e' },
  write:   { bg: '#1a1200', border: '#5a3a0088', text: '#facc15', dot: '#e2a500' },
  red:     { bg: '#180808', border: '#7f1d1d88', text: '#f87171', dot: '#e05252' },
  unknown: { bg: T.bg3,     border: T.border,    text: T.txt2,    dot: T.txt3   },
};

// ── Mode Badge ────────────────────────────────────────────────
export function ModeBadge({ mode, modeLabel, modeColor, primaryStmt }) {
  if (!modeLabel) return null;
  const key     = modeColor === 'red' ? 'red' : mode === 'read' ? 'read' : mode === 'write' ? 'write' : 'unknown';
  const styles  = MODE_STYLES[key];
  const icon    = mode === 'read' ? '🟢' : mode === 'write' ? '🔴' : '⚪';

  return (
    <div style={{
      display: 'inline-flex', alignItems: 'center', gap: 7,
      padding: '4px 12px', borderRadius: 6,
      background: styles.bg, border: `1px solid ${styles.border}`,
      fontSize: 11, fontWeight: 700,
    }}>
      <span style={{ fontSize: 12 }}>{icon}</span>
      <span style={{ color: styles.text }}>{modeLabel}</span>
      {primaryStmt && (
        <span style={{
          color: T.txt3, fontSize: 10, background: T.bg1,
          padding: '1px 6px', borderRadius: 4, border: `1px solid ${T.border}`,
        }}>
          {primaryStmt}
        </span>
      )}
    </div>
  );
}

// ── Confidence Bar ────────────────────────────────────────────
export function ConfidenceBar({ score, riskLevel, analyzing }) {
  const color = score >= 80 ? '#3dba6e'
               : score >= 55 ? '#f59e0b'
               : '#e05252';

  const riskColor = {
    LOW: '#3dba6e', MEDIUM: '#f59e0b', HIGH: '#e05252', CRITICAL: '#e05252'
  }[riskLevel] || T.txt3;

  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10, padding: '5px 12px',
      background: T.bg1, borderBottom: `1px solid ${T.border}`, flexShrink: 0,
    }}>
      <span style={{ fontSize: 10, color: T.txt3, whiteSpace: 'nowrap' }}>
        {analyzing ? '⟳ Analyzing…' : 'Confidence'}
      </span>

      {/* Bar */}
      <div style={{ flex: 1, height: 4, background: T.bg3, borderRadius: 2, overflow: 'hidden' }}>
        <div style={{
          height: '100%', width: `${score}%`, background: color, borderRadius: 2,
          transition: 'width .4s ease, background .3s',
        }} />
      </div>

      {/* Score */}
      <span style={{ fontSize: 11, fontWeight: 700, color, minWidth: 36, whiteSpace: 'nowrap' }}>
        {score}%
      </span>

      {/* Risk badge */}
      <span style={{
        fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4,
        background: `${riskColor}18`, color: riskColor,
        border: `1px solid ${riskColor}44`,
      }}>
        {riskLevel}
      </span>
    </div>
  );
}

// ── Safety Warning Banner ──────────────────────────────────────
export function SafetyWarning({ warnings, syntaxErrors, safetyMsg, mode, requiresValidation }) {
  const items = [
    ...syntaxErrors.map(e => ({ text: e, type: 'error' })),
    ...warnings.map(w => ({ text: w, type: 'warn' })),
  ];

  if (!safetyMsg && items.length === 0 && !requiresValidation) return null;

  const isCrit = warnings.some(w => w.toLowerCase().includes('all rows')) || syntaxErrors.length > 0;

  return (
    <div style={{
      margin: '8px 10px', borderRadius: 7, overflow: 'hidden',
      border: `1px solid ${isCrit ? '#7f1d1d' : '#5a3a0044'}`,
      background: isCrit ? '#180808' : '#1a1200',
    }}>
      {/* Header */}
      {safetyMsg && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px',
          borderBottom: items.length > 0 ? `1px solid ${isCrit ? '#7f1d1d44' : '#5a3a0044'}` : 'none',
        }}>
          <span style={{ fontSize: 14 }}>{isCrit ? '🚨' : '⚠'}</span>
          <span style={{ fontSize: 11, color: isCrit ? '#f87171' : '#facc15', fontWeight: 700 }}>
            {safetyMsg}
          </span>
        </div>
      )}

      {/* Items */}
      {items.length > 0 && (
        <div style={{ padding: '6px 12px' }}>
          {items.map((item, i) => (
            <div key={i} style={{
              fontSize: 11, padding: '3px 0',
              color: item.type === 'error' ? '#fca5a5' : '#fde68a',
              paddingLeft: 6, borderLeft: `2px solid ${item.type === 'error' ? '#e05252' : '#e2a500'}`,
              marginBottom: 2,
            }}>
              • {item.text}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Dynamic Tab Bar ────────────────────────────────────────────
export function DynamicTabBar({ tabs, activeTab, setActiveTab, mode }) {
  const primaryTabs = tabs.slice(0, mode === 'read' ? 6 : 8);

  return (
    <div style={{
      display: 'flex', background: '#040c18',
      borderBottom: `1px solid ${T.border}`, flexShrink: 0,
      overflowX: 'auto',
    }}>
      {tabs.map((tab, i) => {
        const isActive  = activeTab === tab.id;
        const isPrimary = i < (mode === 'read' ? 6 : 8);
        return (
          <div
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              display: 'flex', alignItems: 'center', gap: 5,
              padding: '7px 12px', fontSize: 10, fontWeight: 700,
              letterSpacing: '.05em', cursor: 'pointer', whiteSpace: 'nowrap',
              color: isActive ? tab.color : isPrimary ? T.txt2 : T.txt3,
              borderBottom: isActive ? `2px solid ${tab.color}` : '2px solid transparent',
              background: isActive ? `${tab.color}0d` : 'transparent',
              opacity: isPrimary ? 1 : 0.7,
              transition: 'color .15s, background .15s',
            }}
          >
            <span style={{ fontSize: 11 }}>{tab.icon}</span>
            {tab.label}
          </div>
        );
      })}
    </div>
  );
}

// ── AI Loading Indicator ──────────────────────────────────────
export function AILoading({ visible, message = 'AI is analyzing your query…' }) {
  if (!visible) return null;
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12, padding: '16px 20px',
      background: T.bg1, borderBottom: `1px solid ${T.border}`,
    }}>
      <div style={{
        width: 18, height: 18, borderRadius: '50%',
        border: `2px solid ${T.border2}`, borderTop: `2px solid ${T.gold}`,
        animation: 'kspin .8s linear infinite', flexShrink: 0,
      }} />
      <div>
        <div style={{ fontSize: 12, color: T.txt, fontWeight: 600 }}>{message}</div>
        <div style={{ fontSize: 10, color: T.txt3, marginTop: 2 }}>
          This usually takes 1–3 seconds
        </div>
      </div>
    </div>
  );
}

// ── Read Mode Result with AI explanation above ────────────────
export function ReadModeResults({ preview, explanation, loadingExplain }) {
  return (
    <div>
      {/* AI explanation above results */}
      {(explanation || loadingExplain) && (
        <div style={{
          margin: '10px 10px 0', padding: '10px 14px',
          background: '#071828', border: `1px solid ${T.cyan}33`,
          borderRadius: 7, borderLeft: `3px solid ${T.cyan}`,
        }}>
          <div style={{ fontSize: 10, color: T.cyan, fontWeight: 700, marginBottom: 5, letterSpacing: '.06em' }}>
            💡 AI EXPLANATION
          </div>
          {loadingExplain ? (
            <div style={{ fontSize: 11, color: T.txt3 }}>Generating explanation…</div>
          ) : (
            <div style={{ fontSize: 11, color: T.txt, lineHeight: 1.75 }}>
              {explanation}
            </div>
          )}
        </div>
      )}

      {/* Result stats */}
      {preview && (
        <>
          <div style={{
            display: 'flex', gap: 14, padding: '8px 14px',
            background: T.bg1, margin: '10px 10px 0', borderRadius: '6px 6px 0 0',
            border: `1px solid ${T.border}`, borderBottom: 'none', fontSize: 11,
          }}>
            <span style={{ color: preview.success ? T.green : T.red }}>
              {preview.success ? '✓ Safe Query' : '✗ Error'}
            </span>
            <span style={{ color: T.txt2 }}>Rows: <b style={{ color: T.txt }}>{preview.rowCount}</b></span>
            <span style={{ color: T.txt2 }}>Time: <b style={{ color: T.txt }}>
              {preview.executionMs?.toFixed(0)}ms
            </b></span>
            <span style={{
              marginLeft: 'auto', fontSize: 10, padding: '1px 8px', borderRadius: 4,
              background: '#0a2a18', color: T.green, border: `1px solid #1a5a2a44`,
            }}>
              🟢 READ ONLY
            </span>
          </div>

          {/* Errors */}
          {preview.errors?.length > 0 && (
            <div style={{
              margin: '0 10px', padding: '8px 12px', background: '#180808',
              border: `1px solid #7f1d1d`, borderTop: 'none', fontSize: 11, color: '#fca5a5',
            }}>
              {preview.errors.map((e, i) => <div key={i}>{e}</div>)}
            </div>
          )}

          {/* Grid */}
          {preview.resultSet?.length > 0 && (
            <div style={{ margin: '0 10px 10px', overflowX: 'auto', border: `1px solid ${T.border}`, borderTop: 'none', borderRadius: '0 0 6px 6px' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
                <thead>
                  <tr>
                    {preview.columns.map(c => (
                      <th key={c} style={{
                        background: T.bg3, color: T.blue, padding: '6px 11px',
                        textAlign: 'left', fontWeight: 700, letterSpacing: '.06em',
                        borderBottom: `1px solid ${T.border}`, whiteSpace: 'nowrap',
                      }}>{c}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.resultSet.map((row, i) => (
                    <tr key={i} style={{ background: i % 2 === 0 ? T.bg2 : T.bg1 }}>
                      {preview.columns.map(c => (
                        <td key={c} style={{
                          padding: '5px 11px', borderBottom: `1px solid ${T.bg0}`,
                          color: row[c] === null ? T.txt3 : T.txt,
                          maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                        }} title={String(row[c] ?? 'NULL')}>
                          {row[c] === null ? <i style={{ color: T.txt3 }}>NULL</i> : String(row[c])}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {preview.resultSet?.length === 0 && preview.success && (
            <div style={{ padding: '20px 14px', color: T.txt3, fontSize: 12, margin: '0 10px 10px' }}>
              Query executed. No rows returned.
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ── Empty state ───────────────────────────────────────────────
export function EmptyQueryState({ dbType }) {
  const examples = {
    SqlServer: [
      'Show top 10 customers by order value',
      'Find all procedures modified this week',
      'List tables with more than 1000 rows',
    ],
    MongoDB: [
      'Find all active users in Mumbai',
      'Show orders from last 30 days',
    ],
    MySQL: ['Show all tables with row counts'],
    PostgreSQL: ['List all schemas and their sizes'],
  };
  const prompts = examples[dbType] || examples.SqlServer;

  return (
    <div style={{ padding: '40px 24px', textAlign: 'center' }}>
      <div style={{ fontSize: 36, marginBottom: 14 }}>🦊</div>
      <div style={{ fontSize: 15, fontWeight: 700, color: T.txt, marginBottom: 8 }}>
        Ask Kitsune anything about your database
      </div>
      <div style={{ fontSize: 12, color: T.txt3, marginBottom: 24 }}>
        Type a query or describe what you want in plain English
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'center' }}>
        {prompts.map((p, i) => (
          <div key={i} style={{
            padding: '8px 16px', background: T.bg2, border: `1px solid ${T.border}`,
            borderRadius: 6, fontSize: 12, color: T.txt2, cursor: 'default',
            maxWidth: 320, textAlign: 'left',
          }}>
            "{p}"
          </div>
        ))}
      </div>
    </div>
  );
}
