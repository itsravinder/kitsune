// ============================================================
// KITSUNE – ResizablePanel Component
// Wraps the right panel with drag-to-resize handle.
// Detach button opens content in a floating overlay window.
// ============================================================
import React, { useState, useRef, useCallback, useEffect } from 'react';
import { T } from './SharedComponents';

const MIN_WIDTH = 380;
const MAX_WIDTH_RATIO = 0.85; // max 85% of viewport

export function ResizablePanel({ children, defaultWidth = null }) {
  const [width,    setWidth]    = useState(defaultWidth); // null = flex:1
  const [detached, setDetached] = useState(false);
  const [dragging, setDragging] = useState(false);
  const dragRef    = useRef(null);
  const startXRef  = useRef(0);
  const startWRef  = useRef(0);

  // Mouse drag on the resize handle (left edge of panel)
  const onMouseDown = useCallback((e) => {
    e.preventDefault();
    setDragging(true);
    startXRef.current = e.clientX;
    startWRef.current = width || (window.innerWidth * 0.5);
  }, [width]);

  useEffect(() => {
    if (!dragging) return;

    const onMove = (e) => {
      const delta   = startXRef.current - e.clientX; // drag left = wider
      const newW    = Math.min(
        Math.max(MIN_WIDTH, startWRef.current + delta),
        window.innerWidth * MAX_WIDTH_RATIO
      );
      setWidth(newW);
    };
    const onUp = () => setDragging(false);

    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); };
  }, [dragging]);

  // Keyboard shortcut: Ctrl+Shift+D to detach/reattach
  useEffect(() => {
    const handler = (e) => {
      if (e.ctrlKey && e.shiftKey && e.key === 'D') setDetached(d => !d);
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  const resetWidth = () => setWidth(null);

  // ── Floating/detached window ──────────────────────────────
  if (detached) {
    return (
      <>
        {/* Placeholder in original position */}
        <div style={{
          flex: 1, background: T.bg0, display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: T.txt3, fontSize: 12, flexDirection: 'column', gap: 10,
        }}>
          <span style={{ fontSize: 24 }}>⊞</span>
          <span>Panel detached</span>
          <button
            onClick={() => setDetached(false)}
            style={{
              padding: '6px 14px', borderRadius: 6, background: T.bg3,
              border: `1px solid ${T.bd2}`, color: T.txt2, cursor: 'pointer',
              fontFamily: 'inherit', fontSize: 11,
            }}
          >
            ↩ Reattach (Ctrl+Shift+D)
          </button>
        </div>

        {/* Floating window */}
        <FloatingWindow onClose={() => setDetached(false)}>
          {children}
        </FloatingWindow>
      </>
    );
  }

  // ── Normal mode with resize handle ───────────────────────
  return (
    <div style={{ display: 'flex', flex: width ? 'none' : 1, width: width || undefined, position: 'relative', overflow: 'hidden' }}>
      {/* Resize handle – 6px wide drag zone on left edge */}
      <div
        onMouseDown={onMouseDown}
        style={{
          position: 'absolute', left: 0, top: 0, bottom: 0, width: 6,
          cursor: 'col-resize', zIndex: 10,
          background: dragging ? T.gold : 'transparent',
          transition: 'background .1s',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}
        title="Drag to resize"
      >
        {dragging && <div style={{ width: 2, height: 40, background: T.gold, borderRadius: 2 }} />}
      </div>

      {/* Panel content */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', marginLeft: 6 }}>
        {/* Detach / reset bar */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'flex-end',
          padding: '2px 8px', gap: 6, background: '#040c18', borderBottom: `1px solid ${T.border}`, flexShrink: 0,
        }}>
          {width && (
            <button onClick={resetWidth} title="Reset width"
              style={{ background: 'none', border: 'none', color: T.txt3, cursor: 'pointer', fontSize: 11, padding: '0 4px' }}>
              ⊟ {Math.round(width)}px
            </button>
          )}
          <button
            onClick={() => setDetached(true)}
            title="Detach panel (Ctrl+Shift+D)"
            style={{ background: 'none', border: `1px solid ${T.border}`, color: T.txt3, cursor: 'pointer', fontSize: 10, padding: '1px 7px', borderRadius: 4, fontFamily: 'inherit' }}
          >
            ⊞ Detach
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

// ── Floating draggable window ─────────────────────────────────
function FloatingWindow({ children, onClose }) {
  const [pos,  setPos]  = useState({ x: 80, y: 60 });
  const [size, setSize] = useState({ w: 860, h: 580 });
  const [dragWin, setDragWin] = useState(false);
  const [resizeWin, setResizeWin] = useState(false);
  const startRef = useRef({ x: 0, y: 0, px: 0, py: 0, w: 0, h: 0 });

  const onTitleDown = (e) => {
    setDragWin(true);
    startRef.current = { ...startRef.current, x: e.clientX, y: e.clientY, px: pos.x, py: pos.y };
    e.preventDefault();
  };
  const onResizeDown = (e) => {
    setResizeWin(true);
    startRef.current = { ...startRef.current, x: e.clientX, y: e.clientY, w: size.w, h: size.h };
    e.preventDefault();
  };

  useEffect(() => {
    if (!dragWin && !resizeWin) return;
    const onMove = (e) => {
      if (dragWin) {
        setPos({ x: startRef.current.px + (e.clientX - startRef.current.x), y: startRef.current.py + (e.clientY - startRef.current.y) });
      } else if (resizeWin) {
        setSize({
          w: Math.max(500, startRef.current.w + (e.clientX - startRef.current.x)),
          h: Math.max(350, startRef.current.h + (e.clientY - startRef.current.y)),
        });
      }
    };
    const onUp = () => { setDragWin(false); setResizeWin(false); };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); };
  }, [dragWin, resizeWin]);

  return (
    <div style={{
      position: 'fixed', left: pos.x, top: pos.y,
      width: size.w, height: size.h, zIndex: 9000,
      background: T.bg0, border: `1px solid ${T.gold}55`,
      borderRadius: 8, overflow: 'hidden', display: 'flex', flexDirection: 'column',
      boxShadow: '0 24px 64px rgba(0,0,0,0.8)',
      userSelect: dragWin || resizeWin ? 'none' : 'auto',
    }}>
      {/* Title bar */}
      <div
        onMouseDown={onTitleDown}
        style={{
          display: 'flex', alignItems: 'center', gap: 10, padding: '8px 14px',
          background: '#040c18', borderBottom: `1px solid ${T.border}`,
          cursor: 'grab', flexShrink: 0,
        }}
      >
        <span style={{ color: T.gold, fontSize: 13, fontWeight: 700, fontFamily: "'JetBrains Mono',monospace" }}>🦊 KITSUNE – Results Panel</span>
        <span style={{ color: T.txt3, fontSize: 10 }}>⊞ Detached · drag to move · corner to resize</span>
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 6 }}>
          <button onClick={onClose} style={{
            background: '#2a0808', border: `1px solid #7f1d1d`, color: '#f87171',
            width: 24, height: 24, borderRadius: 4, cursor: 'pointer',
            fontSize: 12, display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>✕</button>
        </div>
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflow: 'auto' }}>
        {children}
      </div>

      {/* Resize handle – bottom-right corner */}
      <div
        onMouseDown={onResizeDown}
        style={{
          position: 'absolute', right: 0, bottom: 0, width: 16, height: 16,
          cursor: 'nwse-resize', zIndex: 1,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}
      >
        <svg width="10" height="10" viewBox="0 0 10 10" fill={T.txt3}>
          <path d="M0 10L10 0M4 10L10 4M8 10L10 8"/>
        </svg>
      </div>
    </div>
  );
}
