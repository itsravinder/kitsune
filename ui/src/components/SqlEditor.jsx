// ============================================================
// KITSUNE – SqlEditor Component
// Textarea with line numbers, keyword highlighting overlay,
// keyboard shortcuts, and copy/clear buttons
// ============================================================
import React, { useRef, useCallback, useState } from 'react';
import { T, Btn } from './SharedComponents';

// SQL keywords for the status bar hint
const KEYWORDS = [
  'SELECT','FROM','WHERE','JOIN','LEFT','RIGHT','INNER','OUTER','ON',
  'GROUP BY','ORDER BY','HAVING','INSERT','UPDATE','DELETE','CREATE',
  'ALTER','DROP','EXEC','EXECUTE','WITH','UNION','DISTINCT','TOP',
  'BEGIN','END','TRAN','TRANSACTION','COMMIT','ROLLBACK','IF','ELSE',
  'PROCEDURE','FUNCTION','VIEW','TRIGGER','INDEX','TABLE',
];

function detectKeywords(sql) {
  const upper = sql.toUpperCase();
  return KEYWORDS.filter(k => upper.includes(k));
}

function countLines(text) {
  return (text.match(/\n/g) || []).length + 1;
}

export function SqlEditor({ value, onChange, height = 180 }) {
  const taRef      = useRef(null);
  const [scroll,   setScroll]   = useState(0);
  const lineCount  = countLines(value);
  const detected   = detectKeywords(value);

  // Sync line gutter scroll with textarea scroll
  const handleScroll = useCallback(() => {
    if (taRef.current) setScroll(taRef.current.scrollTop);
  }, []);

  // Tab key inserts 2 spaces
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Tab') {
      e.preventDefault();
      const ta  = taRef.current;
      const s   = ta.selectionStart;
      const end = ta.selectionEnd;
      const newVal = value.substring(0, s) + '  ' + value.substring(end);
      onChange(newVal);
      // Restore cursor after React re-render
      requestAnimationFrame(() => {
        ta.selectionStart = ta.selectionEnd = s + 2;
      });
    }
  }, [value, onChange]);

  const handleCopy  = () => navigator.clipboard?.writeText(value);
  const handleClear = () => onChange('');
  const handleFormat = () => {
    // Basic SQL formatter: uppercase keywords, normalize whitespace
    let formatted = value;
    KEYWORDS.forEach(kw => {
      const re = new RegExp(`\\b${kw}\\b`, 'gi');
      formatted = formatted.replace(re, kw);
    });
    onChange(formatted);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6,
        padding: '5px 12px', background: T.bg1,
        borderBottom: `1px solid ${T.border}`, flexShrink: 0,
      }}>
        <span style={{ color: T.txt2, fontSize: 10, fontWeight: 700, flex: 1 }}>
          ▣ SQL / QUERY EDITOR
        </span>
        <Btn color={T.txt3} bg="transparent" onClick={handleFormat}
          style={{ padding: '2px 8px', fontSize: 10, border: 'none' }}>
          ≋ Format
        </Btn>
        <Btn color={T.txt3} bg="transparent" onClick={handleCopy}
          style={{ padding: '2px 8px', fontSize: 10, border: 'none' }}>
          ⎘ Copy
        </Btn>
        <Btn color={T.txt3} bg="transparent" onClick={handleClear}
          style={{ padding: '2px 8px', fontSize: 10, border: 'none' }}>
          ✕ Clear
        </Btn>
      </div>

      {/* Editor body: line numbers + textarea */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden', position: 'relative' }}>
        {/* Line numbers gutter */}
        <div
          style={{
            width: 40, background: '#040c18', borderRight: `1px solid ${T.border}`,
            padding: '9px 0', overflow: 'hidden', flexShrink: 0, userSelect: 'none',
          }}
        >
          <div style={{ transform: `translateY(-${scroll}px)` }}>
            {Array.from({ length: lineCount }, (_, i) => (
              <div key={i} style={{
                height: '1.7em', lineHeight: '1.7em',
                textAlign: 'right', paddingRight: 8,
                fontSize: 11, color: T.txt3,
                fontFamily: "'JetBrains Mono',monospace",
              }}>
                {i + 1}
              </div>
            ))}
          </div>
        </div>

        {/* Textarea */}
        <textarea
          ref={taRef}
          value={value}
          onChange={e => onChange(e.target.value)}
          onScroll={handleScroll}
          onKeyDown={handleKeyDown}
          spellCheck={false}
          placeholder={"-- Generated SQL appears here, or type your own…\n-- Preview wraps in BEGIN TRAN/ROLLBACK (zero persistence)"}
          style={{
            flex: 1, background: '#050e1c', border: 'none',
            color: T.txt, padding: '9px 12px',
            fontFamily: "'JetBrains Mono',monospace",
            fontSize: 11.5, resize: 'none', outline: 'none',
            lineHeight: '1.7em', overflowY: 'auto',
          }}
        />
      </div>

      {/* Status bar */}
      <div style={{
        display: 'flex', gap: 12, padding: '3px 12px',
        background: '#030810', borderTop: `1px solid ${T.border}`,
        fontSize: 10, color: T.txt3, flexShrink: 0, flexWrap: 'wrap',
      }}>
        <span>Lines: {lineCount}</span>
        <span>Chars: {value.length}</span>
        {detected.slice(0, 6).map(k => (
          <span key={k} style={{ color: T.purple }}>{k}</span>
        ))}
        <span style={{ marginLeft: 'auto' }}>Tab = 2 spaces</span>
      </div>
    </div>
  );
}
