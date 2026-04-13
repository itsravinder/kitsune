// ============================================================
// KITSUNE – TableDepMap Component
// Visual dependency map for tables used in a generated query.
// Matches the style of DependencyMap (image 4):
//   - Target node (base table, highlighted)
//   - Depth 1: directly joined tables
//   - Depth 2: tables joined via depth-1 tables
//   - FK relationship labels on each edge
// ============================================================
import React, { useMemo } from 'react';
import { T } from './SharedComponents';

const TYPE_COLOR = {
  table:     T.blue   || '#4a8eff',
  view:      T.cyan   || '#38bdf8',
  procedure: '#a37eff',
  function:  '#f59e0b',
};

// ── Single node card (matches image 4 style) ─────────────────
function DepNode({ name, type = 'table', isTarget = false, fkLabel = '' }) {
  const color = TYPE_COLOR[type] || T.blue;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
      {/* Connector line */}
      {!isTarget && (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', marginLeft: 8 }}>
          <div style={{ width: 1, height: 10, background: '#334a60' }} />
          <div style={{ color: '#334a60', fontSize: 12 }}>└</div>
        </div>
      )}

      {/* Node pill */}
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 8,
        padding: '7px 14px', borderRadius: 7,
        background: isTarget ? '#1a1000' : T.bg3 || '#0d1c35',
        border: `1px solid ${isTarget ? T.gold || '#e2a500' : color + '88'}`,
        minWidth: 160,
      }}>
        <span style={{ fontSize: 14 }}>
          {type === 'table' ? '🗄' : type === 'view' ? '👁' : type === 'procedure' ? '⚙' : 'ƒ'}
        </span>
        <span style={{ color: isTarget ? T.gold || '#e2a500' : color, fontWeight: 700, fontSize: 12 }}>
          {name}
        </span>
        <span style={{
          fontSize: 9, fontWeight: 700, letterSpacing: '.06em', padding: '1px 5px',
          borderRadius: 3, background: color + '22', color,
        }}>
          {type.toUpperCase()}
        </span>
        {isTarget && (
          <span style={{ fontSize: 9, color: T.txt3 || '#334a60', marginLeft: 4 }}>(base)</span>
        )}
      </div>

      {/* FK label */}
      {fkLabel && (
        <div style={{
          fontSize: 10, color: T.txt3 || '#334a60', padding: '2px 8px',
          background: T.bg2 || '#07101e', border: `1px solid ${T.border || '#162840'}`,
          borderRadius: 4, maxWidth: 280,
        }}>
          🔗 ON {fkLabel}
        </div>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────
export function TableDepMap({ tablesUsed = [], schemaData = {}, deepmapText = '' }) {
  // Build FK graph from schema state
  // schemaData may have a raw deepmap text or structured data
  const fkMap = useMemo(() => {
    // Try to extract FK relationships from deepmapText if structured data not available
    const map = {}; // tableName → [{ toTable, onClause }]

    // Parse "JOIN ON dbo.Orders.CustomerID = dbo.Customers.CustomerID"
    if (deepmapText) {
      const joinRegex = /(?:JOIN ON|ON)\s+([\w.]+)\s*=\s*([\w.]+)/gi;
      let m;
      while ((m = joinRegex.exec(deepmapText)) !== null) {
        const left  = m[1].split('.').slice(-2).join('.');  // Table.Column
        const right = m[2].split('.').slice(-2).join('.');
        const leftTable  = m[1].split('.').slice(-2)[0];
        const rightTable = m[2].split('.').slice(-2)[0];
        if (leftTable && rightTable && leftTable !== rightTable) {
          if (!map[leftTable])  map[leftTable]  = [];
          if (!map[rightTable]) map[rightTable] = [];
          map[leftTable].push({ toTable: rightTable, onClause: `${left} = ${right}` });
        }
      }
    }
    return map;
  }, [deepmapText]);

  if (!tablesUsed.length) {
    return (
      <div style={{ padding: 24, color: T.txt3 || '#334a60', fontSize: 12, textAlign: 'center' }}>
        Generate a query first to see the table dependency map.
      </div>
    );
  }

  const baseTable   = tablesUsed[0];
  const joinedTables = tablesUsed.slice(1);

  // Compute FK labels for each joined table
  const getFkLabel = (joinedTable) => {
    const baseJoins   = fkMap[baseTable]   || [];
    const joinedJoins = fkMap[joinedTable] || [];
    const fk1 = baseJoins.find(j => j.toTable === joinedTable);
    const fk2 = joinedJoins.find(j => j.toTable === baseTable);
    return fk1?.onClause || fk2?.onClause || '';
  };

  // Group joined tables by FK depth
  // Depth 1: directly joined to base table
  // Depth 2: joined to a depth-1 table
  const depth1 = joinedTables.filter(t => {
    const bj = fkMap[baseTable] || [];
    const tj = fkMap[t] || [];
    return bj.some(j => j.toTable === t) || tj.some(j => j.toTable === baseTable);
  });
  const depth2 = joinedTables.filter(t => !depth1.includes(t));

  return (
    <div style={{ padding: 14, fontFamily: "'JetBrains Mono',monospace" }}>
      {/* Header */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16,
        padding: '8px 12px', background: T.bg1 || '#07101e',
        border: `1px solid ${T.border || '#162840'}`, borderRadius: 7,
        fontSize: 11,
      }}>
        <span style={{ color: T.txt2 || '#6b90b0', fontWeight: 700 }}>QUERY DEPENDENCY MAP</span>
        <span style={{ color: T.txt3 || '#334a60' }}>
          Base: <span style={{ color: T.gold || '#e2a500' }}>{baseTable}</span>
        </span>
        <span style={{ color: T.txt3 || '#334a60' }}>
          Dependents: <span style={{ color: T.txt || '#bccfe0' }}>{joinedTables.length}</span>
        </span>
        <span style={{ color: T.txt3 || '#334a60' }}>
          Max Depth: <span style={{ color: T.txt || '#bccfe0' }}>{depth2.length > 0 ? 2 : depth1.length > 0 ? 1 : 0}</span>
        </span>
      </div>

      {/* Base (target) node */}
      <DepNode name={baseTable} type="table" isTarget={true} />

      {/* Depth 1 nodes */}
      {depth1.length > 0 && (
        <>
          <div style={{ marginLeft: 8, marginTop: 4, marginBottom: 4, fontSize: 10, color: T.txt3 || '#334a60', fontWeight: 700, letterSpacing: '.06em' }}>
            Depth 1
          </div>
          {depth1.map(t => (
            <div key={t} style={{ marginLeft: 12 }}>
              <DepNode name={t} type="table" fkLabel={getFkLabel(t)} />
            </div>
          ))}
        </>
      )}

      {/* Depth 2 nodes */}
      {depth2.length > 0 && (
        <>
          <div style={{ marginLeft: 8, marginTop: 4, marginBottom: 4, fontSize: 10, color: T.txt3 || '#334a60', fontWeight: 700, letterSpacing: '.06em' }}>
            Depth 2
          </div>
          {depth2.map(t => (
            <div key={t} style={{ marginLeft: 24 }}>
              <DepNode name={t} type="table" fkLabel={getFkLabel(t)} />
            </div>
          ))}
        </>
      )}

      {/* FK relationship summary */}
      {Object.keys(fkMap).length > 0 && (
        <div style={{
          marginTop: 16, padding: '10px 14px',
          background: T.bg2 || '#07101e', border: `1px solid ${T.border || '#162840'}`,
          borderRadius: 7,
        }}>
          <div style={{ fontSize: 10, color: T.txt3 || '#334a60', fontWeight: 700, letterSpacing: '.06em', marginBottom: 8 }}>
            FK RELATIONSHIPS
          </div>
          {Object.entries(fkMap).map(([tbl, joins]) =>
            joins.map((j, i) => (
              <div key={`${tbl}-${i}`} style={{ fontSize: 10, color: T.txt2 || '#6b90b0', marginBottom: 3 }}>
                <span style={{ color: T.blue || '#4a8eff' }}>{tbl}</span>
                <span style={{ color: T.txt3 || '#334a60' }}> ──FK──▶ </span>
                <span style={{ color: T.blue || '#4a8eff' }}>{j.toTable}</span>
                <span style={{ color: T.txt3 || '#334a60' }}> ON {j.onClause}</span>
              </div>
            ))
          )}
        </div>
      )}

      {/* Raw deepmap text fallback */}
      {deepmapText && !Object.keys(fkMap).length && (
        <div style={{
          marginTop: 12, padding: '10px 14px',
          background: T.bg2 || '#07101e', border: `1px solid ${T.border || '#162840'}`,
          borderRadius: 7,
        }}>
          <div style={{ fontSize: 10, color: T.txt3 || '#334a60', fontWeight: 700, marginBottom: 6 }}>
            DATA FLOW
          </div>
          <pre style={{ fontSize: 10, color: T.txt || '#bccfe0', whiteSpace: 'pre-wrap', margin: 0 }}>
            {deepmapText}
          </pre>
        </div>
      )}
    </div>
  );
}
