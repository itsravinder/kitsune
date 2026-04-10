// ============================================================
// KITSUNE – SchemaExplorer v2
// Working search filter, server-specific object types,
// fixed text contrast, highlight matches
// ============================================================
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { T } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

// ── Server-specific categories (ONLY show relevant objects) ───
const DB_TREE_CONFIG = {
  SqlServer: [
    { key: 'tables',     label: 'Tables',            icon: '📋', color: '#4a8eff', type: 'table'     },
    { key: 'views',      label: 'Views',             icon: '👁',  color: '#38bdf8', type: 'view'      },
    { key: 'procedures', label: 'Stored Procedures', icon: '⚙',  color: '#a37eff', type: 'procedure'  },
    { key: 'functions',  label: 'Functions',         icon: 'ƒ',   color: '#f59e0b', type: 'function'   },
    { key: 'triggers',   label: 'Triggers',          icon: '⚡',  color: '#e05252', type: 'trigger'    },
    { key: 'indexes',    label: 'Indexes',           icon: '📊',  color: '#6b90b0', type: 'index'      },
    { key: 'schemas',    label: 'Schemas',           icon: '🗂',  color: '#6b90b0', type: 'schema'     },
    { key: 'sequences',  label: 'Sequences',         icon: '🔢',  color: '#6b90b0', type: 'sequence'   },
  ],
  MongoDB: [
    { key: 'collections', label: 'Collections', icon: '🍃', color: '#3dba6e', type: 'collection' },
    { key: 'indexes',     label: 'Indexes',     icon: '📊', color: '#6b90b0', type: 'index'      },
  ],
  MySQL: [
    { key: 'tables',     label: 'Tables',            icon: '📋', color: '#4a8eff', type: 'table'     },
    { key: 'views',      label: 'Views',             icon: '👁',  color: '#38bdf8', type: 'view'      },
    { key: 'procedures', label: 'Stored Procedures', icon: '⚙',  color: '#a37eff', type: 'procedure'  },
    { key: 'functions',  label: 'Functions',         icon: 'ƒ',   color: '#f59e0b', type: 'function'   },
    { key: 'triggers',   label: 'Triggers',          icon: '⚡',  color: '#e05252', type: 'trigger'    },
    { key: 'indexes',    label: 'Indexes',           icon: '📊',  color: '#6b90b0', type: 'index'      },
    { key: 'events',     label: 'Events',            icon: '🗓',  color: '#6b90b0', type: 'event'      },
  ],
  PostgreSQL: [
    { key: 'schemas',    label: 'Schemas',              icon: '🗂',  color: '#4a8eff', type: 'schema'   },
    { key: 'tables',     label: 'Tables',               icon: '📋', color: '#4a8eff', type: 'table'    },
    { key: 'views',      label: 'Views',                icon: '👁',  color: '#38bdf8', type: 'view'     },
    { key: 'mviews',     label: 'Materialized Views',   icon: '👁',  color: '#38bdf8', type: 'mview'    },
    { key: 'procedures', label: 'Stored Procedures',    icon: '⚙',  color: '#a37eff', type: 'procedure' },
    { key: 'functions',  label: 'Functions',            icon: 'ƒ',   color: '#f59e0b', type: 'function'  },
    { key: 'triggers',   label: 'Triggers',             icon: '⚡',  color: '#e05252', type: 'trigger'   },
    { key: 'indexes',    label: 'Indexes',              icon: '📊',  color: '#6b90b0', type: 'index'     },
    { key: 'sequences',  label: 'Sequences',            icon: '🔢',  color: '#6b90b0', type: 'sequence'  },
  ],
};

function HighlightText({ text, query }) {
  if (!query) return <>{text}</>;
  const idx = text.toLowerCase().indexOf(query.toLowerCase());
  if (idx < 0) return <>{text}</>;
  return (
    <>
      {text.slice(0, idx)}
      <mark style={{ background: '#e2a50033', color: T.gold, borderRadius: 2, padding: '0 1px' }}>
        {text.slice(idx, idx + query.length)}
      </mark>
      {text.slice(idx + query.length)}
    </>
  );
}

