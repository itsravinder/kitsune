// ============================================================
// KITSUNE – TopBar Component
// Logo, model selector, DB type toggle, status indicator
// ============================================================
import React from 'react';
import { T } from './SharedComponents';

export function TopBar({ model, setModel, models, dbType, setDbType }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '0 16px', height: 46,
      background: T.bg1, borderBottom: `1px solid ${T.border}`,
      flexShrink: 0,
    }}>
      {/* Logo */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        color: T.gold, fontWeight: 800, fontSize: 15, letterSpacing: '.1em',
      }}>
        <span style={{ fontSize: 20 }}>🦊</span>
        KITSUNE
        <span style={{
          color: T.txt3, fontSize: 9, fontWeight: 400, letterSpacing: '.05em',
        }}>
          AI DATABASE INTELLIGENCE
        </span>
      </div>

      {/* Version badge */}
      <span style={{
        background: T.bg3, color: T.blue,
        padding: '2px 8px', borderRadius: 4, fontSize: 10,
        border: `1px solid ${T.blue}33`,
      }}>
        v2.0
      </span>

      {/* DB Type toggle */}
      <div style={{ display: 'flex', gap: 4, marginLeft: 8 }}>
        {['SqlServer', 'MongoDB'].map(t => (
          <button
            key={t}
            onClick={() => setDbType(t)}
            style={{
              padding: '3px 10px', borderRadius: 4,
              fontSize: 10, fontWeight: 700, cursor: 'pointer',
              fontFamily: 'inherit',
              background: dbType === t ? T.bg4 : 'transparent',
              color: dbType === t ? T.txt : T.txt3,
              border: `1px solid ${dbType === t ? T.border2 : 'transparent'}`,
              transition: 'all .15s',
            }}
          >
            {t === 'SqlServer' ? '🗄 SQL Server' : '🍃 MongoDB'}
          </button>
        ))}
      </div>

      {/* Spacer */}
      <div style={{ flex: 1 }} />

      {/* Model selector */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ color: T.txt3, fontSize: 10, letterSpacing: '.06em' }}>MODEL</span>
        <select
          value={model}
          onChange={e => setModel(e.target.value)}
          style={{
            background: T.bg2, border: `1px solid ${T.gold}55`,
            color: T.gold, padding: '4px 8px', borderRadius: 5,
            fontSize: 11, fontFamily: 'inherit', cursor: 'pointer',
            outline: 'none', minWidth: 190,
          }}
        >
          {models.map(m => (
            <option key={m.id} value={m.id}>
              {m.display_name}{m.available === false ? ' (offline)' : ''}
            </option>
          ))}
        </select>

        {/* Live indicator */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <span style={{
            width: 7, height: 7, borderRadius: '50%',
            background: T.green, flexShrink: 0,
            boxShadow: `0 0 6px ${T.green}88`,
          }} title="Connected" />
          <span style={{ color: T.txt3, fontSize: 10 }}>LIVE</span>
        </div>
      </div>
    </div>
  );
}
