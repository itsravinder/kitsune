// ============================================================
// KITSUNE – Connection Screen  v3
// Supports: SQL Server, MongoDB, PostgreSQL, MySQL
// Auto-discovers all local SQL Server instances
// ============================================================
import React, { useState, useEffect, useCallback } from 'react';

const BACKEND = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

const DB_TYPES = [
  {
    value: 'SqlServer', label: 'SQL Server', icon: '🗄', defaultPort: 1433, color: '#4a8eff',
    hint: 'Named instance: localhost\\INSTANCENAME  |  Default: localhost or localhost,1433',
    placeholder: 'Server=localhost\\INSTANCE;Database=mydb;Trusted_Connection=True;TrustServerCertificate=True;',
  },
  {
    value: 'MongoDB', label: 'MongoDB', icon: '🍃', defaultPort: 27017, color: '#3dba6e',
    hint: 'Local: mongodb://localhost:27017/mydb  |  Atlas: mongodb+srv://user:pass@cluster/db',
    placeholder: 'mongodb://localhost:27017/mydb',
  },
  {
    value: 'PostgreSQL', label: 'PostgreSQL', icon: '🐘', defaultPort: 5432, color: '#38bdf8',
    hint: 'Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass;',
    placeholder: 'Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=pass;',
  },
  {
    value: 'MySQL', label: 'MySQL', icon: '🐬', defaultPort: 3306, color: '#f59e0b',
    hint: 'Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=pass;',
    placeholder: 'Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=pass;',
  },
];

const C = {
  bg:'#04080f', card:'#07101e', input:'#0a1526',
  border:'#162840', bd2:'#1e3556', txt:'#bccfe0',
  txt2:'#6b90b0', txt3:'#334a60', gold:'#e2a500',
  green:'#3dba6e', red:'#e05252', blue:'#4a8eff', amber:'#f59e0b',
};

function buildConnStr(dbType, host, port, dbName, user, pass, trustCert) {
  const h = (host||'').trim(), db = (dbName||'').trim();
  const u = (user||'').trim(), p = (pass||'').trim();
  switch (dbType) {
    case 'SqlServer': {
      const isNamed = h.includes('\\');
      const server  = isNamed ? h : (port && port !== 1433 ? `${h},${port}` : h);
      const auth    = u ? `User Id=${u};Password=${p};` : 'Trusted_Connection=True;';
      return `Server=${server};Database=${db};${auth}TrustServerCertificate=${trustCert?'True':'False'};MultipleActiveResultSets=True;`;
    }
    case 'MongoDB':
      if (u && p) return `mongodb://${u}:${p}@${h}:${port}/${db}`;
      return `mongodb://${h}:${port}/${db}`;
    case 'PostgreSQL':
      return `Host=${h};Port=${port};Database=${db};Username=${u};Password=${p};SslMode=Prefer;`;
    case 'MySQL':
      return `Server=${h};Port=${port};Database=${db};Uid=${u};Pwd=${p};SslMode=None;`;
    default: return '';
  }
}