function Folder({ category, items, expanded, onToggle, onSelect, activeId, search }) {
  const filtered = useMemo(() => {
    if (!search) return items;
    const q = search.toLowerCase();
    return items.filter(i => i.label.toLowerCase().includes(q));
  }, [items, search]);

  if (search && filtered.length === 0) return null;

  return (
    <div>
      <div
        onClick={onToggle}
        style={{
          display: 'flex', alignItems: 'center', gap: 6, padding: '5px 10px',
          cursor: 'pointer', userSelect: 'none', color: T.txt2, fontSize: 11,
          transition: 'background .1s',
        }}
        onMouseEnter={e => e.currentTarget.style.background = T.bg2}
        onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
      >
        <span style={{ color: T.txt3, fontSize: 9, width: 10, textAlign: 'center', flexShrink: 0 }}>
          {expanded ? '▾' : '▸'}
        </span>
        <span style={{ fontSize: 13, flexShrink: 0 }}>{category.icon}</span>
        <span style={{ flex: 1, fontWeight: 600, color: T.txt2, fontSize: 11 }}>{category.label}</span>
        <span style={{
          fontSize: 9, color: T.txt3, background: T.bg3, padding: '1px 6px', borderRadius: 4,
        }}>
          {search ? filtered.length : items.length}
        </span>
      </div>

      {expanded && (
        <div>
          {filtered.length === 0 && (
            <div style={{ padding: '3px 10px 3px 30px', fontSize: 10, color: T.txt3, fontStyle: 'italic' }}>
              {search ? 'No matches' : 'Empty'}
            </div>
          )}
          {filtered.map(item => (
            <div
              key={item.id}
              onClick={() => onSelect(item)}
              style={{
                display: 'flex', alignItems: 'center', gap: 7,
                padding: '4px 10px 4px 28px', cursor: 'pointer', fontSize: 11,
                color: activeId === item.id ? category.color : T.txt,
                background: activeId === item.id ? `${category.color}18` : 'transparent',
                borderLeft: `2px solid ${activeId === item.id ? category.color : 'transparent'}`,
                borderRadius: '0 4px 4px 0', margin: '1px 0',
                transition: 'background .1s',
              }}
              onMouseEnter={e => { if (activeId !== item.id) e.currentTarget.style.background = T.bg2; }}
              onMouseLeave={e => { if (activeId !== item.id) e.currentTarget.style.background = 'transparent'; }}
            >
              <span style={{ fontSize: 12, color: category.color, opacity: .75, flexShrink: 0 }}>
                {category.icon}
              </span>
              <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: T.txt }}>
                <HighlightText text={item.label} query={search} />
              </span>
              {item.schema && item.schema !== 'dbo' && (
                <span style={{ fontSize: 9, color: T.txt3, flexShrink: 0 }}>{item.schema}</span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function SchemaExplorer({ connectionId, dbType = 'SqlServer', onObjectSelect, visible, selectedDatabase }) {
  const [tree,     setTree]     = useState({});
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState('');
  const [search,   setSearch]   = useState('');
  const [expanded, setExpanded] = useState({});
  const [activeId, setActiveId] = useState('');
  const [dbInfo,   setDbInfo]   = useState(null);

  // Fetch actual DB/server info from backend
  useEffect(() => {
    const dbParam = selectedDatabase ? `?db=${encodeURIComponent(selectedDatabase)}` : '';
    fetch(`${BACKEND}/api/db-info${dbParam}`)
      .then(r => r.json())
      .then(d => setDbInfo(d))
      .catch(() => {});
  }, [connectionId, selectedDatabase]);

  // Only use config for connected DB type
  const config = DB_TREE_CONFIG[dbType] || DB_TREE_CONFIG.SqlServer;

  const loadTree = useCallback(async () => {
    if (!connectionId) return;
    setLoading(true); setError(''); setTree({});
    try {
      const dbParam = selectedDatabase ? `?db=${encodeURIComponent(selectedDatabase)}` : '';
      const res  = await fetch(`${BACKEND}/api/connections/${connectionId}/tree${dbParam}`);
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Load failed');

      const map = {};
      const walk = (nodes) => {
        for (const n of (nodes || [])) {
          if (n.type !== 'folder' && n.type !== 'database' && n.type !== 'error') {
            const key = n.type + 's';
            if (!map[key]) map[key] = [];
            if (!map[key].find(x => x.id === n.id))
              map[key].push({ id: n.id, label: n.label, schema: n.schema, type: n.type });
          }
          if (n.children?.length) walk(n.children);
        }
      };
      walk(data.children || []);
      setTree(map);

      // Auto-expand tables and procedures by default
      setExpanded({ tables: true, procedures: true, collections: true });
    } catch (e) {
      setError(e.message);
    } finally { setLoading(false); }
  }, [connectionId]);

  useEffect(() => {
    if (visible && connectionId) loadTree();
  }, [connectionId, visible, loadTree, selectedDatabase]);

  useEffect(() => { setSearch(''); setActiveId(''); }, [dbType]);

  const toggle = (key) => setExpanded(p => ({ ...p, [key]: !p[key] }));

  const handleSelect = async (item) => {
    setActiveId(item.id);
    if (!onObjectSelect) return;

    let definition = '';
    if (['procedure', 'function', 'view', 'trigger'].includes(item.type)) {
      try {
        const name = (item.schema && item.schema !== 'dbo') ? `${item.schema}.${item.label}` : item.label;
        const res  = await fetch(`${BACKEND}/api/connections/${connectionId}/definition?name=${encodeURIComponent(name)}&type=${item.type}`);
        const data = await res.json();
        definition = data.definition || '';
      } catch { /* silent */ }
    }
    onObjectSelect({ ...item, fullName: `${item.schema || 'dbo'}.${item.label}`, definition });
  };

  const totalMatches = useMemo(() => {
    if (!search) return null;
    const q = search.toLowerCase();
    return config.reduce((sum, cat) => sum + (tree[cat.key] || []).filter(i => i.label.toLowerCase().includes(q)).length, 0);
  }, [search, config, tree]);

  if (!visible) return null;

  return (
    <div style={{
      width: 230, background: T.bg1, borderRight: `1px solid ${T.border}`,
      display: 'flex', flexDirection: 'column', flexShrink: 0, overflow: 'hidden',
    }}>
      {/* Header – shows DB type */}
      <div style={{
        padding: '6px 10px', borderBottom: `1px solid ${T.border}`,
        background: '#040c18', display: 'flex', alignItems: 'center', gap: 6,
      }}>
        <span style={{ color: T.txt2, fontSize: 10, fontWeight: 700, flex: 1, letterSpacing: '.06em' }}>
          {dbType.toUpperCase()} EXPLORER
        </span>
        <button onClick={loadTree} disabled={loading} title="Refresh"
          style={{ background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 14 }}>
          {loading ? '⟳' : '↻'}
        </button>
      </div>

      {/* DB Info Banner */}
      {dbInfo?.connected && (
        <div style={{
          padding: '4px 10px', background: '#030a14',
          borderBottom: `1px solid ${T.border}`,
          display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, flexShrink: 0,
        }}>
          <span style={{ color: '#4a8eff', fontWeight: 700 }}>{dbInfo.databaseName}</span>
          <span style={{ color: '#334a60' }}>@</span>
          <span style={{ color: '#6b90b0' }}>{dbInfo.serverName}</span>
        </div>
      )}

      {/* Search – ACTUALLY WORKS */}
      <div style={{ padding: '5px 8px', borderBottom: `1px solid ${T.border}`, flexShrink: 0 }}>
        <div style={{ position: 'relative' }}>
          <span style={{ position: 'absolute', left: 8, top: '50%', transform: 'translateY(-50%)', fontSize: 11, color: T.txt3 }}>🔍</span>
          <input
            style={{
              width: '100%', background: T.bg2, border: `1px solid ${T.border2}`,
              color: T.txt, padding: '5px 26px 5px 24px', borderRadius: 5,
              fontSize: 11, fontFamily: 'inherit', outline: 'none',
            }}
            placeholder={`Filter ${dbType} objects…`}
            value={search}
            onChange={e => {
              setSearch(e.target.value);
              if (e.target.value) {
                const all = {};
                config.forEach(c => { all[c.key] = true; });
                setExpanded(all);
              }
            }}
          />
          {search && (
            <button onClick={() => setSearch('')} style={{
              position: 'absolute', right: 6, top: '50%', transform: 'translateY(-50%)',
              background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 11,
            }}>✕</button>
          )}
        </div>
        {search && totalMatches !== null && (
          <div style={{ fontSize: 10, color: totalMatches > 0 ? T.gold : T.txt3, marginTop: 3, paddingLeft: 2 }}>
            {totalMatches > 0 ? `${totalMatches} match${totalMatches !== 1 ? 'es' : ''}` : 'No matches'}
          </div>
        )}
      </div>

      {/* Tree content */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '4px 0' }}>
        {loading && (
          <div style={{ padding: 20, textAlign: 'center', color: T.txt3, fontSize: 11 }}>
            ⟳ Loading {dbType} schema…
          </div>
        )}
        {error && (
          <div style={{ margin: 8, padding: '8px 10px', background: '#180808', border: `1px solid #7f1d1d`, borderRadius: 5, fontSize: 10, color: '#fca5a5' }}>
            ⚠ {error}
          </div>
        )}
        {!loading && !error && !connectionId && (
          <div style={{ padding: 16, color: T.txt3, fontSize: 11, textAlign: 'center' }}>
            Connect to a database first
          </div>
        )}

        {/* ONLY render categories for connected DB type */}
        {config.map(cat => (
          <Folder
            key={cat.key}
            category={cat}
            items={tree[cat.key] || []}
            expanded={!!expanded[cat.key]}
            onToggle={() => toggle(cat.key)}
            onSelect={handleSelect}
            activeId={activeId}
            search={search}
          />
        ))}
      </div>

      {/* Footer */}
      {!loading && Object.keys(tree).length > 0 && (
        <div style={{
          padding: '4px 10px', borderTop: `1px solid ${T.border}`,
          background: '#040c18', fontSize: 9, color: T.txt3, display: 'flex', gap: 8, flexWrap: 'wrap',
        }}>
          {config.map(cat => {
            const cnt = tree[cat.key]?.length || 0;
            if (!cnt) return null;
            return <span key={cat.key} style={{ color: cat.color }}>{cat.label.split(' ')[0]}: {cnt}</span>;
          })}
        </div>
      )}
    </div>
  );
}
