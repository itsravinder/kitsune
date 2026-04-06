// ============================================================
// KITSUNE – ObjectSelector Component
// Replaces generic text input with dynamic object dropdowns.
// Fetches object list from /api/objects/list?type=PROCEDURE
// Selecting object auto-loads its definition into SQL editor.
// ============================================================
import React, { useState, useEffect, useCallback } from 'react';
import { T, Spinner } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const OBJECT_TYPES = ['PROCEDURE', 'FUNCTION', 'VIEW', 'TABLE', 'TRIGGER'];

const TYPE_ICON = {
  PROCEDURE: '⚙',
  FUNCTION:  'ƒ',
  VIEW:      '👁',
  TABLE:     '📋',
  TRIGGER:   '⚡',
};

export function ObjectSelector({ objectName, setObjectName, objectType, setObjectType, onDefinitionLoaded }) {
  const [objects,     setObjects]     = useState([]);
  const [loading,     setLoading]     = useState(false);
  const [loadingDef,  setLoadingDef]  = useState(false);
  const [searchText,  setSearchText]  = useState('');
  const [showDropdown, setShowDropdown] = useState(false);

  const loadObjects = useCallback(async (type) => {
    setLoading(true);
    try {
      const res  = await fetch(`${BACKEND}/api/objects/list?type=${type}`);
      const data = await res.json();
      setObjects(data.objects || []);
    } catch {
      setObjects([]);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { loadObjects(objectType); }, [objectType, loadObjects]);

  const handleTypeChange = (type) => {
    setObjectType(type);
    setObjectName('');
    setSearchText('');
    setObjects([]);
  };

  const handleSelect = async (obj) => {
    setObjectName(obj.fullName || obj.name);
    setSearchText(obj.name);
    setShowDropdown(false);

    // Auto-load definition
    if (onDefinitionLoaded) {
      setLoadingDef(true);
      try {
        const res  = await fetch(`${BACKEND}/api/objects/definition?name=${encodeURIComponent(obj.fullName || obj.name)}`);
        const data = await res.json();
        if (data.definition) onDefinitionLoaded(data.definition);
      } catch { /* silent */ }
      finally { setLoadingDef(false); }
    }
  };

  const filtered = objects.filter(o =>
    !searchText || o.name.toLowerCase().includes(searchText.toLowerCase())
  );

  const inp = {
    background: T.bg2, border: `1px solid ${T.bd2}`, color: T.txt,
    padding: '5px 9px', borderRadius: T.r, fontSize: 11,
    fontFamily: 'inherit', outline: 'none',
  };

  return (
    <div style={{ borderTop: `1px solid ${T.border}`, flexShrink: 0 }}>
      <div style={{
        padding: '6px 11px', borderBottom: `1px solid ${T.border}`,
        background: T.bg1, color: T.txt2, fontSize: 10, fontWeight: 700, letterSpacing: '.06em',
        display: 'flex', alignItems: 'center', gap: 8,
      }}>
        <span>⚙ OBJECT CONFIGURATION</span>
        {loadingDef && <Spinner />}
        {loadingDef && <span style={{ color: T.gold, fontSize: 10 }}>Loading definition…</span>}
      </div>

      <div style={{ padding: '8px 11px' }}>
        {/* Object Type dropdown */}
        <div style={{ marginBottom: 8 }}>
          <label style={{ fontSize: 10, color: T.txt3, display: 'block', marginBottom: 4, fontWeight: 600 }}>
            OBJECT TYPE
          </label>
          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
            {OBJECT_TYPES.map(t => (
              <button
                key={t}
                onClick={() => handleTypeChange(t)}
                style={{
                  padding: '4px 9px', borderRadius: 5, fontSize: 10, fontWeight: 700,
                  cursor: 'pointer', fontFamily: 'inherit',
                  background: objectType === t ? T.bg4 : 'transparent',
                  color: objectType === t ? T.gold : T.txt3,
                  border: `1px solid ${objectType === t ? T.gold + '55' : T.border}`,
                  transition: 'all .15s',
                }}
              >
                {TYPE_ICON[t]} {t}
              </button>
            ))}
          </div>
        </div>

        {/* Object Name - dynamic dropdown */}
        <div style={{ position: 'relative' }}>
          <label style={{ fontSize: 10, color: T.txt3, display: 'block', marginBottom: 4, fontWeight: 600 }}>
            OBJECT NAME {loading && <Spinner />}
          </label>
          <div style={{ display: 'flex', gap: 6 }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <input
                style={{ ...inp, width: '100%', paddingRight: 28 }}
                placeholder={`Search ${objectType.toLowerCase()}s…`}
                value={searchText}
                onChange={e => { setSearchText(e.target.value); setShowDropdown(true); setObjectName(e.target.value); }}
                onFocus={() => setShowDropdown(true)}
                onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
              />
              {loading && (
                <span style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)' }}>
                  <Spinner />
                </span>
              )}
            </div>
            <button
              onClick={() => loadObjects(objectType)}
              style={{ ...inp, width: 32, cursor: 'pointer', textAlign: 'center', padding: 0 }}
              title="Refresh list"
            >
              ↻
            </button>
          </div>

          {/* Dropdown */}
          {showDropdown && filtered.length > 0 && (
            <div style={{
              position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 1000,
              background: T.bg2, border: `1px solid ${T.bd2}`, borderRadius: 6,
              maxHeight: 200, overflow: 'auto', marginTop: 2,
              boxShadow: '0 8px 24px rgba(0,0,0,0.5)',
            }}>
              {filtered.slice(0, 50).map((obj, i) => (
                <div
                  key={i}
                  onMouseDown={() => handleSelect(obj)}
                  style={{
                    padding: '6px 10px', cursor: 'pointer', fontSize: 11,
                    borderBottom: `1px solid ${T.border}`,
                    display: 'flex', alignItems: 'center', gap: 8,
                  }}
                  onMouseEnter={e => e.currentTarget.style.background = T.bg3}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                >
                  <span style={{ color: T.txt3, fontSize: 10 }}>{TYPE_ICON[objectType]}</span>
                  <span style={{ color: T.blue }}>{obj.name}</span>
                  <span style={{ color: T.txt3, fontSize: 10, marginLeft: 'auto' }}>{obj.schemaName}</span>
                </div>
              ))}
              {filtered.length > 50 && (
                <div style={{ padding: '5px 10px', fontSize: 10, color: T.txt3 }}>
                  + {filtered.length - 50} more — type to filter
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
