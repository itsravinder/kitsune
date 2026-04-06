// ============================================================
// KITSUNE – ExportPanel Component
// Tab panel for downloading query results as CSV/JSON/TSV
// ============================================================
import React, { useState } from 'react';
import { T, Btn, Spinner, AlertBox, EmptyState } from './SharedComponents';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

export function ExportTab({ sqlQuery }) {
  const [format,   setFormat]   = useState('csv');
  const [maxRows,  setMaxRows]  = useState(10000);
  const [fileName, setFileName] = useState('kitsune-export');
  const [headers,  setHeaders]  = useState(true);
  const [loading,  setLoading]  = useState(false);
  const [result,   setResult]   = useState(null);
  const [error,    setError]    = useState('');

  const handleExport = async () => {
    if (!sqlQuery?.trim()) { setError('No SQL query to export. Write a query first.'); return; }
    setLoading(true); setError(''); setResult(null);
    try {
      const res = await fetch(`${BACKEND}/api/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sqlQuery, format, maxRows, fileName, includeHeaders: headers }),
      });
      if (!res.ok) {
        const e = await res.json();
        throw new Error(e.error || `HTTP ${res.status}`);
      }
      // Trigger browser download
      const blob = await res.blob();
      const url  = URL.createObjectURL(blob);
      const a    = document.createElement('a');
      a.href     = url;
      a.download = `${fileName}.${format}`;
      document.body.appendChild(a);
      a.click();
      URL.revokeObjectURL(url);
      document.body.removeChild(a);
      setResult({ format, fileName, size: blob.size });
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const inputStyle = {
    background: T.bg2, border: `1px solid ${T.border2}`, color: T.txt,
    padding: '5px 9px', borderRadius: T.r, fontSize: 11,
    fontFamily: 'inherit', outline: 'none',
  };

  return (
    <div style={{ padding: 16, maxWidth: 560 }}>
      <div style={{ color: T.txt2, fontWeight: 700, fontSize: 10, marginBottom: 16, letterSpacing: '.07em' }}>
        📥 EXPORT QUERY RESULTS
      </div>

      {!sqlQuery?.trim() && (
        <EmptyState icon="📥" message="No query to export"
          sub="Write a SQL query in the editor, then export here" />
      )}

      {sqlQuery?.trim() && (
        <>
          <div style={{
            background: T.bg2, border: `1px solid ${T.border}`, borderRadius: 7,
            padding: '8px 12px', marginBottom: 16, fontSize: 11, color: T.txt3,
            fontFamily: "'JetBrains Mono',monospace", maxHeight: 80, overflow: 'auto',
          }}>
            {sqlQuery.slice(0, 300)}{sqlQuery.length > 300 ? '…' : ''}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 16 }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 10, color: T.txt3 }}>
              Format
              <select style={{ ...inputStyle, width: '100%' }} value={format} onChange={e => setFormat(e.target.value)}>
                <option value="csv">CSV (Comma Separated)</option>
                <option value="tsv">TSV (Tab Separated)</option>
                <option value="json">JSON Array</option>
              </select>
            </label>

            <label style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 10, color: T.txt3 }}>
              Max Rows
              <input style={{ ...inputStyle, width: '100%' }} type="number" min={1} max={100000} step={1000}
                value={maxRows} onChange={e => setMaxRows(Number(e.target.value))} />
            </label>

            <label style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 10, color: T.txt3 }}>
              File Name
              <input style={{ ...inputStyle, width: '100%' }} type="text"
                value={fileName} onChange={e => setFileName(e.target.value)} />
            </label>

            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11, color: T.txt, cursor: 'pointer', marginTop: 20 }}>
              <input type="checkbox" checked={headers} onChange={e => setHeaders(e.target.checked)} />
              Include column headers
            </label>
          </div>

          {error && <AlertBox type="error">{error}</AlertBox>}
          {result && (
            <AlertBox type="success">
              ✓ Downloaded <strong>{result.fileName}.{result.format}</strong>
              {' '}({(result.size / 1024).toFixed(1)} KB)
            </AlertBox>
          )}

          <Btn color={T.green} bg="#0a1e10" onClick={handleExport} disabled={loading}
            style={{ padding: '8px 20px', fontSize: 12 }}>
            {loading ? <Spinner /> : '📥'} Export as {format.toUpperCase()}
          </Btn>

          <div style={{ marginTop: 12, fontSize: 10, color: T.txt3 }}>
            Results are exported directly to your browser. Max {maxRows.toLocaleString()} rows.
          </div>
        </>
      )}
    </div>
  );
}
