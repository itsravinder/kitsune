// ============================================================
// KITSUNE – Shared UI Components
// ============================================================
import React from 'react';

export const T = {
  bg0:'#04080f', bg1:'#070f1c', bg2:'#0a1526', bg3:'#0d1c35', bg4:'#111f3a',
  border:'#162840', border2:'#1e3556',
  txt:'#bccfe0', txt2:'#6b90b0', txt3:'#334a60',
  gold:'#e2a500', blue:'#4a8eff', green:'#3dba6e',
  red:'#e05252', purple:'#a37eff', cyan:'#38bdf8', amber:'#f59e0b',
  r: 6,
};

export const STATUS_COLORS = {
  PASS:     { bg:'#0a2a18', c:'#4ade80' },
  WARN:     { bg:'#1a1200', c:'#facc15' },
  FAIL:     { bg:'#180808', c:'#f87171' },
  APPLIED:  { bg:'#0a2a18', c:'#4ade80' },
  BLOCKED:  { bg:'#180808', c:'#f87171' },
  FAILED:   { bg:'#180808', c:'#f87171' },
  HIGH:     { bg:'#2a0808', c:'#f87171' },
  CRITICAL: { bg:'#2a0808', c:'#f87171' },
  MEDIUM:   { bg:'#1a1200', c:'#facc15' },
  LOW:      { bg:'#0a2a18', c:'#4ade80' },
  SUCCESS:  { bg:'#0a2a18', c:'#4ade80' },
  OK:       { bg:'#0a2a18', c:'#4ade80' },
  ERROR:    { bg:'#180808', c:'#f87171' },
  UNKNOWN:  { bg:'#0d1c35', c:'#94a3b8' },
};

export const StatusBadge = ({ status }) => {
  const s = STATUS_COLORS[status] || STATUS_COLORS.UNKNOWN;
  return (
    <span style={{
      background: s.bg, color: s.c, padding: '3px 10px',
      borderRadius: 999, fontSize: 10, fontWeight: 700,
      border: `1px solid ${s.c}33`,
      display: 'inline-flex', alignItems: 'center', gap: 4,
    }}>
      {status}
    </span>
  );
};

export const Pill = ({ label, color = T.blue }) => (
  <span style={{
    background: `${color}18`, color, padding: '2px 8px',
    borderRadius: 999, fontSize: 10, fontWeight: 600,
    border: `1px solid ${color}33`,
  }}>
    {label}
  </span>
);

export const Spinner = () => (
  <span style={{
    display: 'inline-block', width: 13, height: 13,
    border: `2px solid ${T.border2}`,
    borderTop: `2px solid ${T.gold}`,
    borderRadius: '50%', animation: 'kspin .8s linear infinite',
  }} />
);

export const Btn = ({ children, color = T.blue, bg = T.bg3, onClick, disabled, style = {} }) => (
  <button
    onClick={onClick}
    disabled={disabled}
    style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: '5px 12px', borderRadius: T.r,
      fontFamily: 'inherit', fontSize: 11, fontWeight: 700,
      cursor: disabled ? 'not-allowed' : 'pointer',
      border: `1px solid ${color}44`, background: bg, color,
      opacity: disabled ? 0.4 : 1,
      transition: 'opacity .15s, transform .1s',
      whiteSpace: 'nowrap', ...style,
    }}
  >
    {children}
  </button>
);

export const SectionHeader = ({ label, color = T.txt2, right }) => (
  <div style={{
    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
    padding: '7px 12px', borderBottom: `1px solid ${T.border}`,
    background: T.bg1, color, fontSize: 10, fontWeight: 700, letterSpacing: '.07em',
  }}>
    <span>{label}</span>
    {right}
  </div>
);

export const DataTable = ({ columns, rows, maxWidth = 240 }) => (
  <div style={{ overflowX: 'auto', margin: 10 }}>
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
      <thead>
        <tr>
          {columns.map(c => (
            <th key={c} style={{
              background: T.bg3, color: T.blue, padding: '5px 11px',
              textAlign: 'left', fontWeight: 700, letterSpacing: '.06em',
              borderBottom: `1px solid ${T.border}`, whiteSpace: 'nowrap',
            }}>
              {c}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((row, i) => (
          <tr key={i} style={{ background: i % 2 === 0 ? 'transparent' : T.bg1 }}>
            {columns.map(c => (
              <td key={c} style={{
                padding: '5px 11px', borderBottom: `1px solid ${T.bg1}`,
                maxWidth, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
              }}
                title={String(row[c] ?? 'NULL')}
              >
                {row[c] === null || row[c] === undefined
                  ? <span style={{ color: T.txt3 }}>NULL</span>
                  : String(row[c])}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

export const AlertBox = ({ type = 'info', children }) => {
  const styles = {
    info:    { bg: '#071828', border: '#1a4a68', color: '#93c5fd' },
    success: { bg: '#0a1e10', border: '#1a5a2a', color: '#86efac' },
    warning: { bg: '#1a1200', border: '#5a3a00', color: '#fde68a' },
    error:   { bg: '#180808', border: '#6a1a1a', color: '#fca5a5' },
  };
  const s = styles[type] || styles.info;
  return (
    <div style={{
      background: s.bg, border: `1px solid ${s.border}`, color: s.color,
      padding: '8px 12px', borderRadius: 6, margin: 10, fontSize: 11, lineHeight: 1.6,
    }}>
      {children}
    </div>
  );
};

export const EmptyState = ({ icon, message, sub }) => (
  <div style={{ padding: 48, textAlign: 'center', color: T.txt3 }}>
    <div style={{ fontSize: 36, marginBottom: 14 }}>{icon}</div>
    <div style={{ fontSize: 13, color: T.txt2, marginBottom: 6 }}>{message}</div>
    {sub && <div style={{ fontSize: 11 }}>{sub}</div>}
  </div>
);

export const NotificationToast = ({ notifications }) => (
  <div style={{
    position: 'fixed', bottom: 24, right: 24,
    display: 'flex', flexDirection: 'column', gap: 8, zIndex: 1000,
  }}>
    {notifications.map(n => (
      <div key={n.id} style={{
        padding: '10px 16px', borderRadius: 8, fontSize: 12, fontWeight: 600,
        background: n.type === 'error' ? '#2a0808' : n.type === 'success' ? '#0a2a18' : T.bg3,
        color: n.type === 'error' ? '#f87171' : n.type === 'success' ? '#4ade80' : T.txt,
        border: `1px solid ${n.type === 'error' ? '#7f1d1d' : n.type === 'success' ? '#14401e' : T.border}`,
        boxShadow: '0 4px 20px rgba(0,0,0,0.5)',
        animation: 'slideIn .2s ease',
        maxWidth: 320,
      }}>
        {n.msg}
      </div>
    ))}
  </div>
);

export const globalStyles = `
  @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600;700;800&display=swap');
  * { box-sizing: border-box; margin: 0; padding: 0; }
  ::-webkit-scrollbar { width: 5px; height: 5px; }
  ::-webkit-scrollbar-track { background: #04080f; }
  ::-webkit-scrollbar-thumb { background: #162840; border-radius: 3px; }
  @keyframes kspin { to { transform: rotate(360deg); } }
  @keyframes slideIn { from { opacity: 0; transform: translateX(20px); } to { opacity: 1; transform: translateX(0); } }
  textarea::placeholder, input::placeholder { color: #334a60; }
  select option { background: #0a1526; }
  button:hover:not(:disabled) { opacity: .82; transform: translateY(-1px); }
  button:active:not(:disabled) { transform: translateY(0); }
  button:disabled { cursor: not-allowed; }
`;