export function ConnectionScreen({ onConnected }) {
  const [dbType,    setDbType]    = useState('SqlServer');
  const [connName,  setConnName]  = useState('');
  const [host,      setHost]      = useState('');
  const [port,      setPort]      = useState(1433);
  const [dbName,    setDbName]    = useState('');
  const [dbList,     setDbList]     = useState([]);
  const [loadingDbs, setLoadingDbs] = useState(false);
  const [user,      setUser]      = useState('');
  const [pass,      setPass]      = useState('');
  const [trustCert, setTrustCert] = useState(true);
  const [useRaw,    setUseRaw]    = useState(false);
  const [rawConn,   setRawConn]   = useState('');

  const [profiles,   setProfiles]   = useState([]);
  const [instances,  setInstances]  = useState([]);  // discovered SQL Server instances
  const [discovering,setDiscovering]= useState(false);
  const [testing,    setTesting]    = useState(false);
  const [saving,     setSaving]     = useState(false);
  const [result,     setResult]     = useState(null);
  const [backendOk,  setBackendOk]  = useState(null);
  const [builtStr,   setBuiltStr]   = useState('');
  const [showInstPicker, setShowInstPicker] = useState(false);

  const dbInfo = DB_TYPES.find(d => d.value === dbType) || DB_TYPES[0];
  const isNamedInstance = dbType === 'SqlServer' && host.includes('\\');

  // Live connection string preview
  useEffect(() => {
    if (!useRaw) setBuiltStr(buildConnStr(dbType, host, port, dbName, user, pass, trustCert));
  }, [dbType, host, port, dbName, user, pass, trustCert, useRaw]);

  // Auto-set port when DB type changes
  useEffect(() => {
    const dt = DB_TYPES.find(d => d.value === dbType);
    if (dt) setPort(dt.defaultPort);
  }, [dbType]);

  // Backend health + profiles on mount
  useEffect(() => {
    fetch(`${BACKEND}/api/connections`, { signal: AbortSignal.timeout(4000) })
      .then(r => { setBackendOk(r.ok); return r.json(); })
      .then(d => setProfiles(Array.isArray(d) ? d : []))
      .catch(() => setBackendOk(false));
  }, []);

  // Discover SQL Server instances
  const discoverInstances = useCallback(async () => {
    setDiscovering(true);
    setShowInstPicker(true);
    setInstances([]);
    try {
      const r = await fetch(`${BACKEND}/api/connections/discover`,
        { signal: AbortSignal.timeout(8000) });
      const d = await r.json();
      setInstances(d.instances || []);
    } catch {
      setInstances([{ fullName: 'localhost', instanceName: '', isDefault: true, version: '' }]);
    } finally { setDiscovering(false); }
  }, []);

  const pickInstance = (inst) => {
    setHost(inst.fullName);
    setShowInstPicker(false);
    setResult(null);
    if (!connName) setConnName(inst.instanceName || 'Local SQL Server');
  };

  const getEffectiveConnStr = () => useRaw ? rawConn.trim() : builtStr;

  const callTestString = async (cs) => {
    const r = await fetch(`${BACKEND}/api/connections/test-string`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ connectionString: cs, databaseType: dbType }),
    });
    return r.json();
  };

  const handleTest = async () => {
    const cs = getEffectiveConnStr();
    if (!cs) { setResult({ success: false, message: 'Connection string is empty.' }); return; }
    setTesting(true); setResult(null);
    try {
      const r = await callTestString(cs);
      setResult(r);
      // On successful connection, fetch real DB list from sys.databases
      if (r.success && dbType === 'SqlServer') {
        setLoadingDbs(true);
        try {
          const dbRes = await fetch(`${BACKEND}/api/databases`);
          const dbData = await dbRes.json();
          if (dbData.databases?.length) {
            setDbList(dbData.databases.map(d => d.name));
            // Auto-select first DB if none chosen
            if (!dbName) setDbName(dbData.databases[0]?.name || '');
          }
        } catch { /* non-fatal */ } finally { setLoadingDbs(false); }
      }
    }
    catch (e) { setResult({ success: false, message: e.message }); }
    finally { setTesting(false); }
  };

  const handleConnect = async () => {
    const cs = getEffectiveConnStr();
    if (!cs) { setResult({ success: false, message: 'Connection string is empty.' }); return; }
    // Block tempdb — it is a system database, never a valid target
    if (dbName.toLowerCase() === 'tempdb') {
      setResult({ success: false, message: 'tempdb is a system database. Select a user database.' });
      return;
    }
    setSaving(true); setResult(null);
    try {
      const test = await callTestString(cs);
      setResult(test);
      if (!test.success) { setSaving(false); return; }

      const sr = await fetch(`${BACKEND}/api/connections`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: connName || host, databaseType: dbType,
          host: host || 'localhost', port,
          databaseName: dbName.trim() || test.databaseName || '',
          username: user, password: pass, trustCert,
        }),
      });
      const saved = await sr.json();

      onConnected({
        profileId:      saved.id,
        databaseType:   dbType,
        // Use user-typed DB name first, then what DB_NAME() returned from the test
        // NEVER fall back to dbType string (e.g. "SqlServer") — that is not a DB name
        databaseName:   dbName.trim() || test.databaseName || '',
        host:           host || 'localhost',
        serverVersion:  test.serverVersion,
        connectionName: connName || host,
        connectionString: cs,
      });
    } catch (e) {
      setResult({ success: false, message: e.message });
    } finally { setSaving(false); }
  };

  const fillProfile = (p) => {
    setDbType(p.databaseType || 'SqlServer');
    setConnName(p.connectionName || p.name || '');
    setHost(p.host || ''); setPort(p.port || 1433);
    setDbName(p.databaseName || ''); setUser(p.username || '');
    setResult(null); setShowInstPicker(false);
  };

  const inp = {
    background: C.input, border: `1px solid ${C.bd2}`, color: C.txt,
    padding: '8px 11px', borderRadius: 6, fontSize: 13,
    fontFamily: "'JetBrains Mono',monospace", outline: 'none', width: '100%',
  };
  const lbl = { fontSize: 11, color: C.txt3, marginBottom: 5, display: 'block', fontWeight: 600, letterSpacing: '.06em' };
  const qbtn = (color) => ({
    padding: '5px 11px', borderRadius: 5, cursor: 'pointer', fontSize: 10,
    background: '#0d1c35', border: `1px solid ${color}44`, color, fontFamily: 'inherit',
  });

  return (
    <div style={{ minHeight: '100vh', background: C.bg, display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: "'JetBrains Mono',monospace", color: C.txt, padding: 20 }}>
      <style>{`
        *{box-sizing:border-box}
        input::placeholder,textarea::placeholder{color:${C.txt3}}
        select option{background:${C.input}}
        button:hover:not(:disabled){opacity:.85;transform:translateY(-1px)}
        button:disabled{opacity:.4;cursor:not-allowed}
      `}</style>

      <div style={{ width: '100%', maxWidth: 980 }}>

        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 26 }}>
          <div style={{ fontSize: 30, marginBottom: 6 }}>🦊</div>
          <div style={{ fontSize: 22, fontWeight: 800, color: C.gold, letterSpacing: '.1em' }}>KITSUNE</div>
          <div style={{ fontSize: 11, color: C.txt3, marginTop: 3, letterSpacing: '.08em' }}>AI DATABASE INTELLIGENCE SYSTEM</div>
          <div style={{ marginTop: 10, fontSize: 11 }}>
            {backendOk === null && <span style={{ color: C.txt3 }}>⟳ Connecting to backend…</span>}
            {backendOk === true  && <span style={{ color: C.green }}>✓ Backend connected at {BACKEND}</span>}
            {backendOk === false && <span style={{ color: C.red }}>⚠ Backend not reachable — run: cd backend && dotnet run</span>}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: profiles.length > 0 ? '230px 1fr' : '1fr', gap: 18 }}>

          {/* Saved profiles */}
          {profiles.length > 0 && (
            <div style={{ background: C.card, border: `1px solid ${C.border}`, borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ padding: '10px 14px', background: '#040c18', borderBottom: `1px solid ${C.border}`, fontSize: 10, fontWeight: 700, color: C.txt2, letterSpacing: '.07em' }}>
                SAVED CONNECTIONS
              </div>
              <div style={{ padding: 8 }}>
                {profiles.map(p => (
                  <div key={p.id} onClick={() => fillProfile(p)} style={{
                    padding: '10px 12px', borderRadius: 6, cursor: 'pointer',
                    marginBottom: 4, border: '1px solid transparent', transition: 'all .15s',
                  }}
                    onMouseEnter={e => e.currentTarget.style.background = '#0d1c35'}
                    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 14 }}>{DB_TYPES.find(d => d.value === p.databaseType)?.icon || '🗄'}</span>
                      <span style={{ fontSize: 12, fontWeight: 600, color: C.txt }}>{p.connectionName || p.name}</span>
                    </div>
                    <div style={{ fontSize: 10, color: C.txt3, marginTop: 3, paddingLeft: 20 }}>
                      {p.databaseType} · {p.host} · {p.databaseName || '—'}
                    </div>
                  </div>
                ))}
                <button onClick={() => { setResult(null); setHost(''); setDbName(''); setConnName(''); }} style={{
                  width: '100%', padding: 8, borderRadius: 6, marginTop: 4,
                  background: 'transparent', border: `1px dashed ${C.bd2}`,
                  color: C.txt3, fontSize: 11, cursor: 'pointer', fontFamily: 'inherit',
                }}>
                  + New Connection
                </button>
              </div>
            </div>
          )}

          {/* Main form */}
          <div style={{ background: C.card, border: `1px solid ${C.border}`, borderRadius: 10, overflow: 'hidden' }}>

            {/* DB type tabs */}
            <div style={{ display: 'flex', borderBottom: `1px solid ${C.border}` }}>
              {DB_TYPES.map(dt => (
                <button key={dt.value} onClick={() => { setDbType(dt.value); setResult(null); setShowInstPicker(false); }} style={{
                  flex: 1, padding: '10px 4px', border: 'none', cursor: 'pointer', fontFamily: 'inherit',
                  background: dbType === dt.value ? C.card : '#040c18',
                  color: dbType === dt.value ? dt.color : C.txt3,
                  borderBottom: `2px solid ${dbType === dt.value ? dt.color : 'transparent'}`,
                  fontSize: 11, fontWeight: 700, transition: 'all .15s',
                }}>
                  <span style={{ fontSize: 14, display: 'block', marginBottom: 2 }}>{dt.icon}</span>
                  {dt.label}
                </button>
              ))}
            </div>

            <div style={{ padding: 20 }}>

              {/* Connection name + raw toggle */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 10, marginBottom: 16, alignItems: 'end' }}>
                <div>
                  <label style={lbl}>CONNECTION NAME</label>
                  <input style={inp} placeholder="e.g. Production DB, Local Dev"
                    value={connName} onChange={e => setConnName(e.target.value)} />
                </div>
                <button onClick={() => { setUseRaw(!useRaw); setResult(null); }} style={{
                  padding: '8px 12px', borderRadius: 6, cursor: 'pointer',
                  background: useRaw ? '#0d1c35' : 'transparent',
                  border: `1px solid ${useRaw ? C.gold+'88' : C.bd2}`,
                  color: useRaw ? C.gold : C.txt3, fontSize: 10, fontFamily: 'inherit', whiteSpace: 'nowrap',
                }}>
                  {useRaw ? '⊟ Use Fields' : '⊞ Raw String'}
                </button>
              </div>

              {useRaw ? (
                <div style={{ marginBottom: 16 }}>
                  <label style={lbl}>CONNECTION STRING</label>
                  <textarea style={{ ...inp, height: 90, resize: 'vertical', fontSize: 12 }}
                    placeholder={dbInfo.placeholder}
                    value={rawConn} onChange={e => setRawConn(e.target.value)} />
                  <div style={{ fontSize: 10, color: C.txt3, marginTop: 5 }}>💡 {dbInfo.hint}</div>
                </div>
              ) : (
                <>
                  {/* Host row with discover button for SQL Server */}
                  <div style={{ marginBottom: 14 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 5 }}>
                      <label style={{ ...lbl, marginBottom: 0 }}>HOST / SERVER</label>
                      {dbType === 'SqlServer' && (
                        <button onClick={discoverInstances} disabled={discovering} style={{
                          padding: '3px 9px', borderRadius: 4, cursor: 'pointer', fontSize: 10,
                          background: 'transparent', border: `1px solid ${C.blue}55`,
                          color: C.blue, fontFamily: 'inherit',
                        }}>
                          {discovering ? '⟳ Scanning…' : '🔍 Discover Instances'}
                        </button>
                      )}
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px', gap: 10 }}>
                      <input style={inp}
                        placeholder={dbType === 'SqlServer' ? 'localhost  or  localhost\\INSTANCENAME' : 'localhost'}
                        value={host} onChange={e => { setHost(e.target.value); setResult(null); setShowInstPicker(false); }} />
                      <input style={{ ...inp, opacity: isNamedInstance ? 0.4 : 1 }}
                        type="number" value={port}
                        onChange={e => { setPort(Number(e.target.value)); setResult(null); }}
                        disabled={isNamedInstance}
                        title={isNamedInstance ? 'Port ignored for named instances — SQL Browser routes automatically' : 'Port'} />
                    </div>

                    {isNamedInstance && (
                      <div style={{ fontSize: 10, color: C.amber, marginTop: 6, padding: '4px 8px', background: '#1a1000', borderRadius: 4, border: `1px solid ${C.amber}33` }}>
                        ⚡ Named instance — port ignored. SQL Server Browser routes automatically.
                      </div>
                    )}

                    {/* Instance picker dropdown */}
                    {showInstPicker && (
                      <div style={{ marginTop: 8, background: '#040c18', border: `1px solid ${C.bd2}`, borderRadius: 7, overflow: 'hidden' }}>
                        <div style={{ padding: '8px 12px', borderBottom: `1px solid ${C.border}`, fontSize: 10, color: C.txt3, fontWeight: 700, letterSpacing: '.06em', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <span>DISCOVERED SQL SERVER INSTANCES {discovering && '⟳'}</span>
                          <button onClick={() => setShowInstPicker(false)} style={{ background: 'none', border: 'none', color: C.txt3, cursor: 'pointer', fontSize: 13 }}>✕</button>
                        </div>
                        {discovering && (
                          <div style={{ padding: '12px 14px', fontSize: 11, color: C.txt3 }}>
                            Scanning network for SQL Server instances… (up to 8 seconds)
                          </div>
                        )}
                        {!discovering && instances.length === 0 && (
                          <div style={{ padding: '12px 14px', fontSize: 11, color: C.txt3 }}>
                            No instances found. Make sure SQL Server Browser service is running.
                          </div>
                        )}
                        {instances.map((inst, i) => (
                          <div key={i} onClick={() => pickInstance(inst)} style={{
                            padding: '10px 14px', cursor: 'pointer', borderBottom: `1px solid ${C.border}`,
                            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                            transition: 'background .1s',
                          }}
                            onMouseEnter={e => e.currentTarget.style.background = '#0d1c35'}
                            onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                          >
                            <div>
                              <div style={{ fontSize: 13, fontWeight: 700, color: C.blue }}>
                                {inst.fullName}
                              </div>
                              <div style={{ fontSize: 10, color: C.txt3, marginTop: 2 }}>
                                {inst.isDefault ? 'Default instance' : `Named: ${inst.instanceName}`}
                                {inst.version ? ` · v${inst.version}` : ''}
                              </div>
                            </div>
                            <span style={{ fontSize: 11, color: C.green, padding: '3px 9px', background: '#0a1e10', borderRadius: 4, border: `1px solid #1a5a2a44` }}>
                              Select →
                            </span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div style={{ marginBottom: 14 }}>
                    <label style={{ ...lbl, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span>DATABASE NAME</span>
                      {loadingDbs && <span style={{ color: C.txt3, fontSize: 10 }}>⟳ Loading…</span>}
                      {dbList.length > 0 && !loadingDbs && (
                        <span style={{ color: C.green, fontSize: 10 }}>✓ {dbList.length} databases found</span>
                      )}
                    </label>
                    {dbList.length > 0 ? (
                      <select style={{ ...inp, cursor: 'pointer' }}
                        value={dbName} onChange={e => { setDbName(e.target.value); setResult(null); }}>
                        <option value="">— Select database —</option>
                        {dbList.map(db => (
                          <option key={db} value={db}>{db}</option>
                        ))}
                      </select>
                    ) : (
                      <input style={inp} placeholder="KitsuneDB  (test connection first to see available DBs)"
                        value={dbName} onChange={e => { setDbName(e.target.value); setResult(null); }} />
                    )}
                    {dbName.toLowerCase() === 'tempdb' && (
                      <div style={{ marginTop: 5, fontSize: 10, color: C.red }}>
                        ⚠ tempdb is a system database — select a user database instead
                      </div>
                    )}
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 14 }}>
                    <div>
                      <label style={lbl}>USERNAME</label>
                      <input style={inp}
                        placeholder={dbType === 'SqlServer' ? 'sa  (blank = Windows Auth)' : 'username'}
                        value={user} onChange={e => { setUser(e.target.value); setResult(null); }} />
                    </div>
                    <div>
                      <label style={lbl}>PASSWORD</label>
                      <input style={inp} type="password" placeholder="••••••••"
                        value={pass} onChange={e => { setPass(e.target.value); setResult(null); }} />
                    </div>
                  </div>

                  {dbType === 'SqlServer' && (
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: C.txt, cursor: 'pointer', marginBottom: 14 }}>
                      <input type="checkbox" checked={trustCert} onChange={e => { setTrustCert(e.target.checked); setResult(null); }} />
                      Trust Server Certificate (required for local / self-signed)
                    </label>
                  )}

                  {/* Live connection string preview */}
                  <div style={{ marginBottom: 16 }}>
                    <label style={lbl}>GENERATED CONNECTION STRING</label>
                    <div style={{ padding: '8px 10px', background: '#040c18', borderRadius: 5, border: `1px solid ${C.border}`, fontSize: 10, color: C.txt3, wordBreak: 'break-all', lineHeight: 1.6, fontFamily: 'monospace', minHeight: 36 }}>
                      {builtStr || <span style={{ fontStyle: 'italic' }}>Fill in fields above…</span>}
                    </div>
                  </div>
                </>
              )}

              {/* Common quick-fills per DB type */}
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 10, color: C.txt3, fontWeight: 600, letterSpacing: '.06em', marginBottom: 7 }}>QUICK CONNECT</div>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  {dbType === 'SqlServer' && (<>
                    <button onClick={() => { setUseRaw(true); setRawConn(`Server=localhost\\${'{'}INSTANCE{'}'};Database=KitsuneDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;`); }} style={qbtn(C.txt2)}>
                      📋 Windows Auth template
                    </button>
                    <button onClick={() => { setUseRaw(true); setRawConn('Server=localhost;Database=KitsuneDB;User Id=sa;Password=;TrustServerCertificate=True;MultipleActiveResultSets=True;'); }} style={qbtn(C.blue)}>
                      📋 Default instance · sa
                    </button>
                    <button onClick={discoverInstances} disabled={discovering} style={qbtn(C.green)}>
                      {discovering ? '⟳ Scanning…' : '🔍 Auto-discover all instances'}
                    </button>
                  </>)}
                  {dbType === 'MongoDB' && (<>
                    <button onClick={() => { setUseRaw(false); setHost('localhost'); setPort(27017); setUser(''); setPass(''); setResult(null); }} style={qbtn(C.green)}>localhost:27017</button>
                    <button onClick={() => { setUseRaw(true); setRawConn('mongodb://localhost:27017/'); }} style={qbtn(C.txt2)}>📋 Local template</button>
                  </>)}
                  {dbType === 'PostgreSQL' && (<>
                    <button onClick={() => { setUseRaw(false); setHost('localhost'); setPort(5432); setUser('postgres'); setResult(null); }} style={qbtn(C.blue)}>localhost:5432 · postgres</button>
                  </>)}
                  {dbType === 'MySQL' && (<>
                    <button onClick={() => { setUseRaw(false); setHost('localhost'); setPort(3306); setUser('root'); setResult(null); }} style={qbtn(C.amber)}>localhost:3306 · root</button>
                  </>)}
                </div>
              </div>

              {/* Test result */}
              {result && (
                <div style={{ padding: '10px 14px', borderRadius: 7, marginBottom: 16, background: result.success ? '#0a1e10' : '#180808', border: `1px solid ${result.success ? '#1a5a2a55' : '#7f1d1d55'}`, fontSize: 12, color: result.success ? C.green : C.red }}>
                  {result.success ? '✓' : '✗'} {result.message}
                  {result.serverVersion && <div style={{ fontSize: 10, color: C.txt3, marginTop: 4 }}>{String(result.serverVersion).slice(0, 100)}</div>}
                  {result.databaseName && <div style={{ fontSize: 10, color: C.txt3, marginTop: 2 }}>Database: {result.databaseName} · {result.responseMs?.toFixed(0)}ms</div>}
                </div>
              )}

              {/* Action buttons */}
              <div style={{ display: 'flex', gap: 10 }}>
                <button onClick={handleTest} disabled={testing} style={{ flex: 1, padding: 10, borderRadius: 7, cursor: 'pointer', background: '#07101e', border: `1px solid ${C.bd2}`, color: C.txt2, fontSize: 13, fontFamily: 'inherit', fontWeight: 600 }}>
                  {testing ? '⟳ Testing…' : '⚡ Test Connection'}
                </button>
                <button onClick={handleConnect} disabled={saving} style={{ flex: 2, padding: 10, borderRadius: 7, cursor: 'pointer', background: result?.success ? '#0a1e10' : '#0d1c35', border: `1px solid ${result?.success ? C.green+'88' : dbInfo.color+'55'}`, color: result?.success ? C.green : dbInfo.color, fontSize: 13, fontFamily: 'inherit', fontWeight: 700 }}>
                  {saving ? '⟳ Connecting…' : `${dbInfo.icon} Connect to ${dbInfo.label}`}
                </button>
              </div>

            </div>
          </div>
        </div>

        <div style={{ textAlign: 'center', marginTop: 16, fontSize: 10, color: C.txt3 }}>
          KITSUNE v6.0 · SQL Server · MongoDB · PostgreSQL · MySQL · Auto-discovery enabled
        </div>
      </div>
    </div>
  );
}
