// ============================================================
// KITSUNE – SchemaExplorer Component
// SSMS-style tree: Databases → Tables / Views / Procs / Funcs
// Click object → loads definition into SQL editor
// ============================================================
import React, { useState, useEffect, useCallback } from 'react';
import { T } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const ICONS = {
  database:   '🗄',
  folder:     '📁',
  table:      '📋',
  view:       '👁',
  procedure:  '⚙',
  function:   'ƒ',
  collection: '🍃',
  error:      '⚠',
};

const COLORS = {
  database:   T.gold,
  folder:     T.txt2,
  table:      T.blue,
  view:       T.cyan,
  procedure:  T.purple,
  function:   T.amber,
  collection: '#3dba6e',
  error:      T.red,
};

function TreeNode({ node, depth = 0, onSelect, activeId }) {
  const [expanded, setExpanded] = useState(depth < 1);
  const [loading,  setLoading]  = useState(false);
  const isFolder  = node.type === 'folder' || node.type === 'database';
  const isLeaf    = !isFolder && node.children?.length === 0;
  const isActive  = node.id === activeId;

  const toggle = () => { if (isFolder || node.hasChildren) setExpanded(e => !e); };

  const handleClick = () => {
    toggle();
    if (isLeaf || (!isFolder && node.type !== 'error')) {
      onSelect(node);
    }
  };

  return (
    <div>
      <div
        onClick={handleClick}
        style={{
          display: 'flex', alignItems: 'center', gap: 5,
          padding: `4px ${8 + depth * 14}px 4px ${8 + depth * 14}px`,
          cursor: 'pointer', borderRadius: 4, margin: '1px 4px',
          background: isActive ? '#0d1c35' : 'transparent',
          border: `1px solid ${isActive ? T.gold + '44' : 'transparent'}`,
          transition: 'background .1s',
          fontSize: 11,
        }}
        onMouseEnter={e => !isActive && (e.currentTarget.style.background = '#07101e')}
        onMouseLeave={e => !isActive && (e.currentTarget.style.background = 'transparent')}
      >
        {/* Expand arrow */}
        <span style={{ width: 12, color: T.txt3, fontSize: 9, flexShrink: 0 }}>
          {(isFolder || node.hasChildren) ? (expanded ? '▾' : '▸') : ''}
        </span>

        {/* Icon */}
        <span style={{ fontSize: 12, flexShrink: 0 }}>
          {ICONS[node.type] || '○'}
        </span>

        {/* Label */}
        <span style={{ color: COLORS[node.type] || T.txt, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>
          {node.label}
        </span>

        {/* Count badge for folders */}
        {isFolder && node.children?.length > 0 && (
          <span style={{ fontSize: 9, color: T.txt3, background: T.bg3, padding: '1px 5px', borderRadius: 4 }}>
            {node.children.length}
          </span>
        )}
      </div>

      {/* Children */}
      {expanded && node.children?.length > 0 && (
        <div>
          {node.children.map(child => (
            <TreeNode
              key={child.id}
              node={child}
              depth={depth + 1}
              onSelect={onSelect}
              activeId={activeId}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export function SchemaExplorer({ connectionId, onObjectSelect, visible }) {
  const [tree,    setTree]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');
  const [search,  setSearch]  = useState('');
  const [activeId,setActiveId]= useState('');

  const loadTree = useCallback(async () => {
    if (!connectionId) return;
    setLoading(true); setError('');
    try {
      const res = await fetch(`${BACKEND}/api/connections/${connectionId}/tree`);
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Failed to load schema');
      setTree(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [connectionId]);

  useEffect(() => { if (visible && connectionId) loadTree(); }, [connectionId, visible, loadTree]);

  const handleSelect = async (node) => {
    setActiveId(node.id);
    if (!onObjectSelect) return;

    // For leaf objects, fetch definition
    let definition = node.definition;
    if (!definition && node.type !== 'folder' && node.type !== 'database' && node.type !== 'collection') {
      try {
        const res  = await fetch(`${BACKEND}/api/connections/${connectionId}/definition?name=${encodeURIComponent(node.schema+'.'+node.label)}&type=${node.type}`);
        const data = await res.json();
        definition = data.definition;
      } catch { /* silent */ }
    }

    onObjectSelect({
      name:       node.label,
      fullName:   node.schema ? `${node.schema}.${node.label}` : node.label,
      type:       node.type,
      schema:     node.schema || 'dbo',
      definition: definition || '',
    });
  };

  // Filter tree by search
  const filterTree = (node) => {
    if (!search) return node;
    const q = search.toLowerCase();
    if (node.label.toLowerCase().includes(q)) return node;
    if (node.children?.length) {
      const filtered = node.children.map(filterTree).filter(Boolean);
      if (filtered.length) return { ...node, children: filtered };
    }
    return null;
  };

  const filteredTree = tree ? filterTree(tree) : null;

  if (!visible) return null;

  return (
    <div style={{
      width: 240, background: T.bg1, borderRight: `1px solid ${T.border}`,
      display: 'flex', flexDirection: 'column', flexShrink: 0, overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '7px 10px', borderBottom: `1px solid ${T.border}`,
        background: '#040c18', display: 'flex', alignItems: 'center', gap: 7,
      }}>
        <span style={{ color: T.txt2, fontSize: 10, fontWeight: 700, flex: 1, letterSpacing: '.06em' }}>
          SCHEMA EXPLORER
        </span>
        <button
          onClick={loadTree}
          disabled={loading}
          style={{ background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 14, padding: 0 }}
          title="Refresh"
        >
          {loading ? '⟳' : '↻'}
        </button>
      </div>

      {/* Search */}
      <div style={{ padding: '6px 8px', borderBottom: `1px solid ${T.border}` }}>
        <input
          style={{
            width: '100%', background: T.bg2, border: `1px solid ${T.bd2}`,
            color: T.txt, padding: '4px 8px', borderRadius: 5, fontSize: 11,
            fontFamily: 'inherit', outline: 'none',
          }}
          placeholder="🔍 Filter objects…"
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </div>

      {/* Tree content */}
      <div style={{ flex: 1, overflow: 'auto', padding: '4px 0' }}>
        {loading && (
          <div style={{ padding: 20, textAlign: 'center', color: T.txt3, fontSize: 11 }}>
            ⟳ Loading schema…
          </div>
        )}
        {error && (
          <div style={{ padding: 10, fontSize: 11, color: T.red, background: '#180808', margin: 8, borderRadius: 5 }}>
            ⚠ {error}
          </div>
        )}
        {!loading && !error && !connectionId && (
          <div style={{ padding: 16, color: T.txt3, fontSize: 11, textAlign: 'center' }}>
            Connect to a database to see schema
          </div>
        )}
        {filteredTree && (
          <TreeNode node={filteredTree} depth={0} onSelect={handleSelect} activeId={activeId} />
        )}
      </div>
    </div>
  );
}
