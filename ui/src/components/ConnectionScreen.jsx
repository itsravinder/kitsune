// ============================================================
// KITSUNE – ConnectionScreen Component
// Initial screen shown before main UI. Tests connection,
// persists profiles, then loads main app on success.
// ============================================================
import React, { useState, useEffect } from 'react';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const DB_TYPES = [
  { value: 'SqlServer',  label: 'SQL Server',  defaultPort: 1433 },
  { value: 'MongoDB',    label: 'MongoDB',      defaultPort: 27017 },
  { value: 'MySQL',      label: 'MySQL',        defaultPort: 3306 },
  { value: 'PostgreSQL', label: 'PostgreSQL',   defaultPort: 5432 },
];

const post = (path, body) =>
  fetch(`${BACKEND}${path}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }).then(r => r.json());

const get = (path) => fetch(`${BACKEND}${path}`).then(r => r.json());

// Color tokens matching existing dark theme
const C = {
  bg:     '#04080f',
  card:   '#07101e',
  input:  '#0a1526',
  border: '#162840',
  bd2:    '#1e3556',
  txt:    '#bccfe0',
  txt2:   '#6b90b0',
  txt3:   '#334a60',
  gold:   '#e2a500',
  green:  '#3dba6e',
  red:    '#e05252',
  blue:   '#4a8eff',
};

export function ConnectionScreen({ onConnected }) {
  const [form, setForm] = useState({
    name: 'Local Development',
    databaseType: 'SqlServer',
    host: 'localhost',
    port: 1433,
    databaseName: '',
    username: 'sa',
    password: '',
    trustCert: true,
    connectionStringOverride: '',
  });
  const [useConnStr,   setUseConnStr]   = useState(false);
  const [profiles,     setProfiles]     = useState([]);
  const [testing,      setTesting]      = useState(false);
  const [saving,       setSaving]       = useState(false);
  const [testResult,   setTestResult]   = useState(null);
  const [selectedProfile, setSelectedProfile] = useState(null);
  const [showNew,      setShowNew]      = useState(true);
  const [backendOk,    setBackendOk]    = useState(null);

  // Check backend reachability on mount
  useEffect(() => {
    fetch(`${BACKEND}/health`)
      .then(r => { setBackendOk(r.ok); })
      .catch(() => setBackendOk(false));
    loadProfiles();
  }, []);

  const loadProfiles = async () => {
    try {
      const res = await get('/api/connections');
      setProfiles(Array.isArray(res) ? res : []);
    } catch { setProfiles([]); }
  };

  const setField = (k, v) => {
    setForm(p => {
      const next = { ...p, [k]: v };
      // Auto-set port when DB type changes
      if (k === 'databaseType') {
        const dt = DB_TYPES.find(d => d.value === v);
        if (dt) next.port = dt.defaultPort;
      }
      return next;
    });
    setTestResult(null);
  };

  const handleTest = async () => {
    setTesting(true); setTestResult(null);
    try {
      const res = await post('/api/connections/test-raw', form);
      setTestResult(res);
    } catch (e) {
      setTestResult({ success: false, message: e.message });
    } finally { setTesting(false); }
  };

  const handleConnect = async () => {
    setSaving(true);
    try {
      // Test first
      const test = await post('/api/connections/test-raw', form);
      if (!test.success) { setTestResult(test); setSaving(false); return; }
      setTestResult(test);

      // Save profile if named
      let profileId = selectedProfile?.id;
      if (showNew && form.name.trim()) {
        const saved = await post('/api/connections', form);
        profileId = saved.id;
      }

      // Signal parent to load main UI
      onConnected({
        profileId,
        databaseType: form.databaseType,
        databaseName: form.databaseName,
        host: form.host,
        serverVersion: test.serverVersion,
        connectionName: form.name || form.host,
      });
    } catch (e) {
      setTestResult({ success: false, message: e.message });
    } finally { setSaving(false); }
  };

  const handleSelectProfile = (profile) => {
    setSelectedProfile(profile);
    setShowNew(false);
    setForm(p => ({
      ...p,
      name: profile.name,
      databaseType: profile.databaseType,
      host: profile.host,
      port: profile.port,
      databaseName: profile.databaseName,
      username: profile.username,
      trustCert: profile.trustCert,
    }));
    setTestResult(null);
  };

  const inp = {
    background: C.input, border: `1px solid ${C.bd2}`, color: C.txt,
    padding: '8px 11px', borderRadius: 6, fontSize: 13,
    fontFamily: "'JetBrains Mono',monospace", outline: 'none', width: '100%',
  };
  const lbl = { fontSize: 11, color: C.txt3, marginBottom: 5, display: 'block', fontWeight: 600, letterSpacing: '.06em' };

  return (
    <div style={{
      minHeight: '100vh', background: C.bg, display: 'flex',
      alignItems: 'center', justifyContent: 'center',
      fontFamily: "'JetBrains Mono',monospace", color: C.txt,
      padding: 20,
    }}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600;700;800&display=swap');
        *{box-sizing:border-box}
        input::placeholder,textarea::placeholder{color:${C.txt3}}
        select option{background:${C.input}}
        button:hover:not(:disabled){opacity:.85;transform:translateY(-1px)}
        button:disabled{opacity:.4;cursor:not-allowed}
      `}</style>

      <div style={{ width: '100%', maxWidth: 900 }}>
        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <div style={{ fontSize: 32, marginBottom: 8 }}>🦊</div>
          <div style={{ fontSize: 24, fontWeight: 800, color: C.gold, letterSpacing: '.1em' }}>KITSUNE</div>
          <div style={{ fontSize: 12, color: C.txt3, marginTop: 4, letterSpacing: '.08em' }}>AI DATABASE INTELLIGENCE SYSTEM</div>
          {backendOk === false && (
            <div style={{ marginTop: 12, padding: '8px 16px', background: '#180808', border: `1px solid #7f1d1d`, borderRadius: 6, fontSize: 11, color: C.red, display: 'inline-block' }}>
              ⚠ Backend not reachable at {BACKEND} — start the .NET API first
            </div>
          )}
          {backendOk === true && (
            <div style={{ marginTop: 12, fontSize: 11, color: C.green }}>✓ Backend connected</div>
          )}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: profiles.length > 0 ? '260px 1fr' : '1fr', gap: 20 }}>
          {/* Saved profiles sidebar */}
          {profiles.length > 0 && (
            <div style={{ background: C.card, border: `1px solid ${C.border}`, borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ padding: '10px 14px', background: '#040c18', borderBottom: `1px solid ${C.border}`, fontSize: 10, fontWeight: 700, color: C.txt2, letterSpacing: '.07em' }}>
                SAVED CONNECTIONS
              </div>
              <div style={{ padding: 8 }}>
                {profiles.map(p => (
                  <div
                    key={p.id}
                    onClick={() => handleSelectProfile(p)}
                    style={{
                      padding: '10px 12px', borderRadius: 6, cursor: 'pointer', marginBottom: 4,
                      background: selectedProfile?.id === p.id ? '#0d1c35' : 'transparent',
                      border: `1px solid ${selectedProfile?.id === p.id ? C.gold + '55' : 'transparent'}`,
                      transition: 'all .15s',
                    }}
                  >
                    <div style={{ fontSize: 12, fontWeight: 600, color: C.txt }}>{p.name}</div>
                    <div style={{ fontSize: 10, color: C.txt3, marginTop: 3 }}>
                      {p.databaseType} · {p.host} · {p.databaseName || '—'}
                    </div>
                    <div style={{ fontSize: 10, marginTop: 3 }}>
                      <span style={{
                        padding: '1px 6px', borderRadius: 4, fontSize: 9,
                        background: p.lastTestOk ? '#0a1e10' : '#1a1200',
                        color: p.lastTestOk ? C.green : C.txt3,
                        border: `1px solid ${p.lastTestOk ? '#1a5a2a55' : '#5a3a0044'}`,
                      }}>
                        {p.lastTestOk ? '✓ Connected' : 'Not tested'}
                      </span>
                    </div>
                  </div>
                ))}
                <button
                  onClick={() => { setSelectedProfile(null); setShowNew(true); setTestResult(null); }}
                  style={{
                    width: '100%', padding: '8px', borderRadius: 6, marginTop: 4,
                    background: 'transparent', border: `1px dashed ${C.bd2}`, color: C.txt3,
                    fontSize: 11, cursor: 'pointer', fontFamily: 'inherit',
                  }}
                >
                  + New Connection
                </button>
              </div>
            </div>
          )}

          {/* Connection form */}
          <div style={{ background: C.card, border: `1px solid ${C.border}`, borderRadius: 10, overflow: 'hidden' }}>
            <div style={{ padding: '12px 16px', background: '#040c18', borderBottom: `1px solid ${C.border}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <span style={{ fontSize: 10, fontWeight: 700, color: C.txt2, letterSpacing: '.07em' }}>
                {showNew ? 'NEW CONNECTION' : `EDIT · ${selectedProfile?.name}`}
              </span>
              <button
                onClick={() => setUseConnStr(!useConnStr)}
                style={{ background: 'none', border: `1px solid ${C.bd2}`, color: useConnStr ? C.gold : C.txt3, padding: '3px 10px', borderRadius: 4, fontSize: 10, cursor: 'pointer', fontFamily: 'inherit' }}
              >
                {useConnStr ? '⊟ Use Fields' : '⊞ Use Connection String'}
              </button>
            </div>

            <div style={{ padding: 20 }}>
              {/* Connection name */}
              <div style={{ marginBottom: 16 }}>
                <label style={lbl}>CONNECTION NAME</label>
                <input style={inp} placeholder="e.g. Production SQL Server"
                  value={form.name} onChange={e => setField('name', e.target.value)} />
              </div>

              {/* DB Type */}
              <div style={{ marginBottom: 16 }}>
                <label style={lbl}>DATABASE TYPE</label>
                <select style={{ ...inp, cursor: 'pointer' }} value={form.databaseType}
                  onChange={e => setField('databaseType', e.target.value)}>
                  {DB_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </div>

              {useConnStr ? (
                <div style={{ marginBottom: 16 }}>
                  <label style={lbl}>CONNECTION STRING</label>
                  <textarea
                    style={{ ...inp, height: 80, resize: 'vertical' }}
                    placeholder="Server=localhost,1433;Database=MyDB;User Id=sa;Password=...;"
                    value={form.connectionStringOverride}
                    onChange={e => setField('connectionStringOverride', e.target.value)}
                  />
                </div>
              ) : (
                <>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 120px', gap: 12, marginBottom: 16 }}>
                    <div>
                      <label style={lbl}>HOST / SERVER</label>
                      <input style={inp} placeholder="localhost or server.database.windows.net"
                        value={form.host} onChange={e => setField('host', e.target.value)} />
                    </div>
                    <div>
                      <label style={lbl}>PORT</label>
                      <input style={inp} type="number" value={form.port}
                        onChange={e => setField('port', Number(e.target.value))} />
                    </div>
                  </div>

                  <div style={{ marginBottom: 16 }}>
                    <label style={lbl}>DATABASE NAME</label>
                    <input style={inp} placeholder="KitsuneDB"
                      value={form.databaseName} onChange={e => setField('databaseName', e.target.value)} />
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 16 }}>
                    <div>
                      <label style={lbl}>USERNAME</label>
                      <input style={inp} placeholder="sa"
                        value={form.username} onChange={e => setField('username', e.target.value)} />
                    </div>
                    <div>
                      <label style={lbl}>PASSWORD</label>
                      <input style={inp} type="password" placeholder="••••••••"
                        value={form.password} onChange={e => setField('password', e.target.value)} />
                    </div>
                  </div>

                  {form.databaseType === 'SqlServer' && (
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: C.txt, cursor: 'pointer', marginBottom: 16 }}>
                      <input type="checkbox" checked={form.trustCert}
                        onChange={e => setField('trustCert', e.target.checked)} />
                      Trust Server Certificate (self-signed / dev)
                    </label>
                  )}
                </>
              )}

              {/* Test result */}
              {testResult && (
                <div style={{
                  padding: '10px 14px', borderRadius: 7, marginBottom: 16,
                  background: testResult.success ? '#0a1e10' : '#180808',
                  border: `1px solid ${testResult.success ? '#1a5a2a55' : '#7f1d1d55'}`,
                  fontSize: 12, color: testResult.success ? C.green : C.red,
                }}>
                  {testResult.success ? '✓' : '✗'} {testResult.message}
                  {testResult.serverVersion && (
                    <div style={{ fontSize: 10, color: C.txt3, marginTop: 4 }}>{testResult.serverVersion.slice(0, 80)}</div>
                  )}
                </div>
              )}

              {/* Buttons */}
              <div style={{ display: 'flex', gap: 10 }}>
                <button
                  onClick={handleTest}
                  disabled={testing || backendOk === false}
                  style={{
                    flex: 1, padding: '10px', borderRadius: 7, cursor: 'pointer',
                    background: '#07101e', border: `1px solid ${C.bd2}`,
                    color: C.txt2, fontSize: 13, fontFamily: 'inherit', fontWeight: 600,
                  }}
                >
                  {testing ? '⟳ Testing…' : '⚡ Test Connection'}
                </button>
                <button
                  onClick={handleConnect}
                  disabled={saving || backendOk === false}
                  style={{
                    flex: 2, padding: '10px', borderRadius: 7, cursor: 'pointer',
                    background: testResult?.success ? '#0a1e10' : '#0d1c35',
                    border: `1px solid ${testResult?.success ? C.green + '88' : C.gold + '55'}`,
                    color: testResult?.success ? C.green : C.gold,
                    fontSize: 13, fontFamily: 'inherit', fontWeight: 700,
                  }}
                >
                  {saving ? '⟳ Connecting…' : `🦊 Connect to ${DB_TYPES.find(d => d.value === form.databaseType)?.label}`}
                </button>
              </div>
            </div>
          </div>
        </div>

        <div style={{ textAlign: 'center', marginTop: 20, fontSize: 10, color: C.txt3 }}>
          KITSUNE v2.0 · Passwords encrypted with AES-256 · Profiles stored in SQL Server
        </div>
      </div>
    </div>
  );
}
