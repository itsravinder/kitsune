// ============================================================
// KITSUNE – DependencyMap Component
// Shows dependent and referenced objects for the current object
// Toggle between list view and simple tree diagram
// ============================================================
import React, { useState, useCallback } from 'react';
import { T, Btn, Spinner, EmptyState, AlertBox } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const TYPE_COLOR = {
  SQL_STORED_PROCEDURE: T.purple,
  VIEW:                 T.cyan,
  USER_TABLE:           T.blue,
  SQL_INLINE_TABLE_VALUED_FUNCTION: T.amber,
  SQL_SCALAR_FUNCTION:  T.amber,
  DEFAULT_CONSTRAINT:   T.txt3,
};

export function DependencyMap({ objectName, objectType }) {
  const [deps,    setDeps]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');
  const [view,    setView]    = useState('list'); // list | tree

  const loadDeps = useCallback(async () => {
    if (!objectName) return;
    setLoading(true); setError('');
    try {
      const res  = await fetch(`${BACKEND}/api/validate/dependencies/${encodeURIComponent(objectName)}`);
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Failed');
      setDeps(data);
    } catch (e) {
      setError(e.message);
    } finally { setLoading(false); }
  }, [objectName]);

  // Group by depth level
  const grouped = deps?.dependencies?.reduce((acc, d) => {
    const key = d.depth || 1;
    if (!acc[key]) acc[key] = [];
    acc[key].push(d);
    return acc;
  }, {}) || {};

  return (
    <div>
      {/* Toolbar */}
      <div style={{
        display: 'flex', gap: 8, padding: '8px 12px',
        background: T.bg1, borderBottom: `1px solid ${T.border}`,
        alignItems: 'center', flexWrap: 'wrap',
      }}>
        <span style={{ color: T.txt2, fontSize: 11, fontWeight: 700 }}>DEPENDENCY MAP</span>
        {objectName && (
          <span style={{ fontSize: 10, color: T.txt3 }}>
            for <span style={{ color: T.purple }}>{objectName}</span>
          </span>
        )}
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 6, alignItems: 'center' }}>
          {/* View toggle */}
          <div style={{ display: 'flex', border: `1px solid ${T.border}`, borderRadius: 5, overflow: 'hidden' }}>
            {['list', 'tree'].map(v => (
              <button
                key={v}
                onClick={() => setView(v)}
                style={{
                  padding: '3px 10px', fontSize: 10, fontWeight: 700, cursor: 'pointer',
                  fontFamily: 'inherit', border: 'none',
                  background: view === v ? T.bg4 : 'transparent',
                  color: view === v ? T.gold : T.txt3,
                }}
              >
                {v === 'list' ? '☰ List' : '⑃ Tree'}
              </button>
            ))}
          </div>
          <Btn color={T.purple} bg="#120e2a" onClick={loadDeps} disabled={loading || !objectName}>
            {loading ? <Spinner /> : '⑃'} Map Dependencies
          </Btn>
        </div>
      </div>

      {/* Content */}
      {!objectName && (
        <EmptyState icon="⑃" message="No object selected"
          sub="Set an object name in the Object Configuration section, then click Map Dependencies" />
      )}
      {error && <AlertBox type="error">{error}</AlertBox>}

      {deps && (
        <>
          {/* Summary bar */}
          <div style={{
            display: 'flex', gap: 16, padding: '8px 14px',
            background: T.bg1, borderBottom: `1px solid ${T.border}`,
            fontSize: 11, flexWrap: 'wrap',
          }}>
            <span style={{ color: T.txt2 }}>
              Object: <span style={{ color: T.purple }}>{deps.objectName}</span>
            </span>
            <span style={{ color: T.txt2 }}>
              Dependents: <span style={{ color: T.red }}>{deps.dependencyCount}</span>
            </span>
            <span style={{ color: T.txt2 }}>
              Max Depth: <span style={{ color: T.gold }}>{Math.max(...(deps.dependencies?.map(d => d.depth) || [0]))}</span>
            </span>
          </div>

          {deps.dependencyCount === 0 && (
            <div style={{ padding: '20px 14px', fontSize: 12, color: T.txt3 }}>
              ✓ No dependent objects found. This object can be safely modified.
            </div>
          )}

          {view === 'list' && deps.dependencyCount > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
                <thead>
                  <tr>
                    {['Object Name', 'Type', 'Schema', 'Depth', 'Dependency Path'].map(h => (
                      <th key={h} style={{
                        background: T.bg3, color: T.blue, padding: '5px 11px',
                        textAlign: 'left', fontWeight: 700, borderBottom: `1px solid ${T.border}`,
                        whiteSpace: 'nowrap',
                      }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {deps.dependencies?.map((dep, i) => (
                    <tr key={i} style={{ background: i % 2 === 0 ? 'transparent' : T.bg1 }}>
                      <td style={{ padding: '5px 11px', color: T.blue, fontWeight: 700 }}>{dep.affectedName}</td>
                      <td style={{ padding: '5px 11px' }}>
                        <span style={{
                          padding: '2px 7px', borderRadius: 4, fontSize: 10,
                          background: `${TYPE_COLOR[dep.affectedType] || T.txt3}22`,
                          color: TYPE_COLOR[dep.affectedType] || T.txt3,
                          border: `1px solid ${TYPE_COLOR[dep.affectedType] || T.txt3}44`,
                        }}>
                          {dep.affectedType?.replace('SQL_', '').replace('USER_', '').replace('_', ' ')}
                        </span>
                      </td>
                      <td style={{ padding: '5px 11px', color: T.txt3 }}>{dep.schemaName}</td>
                      <td style={{ padding: '5px 11px', color: T.gold }}>{dep.depth}</td>
                      <td style={{ padding: '5px 11px', color: T.txt3, fontSize: 10 }}>{dep.dependencyPath}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {view === 'tree' && deps.dependencyCount > 0 && (
            <div style={{ padding: 16, overflow: 'auto' }}>
              {/* Root node */}
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 4 }}>
                <div style={{
                  display: 'inline-flex', alignItems: 'center', gap: 8,
                  padding: '8px 14px', borderRadius: 7,
                  background: T.bg3, border: `2px solid ${T.gold}`,
                  color: T.gold, fontWeight: 700, fontSize: 13, marginBottom: 12,
                }}>
                  ⚙ {deps.objectName}
                  <span style={{ fontSize: 10, color: T.txt3, fontWeight: 400 }}>(target)</span>
                </div>

                {Object.entries(grouped).map(([depth, nodes]) => (
                  <div key={depth} style={{ marginLeft: Number(depth) * 24, display: 'flex', flexDirection: 'column', gap: 6 }}>
                    <div style={{ fontSize: 10, color: T.txt3, marginBottom: 2, marginLeft: 14 }}>
                      Depth {depth}
                    </div>
                    {nodes.map((node, i) => (
                      <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <span style={{ color: T.txt3, fontSize: 12 }}>└─</span>
                        <div style={{
                          display: 'inline-flex', alignItems: 'center', gap: 8,
                          padding: '6px 12px', borderRadius: 6,
                          background: `${TYPE_COLOR[node.affectedType] || T.txt3}18`,
                          border: `1px solid ${TYPE_COLOR[node.affectedType] || T.txt3}44`,
                          color: TYPE_COLOR[node.affectedType] || T.txt,
                          fontSize: 11,
                        }}>
                          {node.affectedName}
                          <span style={{ fontSize: 9, color: T.txt3 }}>
                            {node.affectedType?.replace('SQL_', '').replace('USER_', '')}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {!deps && !loading && objectName && (
        <EmptyState icon="⑃"
          message="Ready to map dependencies"
          sub={`Click "Map Dependencies" to analyze what depends on ${objectName}`}
        />
      )}
    </div>
  );
}
