// ============================================================
// KITSUNE – Complete React UI v2
// All 10 features: NL→SQL, Schema, Validation, Preview,
// Backup, Rollback, Apply, Risk, Explain, Connections, Audit
// ============================================================
import { useState, useEffect, useCallback, useRef } from "react";

const BACKEND = process.env.REACT_APP_BACKEND_URL || "http://localhost:5000";
const AI_SVC  = process.env.REACT_APP_AI_URL      || "http://localhost:8000";

// ── API layer ─────────────────────────────────────────────────
const post = (base, path, body) =>
  fetch(`${base}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  }).then(r => r.json());

const get = (base, path) =>
  fetch(`${base}${path}`).then(r => r.json());

const api = {
  // Core
  validate   : b => post(BACKEND, "/api/validate",          b),
  backup     : b => post(BACKEND, "/api/backup",            b),
  rollback   : b => post(BACKEND, "/api/rollback",          b),
  preview    : b => post(BACKEND, "/api/preview",           b),
  apply      : b => post(BACKEND, "/api/apply",             b),
  getVersions: n => get(BACKEND,  `/api/versions/${encodeURIComponent(n)}`),
  getDefn    : n => get(BACKEND,  `/api/versions/${encodeURIComponent(n)}/definition`),
  // Schema
  getSqlSchema : (db) => get(BACKEND, `/api/schema/sqlserver${db ? `?db=${db}` : ""}`),
  getDdl       : (db) => get(BACKEND, `/api/schema/ddl${db ? `?db=${db}` : ""}`),
  // Diff
  compare    : b => post(BACKEND, "/api/changesummary/compare", b),
  // Connections
  listConns  : ()  => get(BACKEND,  "/api/connections"),
  saveConn   : b   => post(BACKEND, "/api/connections",     b),
  testConn   : id  => post(BACKEND, `/api/connections/${id}/test`, {}),
  deleteConn : id  => fetch(`${BACKEND}/api/connections/${id}`, { method: "DELETE" }).then(r => r.json()),
  // Audit
  getAudit   : (n, top) => get(BACKEND, `/api/audit?${n?`objectName=${encodeURIComponent(n)}&`:""}top=${top||100}`),
  // AI
  generate   : b => post(AI_SVC,  "/generate",          b),
  listModels : () => get(AI_SVC,  "/models"),
  explain    : b => post(AI_SVC,  "/explain",           b),
  risk       : b => post(AI_SVC,  "/risk",              b),
  aiSummary  : b => post(AI_SVC,  "/summarize-change",  b),
};

// ── Utility ───────────────────────────────────────────────────
const ts = () => new Date().toLocaleTimeString();
const fmtMs = ms => ms < 1000 ? `${ms.toFixed(0)}ms` : `${(ms/1000).toFixed(2)}s`;

// ── CSS-in-JS theme ───────────────────────────────────────────
const T = {
  bg0:"#04080f", bg1:"#070f1c", bg2:"#0a1526", bg3:"#0d1c35",
  bg4:"#111f3a", border:"#162840", border2:"#1e3556",
  txt:"#bccfe0", txt2:"#6b90b0", txt3:"#334a60",
  gold:"#e2a500", gold2:"#ffca40",
  blue:"#4a8eff", green:"#3dba6e", red:"#e05252",
  purple:"#a37eff", cyan:"#38bdf8", amber:"#f59e0b",
  r:6,
};

const css = {
  root:{display:"flex",flexDirection:"column",height:"100vh",
    background:T.bg0,color:T.txt,fontFamily:"'JetBrains Mono',monospace",
    fontSize:12,overflow:"hidden"},
  topbar:{display:"flex",alignItems:"center",gap:10,padding:"0 16px",
    height:46,background:T.bg1,borderBottom:`1px solid ${T.border}`,flexShrink:0},
  logo:{display:"flex",alignItems:"center",gap:8,color:T.gold,
    fontWeight:800,fontSize:15,letterSpacing:".1em"},
  badge:(c=T.blue)=>({background:T.bg3,color:c,padding:"2px 8px",
    borderRadius:4,fontSize:10,border:`1px solid ${c}33`}),
  main:{display:"flex",flex:1,overflow:"hidden"},
  leftPane:{display:"flex",flexDirection:"column",width:420,
    borderRight:`1px solid ${T.border}`,flexShrink:0,overflow:"hidden"},
  rightPane:{display:"flex",flexDirection:"column",flex:1,overflow:"hidden"},
  sectionHead:(label)=>({display:"flex",alignItems:"center",gap:7,
    padding:"7px 12px",borderBottom:`1px solid ${T.border}`,
    background:T.bg1,color:T.txt2,fontSize:10,fontWeight:700,letterSpacing:".07em"}),
  textarea:{background:T.bg1,border:"none",color:T.txt,padding:"9px 12px",
    fontFamily:"'JetBrains Mono',monospace",fontSize:11.5,
    resize:"none",outline:"none",lineHeight:1.7,width:"100%"},
  input:{background:T.bg2,border:`1px solid ${T.border2}`,color:T.txt,
    padding:"5px 9px",borderRadius:T.r,fontSize:11,fontFamily:"inherit",
    outline:"none",width:"100%"},
  select:{background:T.bg2,border:`1px solid ${T.border2}`,color:T.txt,
    padding:"5px 8px",borderRadius:T.r,fontSize:11,fontFamily:"inherit",cursor:"pointer",outline:"none"},
  row:{display:"flex",gap:7,padding:"7px 12px",alignItems:"center",flexWrap:"wrap"},
  btn:(c=T.blue,bg=T.bg3)=>({display:"inline-flex",alignItems:"center",gap:5,
    padding:"5px 12px",borderRadius:T.r,fontFamily:"inherit",fontSize:11,fontWeight:700,
    cursor:"pointer",border:`1px solid ${c}44`,background:bg,color:c,
    transition:"opacity .15s,transform .1s",whiteSpace:"nowrap"}),
  applyBtn:{display:"flex",justifyContent:"center",alignItems:"center",gap:7,
    margin:"0 12px 10px",padding:"8px",borderRadius:T.r,fontFamily:"inherit",
    fontSize:12,fontWeight:700,cursor:"pointer",border:`1px solid #7f1d1d`,
    background:"#180808",color:T.red,width:"calc(100% - 24px)"},
  tabBar:{display:"flex",background:T.bg1,borderBottom:`1px solid ${T.border}`,
    flexShrink:0,overflowX:"auto"},
  tab:(a)=>({padding:"8px 14px",fontSize:10,fontWeight:700,letterSpacing:".06em",
    cursor:"pointer",color:a?T.gold:T.txt3,
    borderBottom:a?`2px solid ${T.gold}`:"2px solid transparent",
    whiteSpace:"nowrap"}),
  panel:{flex:1,overflow:"auto"},
  card:{background:T.bg2,borderRadius:8,margin:10,border:`1px solid ${T.border}`,overflow:"hidden"},
  th:{background:T.bg3,color:T.blue,padding:"5px 11px",textAlign:"left",
    fontWeight:700,letterSpacing:".06em",borderBottom:`1px solid ${T.border}`,whiteSpace:"nowrap"},
  td:{padding:"5px 11px",borderBottom:`1px solid ${T.bg1}`,
    overflow:"hidden",textOverflow:"ellipsis",whiteSpace:"nowrap"},
  statusBar:{display:"flex",alignItems:"center",gap:14,padding:"3px 12px",
    background:T.bg1,borderTop:`1px solid ${T.border}`,fontSize:10,color:T.txt3,flexShrink:0},
  errBox:{background:"#180808",border:`1px solid #6a1a1a`,color:"#fca5a5",
    padding:"8px 12px",borderRadius:6,margin:10,fontSize:11},
  okBox:{background:"#0a1e10",border:`1px solid #1a5a2a`,color:"#86efac",
    padding:"8px 12px",borderRadius:6,margin:10,fontSize:11},
  warnBox:{background:"#1a1200",border:`1px solid #5a3a00`,color:"#fde68a",
    padding:"8px 12px",borderRadius:6,margin:10,fontSize:11},
};

const OBJECT_TYPES = ["PROCEDURE","FUNCTION","VIEW","TABLE","TRIGGER"];
const DB_TYPES     = ["SqlServer","MongoDB"];

// ── Status badge ──────────────────────────────────────────────
const SBadge = ({ s }) => {
  const m = {
    PASS:{bg:"#0a2a18",c:"#4ade80"}, WARN:{bg:"#1a1200",c:"#facc15"},
    FAIL:{bg:"#180808",c:"#f87171"}, APPLIED:{bg:"#0a2a18",c:"#4ade80"},
    BLOCKED:{bg:"#180808",c:"#f87171"}, FAILED:{bg:"#180808",c:"#f87171"},
    HIGH:{bg:"#2a0808",c:"#f87171"}, MEDIUM:{bg:"#1a1200",c:"#facc15"},
    LOW:{bg:"#0a2a18",c:"#4ade80"}, CRITICAL:{bg:"#2a0808",c:"#f87171"},
    SUCCESS:{bg:"#0a2a18",c:"#4ade80"},
  };
  const v = m[s] || {bg:T.bg3,c:T.txt2};
  return <span style={{background:v.bg,color:v.c,padding:"3px 10px",
    borderRadius:999,fontSize:10,fontWeight:700,border:`1px solid ${v.c}33`,
    display:"inline-flex",alignItems:"center",gap:4}}>{s}</span>;
};

const Spin = () => (
  <span style={{display:"inline-block",width:13,height:13,border:`2px solid ${T.border2}`,
    borderTop:`2px solid ${T.gold}`,borderRadius:"50%",
    animation:"kspin .8s linear infinite"}}/>
);

const Pill = ({ label, color=T.blue }) => (
  <span style={{background:`${color}18`,color,padding:"2px 8px",
    borderRadius:999,fontSize:10,fontWeight:600,border:`1px solid ${color}33`}}>{label}</span>
);

// ── Tabs ──────────────────────────────────────────────────────
const ALL_TABS = [
  {id:"results",     label:"Results"},
  {id:"validation",  label:"Validation"},
  {id:"history",     label:"Versions"},
  {id:"diff",        label:"Diff"},
  {id:"risk",        label:"Risk"},
  {id:"explain",     label:"Explain"},
  {id:"schema",      label:"Schema"},
  {id:"connections", label:"Connections"},
  {id:"audit",       label:"Audit Log"},
];

// ─────────────────────────────────────────────────────────────
export default function KitsuneApp() {
  // ── Core state ────────────────────────────────────────────
  const [nlQuery,      setNlQuery]      = useState("Show all customers with orders in the last 30 days and their total spend");
  const [sqlQuery,     setSqlQuery]     = useState("");
  const [objectName,   setObjectName]   = useState("");
  const [objectType,   setObjectType]   = useState("PROCEDURE");
  const [dbType,       setDbType]       = useState("SqlServer");
  const [model,        setModel]        = useState("auto");
  const [activeTab,    setActiveTab]    = useState("results");

  // ── Data state ────────────────────────────────────────────
  const [models,       setModels]       = useState([]);
  const [validation,   setValidation]   = useState(null);
  const [preview,      setPreview]      = useState(null);
  const [applyResult,  setApplyResult]  = useState(null);
  const [versions,     setVersions]     = useState([]);
  const [backupResult, setBackupResult] = useState(null);
  const [rollbackResult,setRollbackResult]=useState(null);
  const [riskResult,   setRiskResult]   = useState(null);
  const [explanation,  setExplanation]  = useState("");
  const [genMeta,      setGenMeta]      = useState("");
  const [diffResult,   setDiffResult]   = useState(null);
  const [schema,       setSchema]       = useState(null);
  const [connections,  setConnections]  = useState([]);
  const [auditLogs,    setAuditLogs]    = useState([]);

  // ── Connection form ───────────────────────────────────────
  const [connForm, setConnForm] = useState({
    name:"", databaseType:"SqlServer", host:"localhost",
    port:1433, databaseName:"", username:"sa", password:"", trustCert:true,
  });
  const [connTestResult, setConnTestResult] = useState(null);

  // ── Loading ───────────────────────────────────────────────
  const [loading, setLoading] = useState({});
  const setLoad  = (k, v) => setLoading(p => ({ ...p, [k]: v }));

  // Diffs compare state
  const [diffVA, setDiffVA] = useState(0);
  const [diffVB, setDiffVB] = useState(0);

  useEffect(() => {
    api.listModels()
       .then(data => setModels([
         { id:"auto", display_name:"⚡ Auto-Route", type:"system", available:true },
         ...data,
       ]))
       .catch(() => setModels([
         { id:"auto",       display_name:"⚡ Auto-Route",        type:"system", available:true },
         { id:"sqlcoder",   display_name:"SQLCoder (Local)",     type:"local",  available:true },
         { id:"qwen3-coder",display_name:"Qwen3 480B (Cloud)",  type:"cloud",  available:true },
       ]));
  }, []);

  // ── Handlers ──────────────────────────────────────────────
  const handle = (key, fn) => async (...args) => {
    setLoad(key, true);
    try { await fn(...args); }
    catch(e) { console.error(key, e); }
    finally { setLoad(key, false); }
  };

  const handleGenerate = handle("gen", async () => {
    setGenMeta("");
    const res = await api.generate({ natural_language:nlQuery, database_type:dbType, model });
    setSqlQuery(res.generated_query || "");
    setGenMeta(`${res.display_name} · Confidence ${(res.confidence_score*100).toFixed(0)}% · ${fmtMs(res.execution_ms)} · ${res.tokens_used} tokens${res.fallback_used?" · fallback used":""}`);
  });

  const handleValidate = handle("validate", async () => {
    const res = await api.validate({ objectName, objectType, newDefinition:sqlQuery });
    setValidation(res);
    setActiveTab("validation");
  });

  const handlePreview = handle("preview", async () => {
    const res = await api.preview({ sqlQuery, isStoredProc:false, timeoutSeconds:30 });
    setPreview(res);
    setActiveTab("results");
  });

  const handleBackup = handle("backup", async () => {
    const res = await api.backup({ objectName, objectType });
    setBackupResult(res);
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
    setActiveTab("history");
  });

  const handleVersions = handle("versions", async () => {
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
    setActiveTab("history");
  });

  const handleRollback = handle("rollback", async (vNum) => {
    const res = await api.rollback({ objectName, versionNumber:vNum });
    setRollbackResult(res);
    if (res.restoredScript) setSqlQuery(res.restoredScript);
    const v = await api.getVersions(objectName);
    setVersions(v.versions || []);
  });

  const handleApply = handle("apply", async () => {
    const res = await api.apply({
      objectName, objectType, sqlScript:sqlQuery,
      skipValidation:false, skipBackup:false,
    });
    setApplyResult(res);
    setActiveTab("results");
  });

  const handleRisk = handle("risk", async () => {
    const res = await api.risk({ query:sqlQuery, object_type:objectType });
    setRiskResult(res);
    setActiveTab("risk");
  });

  const handleExplain = handle("explain", async () => {
    const res = await api.explain({ query:sqlQuery, model });
    setExplanation(res.explanation || "");
    setActiveTab("explain");
  });

  const handleSchema = handle("schema", async () => {
    const res = await api.getSqlSchema();
    setSchema(res);
    setActiveTab("schema");
  });

  const handleDiff = handle("diff", async () => {
    if (versions.length < 2) return;
    const va = versions.find(v => v.versionNumber === diffVA);
    const vb = versions.find(v => v.versionNumber === diffVB);
    if (!va || !vb) return;
    const res = await api.compare({
      objectName, oldScript:va.scriptContent, newScript:vb.scriptContent,
      oldVersion:diffVA, newVersion:diffVB, model,
    });
    setDiffResult(res);
    setActiveTab("diff");
  });

  const handleLoadConnections = handle("conns", async () => {
    const res = await api.listConns();
    setConnections(res || []);
    setActiveTab("connections");
  });

  const handleSaveConn = handle("saveConn", async () => {
    await api.saveConn({ ...connForm });
    const res = await api.listConns();
    setConnections(res || []);
  });

  const handleTestConn = handle("testConn", async (id) => {
    const res = await api.testConn(id);
    setConnTestResult(res);
  });

  const handleLoadAudit = handle("audit", async () => {
    const res = await api.getAudit(objectName || null, 100);
    setAuditLogs(res.logs || []);
    setActiveTab("audit");
  });

  // ── Render helpers ────────────────────────────────────────
  const SH = ({ label, color=T.txt2 }) => (
    <div style={css.sectionHead(label)}>
      <span style={{color}}>{label}</span>
    </div>
  );

  const BtnRow = () => (
    <div style={css.row}>
      <button style={css.btn(T.green,"#0a1e10")} onClick={handleValidate} disabled={loading.validate}>
        {loading.validate?<Spin/>:"🛡"} Validate
      </button>
      <button style={css.btn(T.purple,"#120e2a")} onClick={handlePreview} disabled={loading.preview}>
        {loading.preview?<Spin/>:"👁"} Preview
      </button>
      <button style={css.btn(T.gold,"#1a1200")} onClick={handleBackup} disabled={loading.backup}>
        {loading.backup?<Spin/>:"💾"} Backup
      </button>
      <button style={css.btn(T.txt2,T.bg3)} onClick={handleVersions} disabled={loading.versions}>
        {loading.versions?<Spin/>:"🕐"} Versions
      </button>
      <button style={css.btn(T.red,"#180808")} onClick={handleRisk} disabled={loading.risk}>
        {loading.risk?<Spin/>:"⚠"} Risk
      </button>
      <button style={css.btn(T.cyan,"#071828")} onClick={handleExplain} disabled={loading.explain}>
        {loading.explain?<Spin/>:"💡"} Explain
      </button>
      <button style={css.btn(T.amber,"#1a1000")} onClick={handleSchema} disabled={loading.schema}>
        {loading.schema?<Spin/>:"🗃"} Schema
      </button>
      <button style={css.btn(T.txt2,T.bg3)} onClick={handleLoadAudit} disabled={loading.audit}>
        {loading.audit?<Spin/>:"📋"} Audit
      </button>
    </div>
  );

  // ── Left pane ─────────────────────────────────────────────
  const LeftPane = () => (
    <div style={css.leftPane}>
      {/* NL Input */}
      <SH label="◆ NATURAL LANGUAGE QUERY" />
      <div style={{padding:"9px 12px"}}>
        <textarea style={{...css.textarea,height:80,borderRadius:6,border:`1px solid ${T.border2}`}}
          value={nlQuery} onChange={e=>setNlQuery(e.target.value)}
          placeholder="Describe your query in plain English…"/>
      </div>
      <div style={css.row}>
        <select style={{...css.select,flex:1}} value={dbType} onChange={e=>setDbType(e.target.value)}>
          {DB_TYPES.map(t=><option key={t}>{t}</option>)}
        </select>
        <button style={css.btn(T.green,"#0a1e10")} onClick={handleGenerate} disabled={loading.gen}>
          {loading.gen?<Spin/>:"▶"} Generate
        </button>
      </div>
      {genMeta && <div style={{padding:"0 12px 8px",fontSize:10,color:T.txt3}}>{genMeta}</div>}

      {/* SQL Editor */}
      <div style={{flex:1,display:"flex",flexDirection:"column",borderTop:`1px solid ${T.border}`,overflow:"hidden"}}>
        <div style={{...css.sectionHead("▣ SQL / QUERY EDITOR"),justifyContent:"space-between"}}>
          <span style={{color:T.txt2,fontSize:10,fontWeight:700}}>▣ SQL / QUERY EDITOR</span>
          <button style={{...css.btn(T.txt3,T.bg1),padding:"2px 8px",fontSize:10}}
            onClick={()=>navigator.clipboard?.writeText(sqlQuery)}>⎘ Copy</button>
        </div>
        <textarea style={{...css.textarea,flex:1,minHeight:140,background:"#050e1c"}}
          value={sqlQuery} onChange={e=>setSqlQuery(e.target.value)}
          placeholder="-- Generated SQL or type your own…&#10;-- Preview wraps in BEGIN TRAN/ROLLBACK (safe mode)"
          spellCheck={false}/>
      </div>

      {/* Object config */}
      <div style={{borderTop:`1px solid ${T.border}`}}>
        <SH label="⚙ OBJECT CONFIGURATION" />
        <div style={css.row}>
          <input style={{...css.input,flex:1}} placeholder="Object name (e.g. usp_GetOrders)"
            value={objectName} onChange={e=>setObjectName(e.target.value)}/>
          <select style={css.select} value={objectType} onChange={e=>setObjectType(e.target.value)}>
            {OBJECT_TYPES.map(t=><option key={t}>{t}</option>)}
          </select>
        </div>
      </div>

      <BtnRow />

      <button style={css.applyBtn} onClick={handleApply} disabled={loading.apply}>
        {loading.apply?<Spin/>:"⚡"} APPLY CHANGE (LIVE)
      </button>
    </div>
  );

  // ── Result set table ──────────────────────────────────────
  const ResultTable = ({ data, columns }) => (
    <div style={{overflowX:"auto",margin:10}}>
      <table style={{width:"100%",borderCollapse:"collapse",fontSize:11}}>
        <thead><tr>{columns.map(c=><th key={c} style={css.th}>{c}</th>)}</tr></thead>
        <tbody>
          {data.map((row,i)=>(
            <tr key={i} style={{background:i%2===0?"transparent":T.bg1}}>
              {columns.map(c=>(
                <td key={c} style={{...css.td,maxWidth:240}} title={String(row[c]??"NULL")}>
                  {row[c]===null?<span style={{color:T.txt3}}>NULL</span>:String(row[c])}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );

  // ── Panel content per tab ─────────────────────────────────
  const PanelContent = () => {
    switch(activeTab) {

      // ── RESULTS ─────────────────────────────────────────
      case "results": return (
        <div>
          {applyResult && (
            <div style={applyResult.success?css.okBox:css.errBox}>
              <SBadge s={applyResult.status}/>&nbsp;&nbsp;
              {applyResult.message}
              {applyResult.backupVersion && ` · Auto-backed up as v${applyResult.backupVersion}`}
              {applyResult.errors?.map((e,i)=><div key={i} style={{marginTop:4}}>• {e}</div>)}
            </div>
          )}
          {preview ? (
            <>
              <div style={{display:"flex",gap:14,padding:"8px 14px",
                background:T.bg1,borderBottom:`1px solid ${T.border}`,flexWrap:"wrap"}}>
                <span style={{color:preview.success?T.green:T.red}}>
                  {preview.success?"✓ SAFE_PREVIEW":"✗ Error"}
                </span>
                <span style={{color:T.txt2}}>Rows: <b style={{color:T.txt}}>{preview.rowCount}</b></span>
                <span style={{color:T.txt2}}>Time: <b style={{color:T.txt}}>{fmtMs(preview.executionMs)}</b></span>
                <span style={{color:T.txt2}}>Mode: <b style={{color:T.gold}}>BEGIN TRAN / ROLLBACK</b></span>
              </div>
              {preview.errors?.length>0 && <div style={css.errBox}>{preview.errors.map((e,i)=><div key={i}>{e}</div>)}</div>}
              {preview.messages?.length>0 && <div style={css.okBox}>{preview.messages.map((m,i)=><div key={i}>{m}</div>)}</div>}
              {preview.resultSet?.length>0 && <ResultTable data={preview.resultSet} columns={preview.columns}/>}
              {preview.resultSet?.length===0 && preview.success &&
                <div style={{padding:20,color:T.txt3,fontSize:11}}>Query executed successfully. No rows returned.</div>}
            </>
          ) : (
            !applyResult && <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>👁</div>
              Click <b>Preview</b> (safe mode) or <b>Apply</b> (live)
            </div>
          )}
        </div>
      );

      // ── VALIDATION ──────────────────────────────────────
      case "validation": return (
        <div>
          {!validation ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>🛡</div>
              Click <b>Validate</b> to analyze dependencies
            </div>
          ) : (
            <>
              <div style={{padding:"12px 16px",display:"flex",alignItems:"center",gap:10,
                background:validation.status==="PASS"?"#0a1e10":validation.status==="FAIL"?"#180808":"#1a1200",
                borderBottom:`1px solid ${T.border}`}}>
                <SBadge s={validation.status}/>
                <span style={{fontSize:11}}>{validation.message}</span>
              </div>
              {validation.warnings?.length>0 && (
                <div style={css.warnBox}>
                  <div style={{fontWeight:700,fontSize:10,marginBottom:6}}>⚠ WARNINGS</div>
                  {validation.warnings.map((w,i)=><div key={i} style={{marginTop:4}}>• {w}</div>)}
                </div>
              )}
              {validation.errors?.length>0 && (
                <div style={css.errBox}>
                  <div style={{fontWeight:700,marginBottom:6}}>SYNTAX ERRORS</div>
                  {validation.errors.map((e,i)=><div key={i}>• {e}</div>)}
                </div>
              )}
              <div style={{padding:"8px 12px 4px",color:T.txt3,fontSize:10,fontWeight:700}}>
                AFFECTED OBJECTS ({validation.affectedObjects?.length||0})
              </div>
              {validation.affectedObjects?.length>0 ? (
                <ResultTable
                  data={validation.affectedObjects.map(o=>({
                    Object:o.affectedName, Type:o.affectedType,
                    Schema:o.schemaName, Depth:o.depth, Path:o.dependencyPath,
                  }))}
                  columns={["Object","Type","Schema","Depth","Path"]}
                />
              ) : (
                <div style={{padding:"10px 14px",color:T.txt3,fontSize:11}}>
                  No dependent objects found. Safe to apply.
                </div>
              )}
            </>
          )}
        </div>
      );

      // ── VERSION HISTORY ──────────────────────────────────
      case "history": return (
        <div>
          {(backupResult||rollbackResult) && (
            <div style={backupResult?.success||rollbackResult?.success ? css.okBox : css.errBox}>
              {backupResult && <span>💾 Backed up as <b>v{backupResult.versionNumber}</b> · {backupResult.message}</span>}
              {rollbackResult && <span>↩ {rollbackResult.message}</span>}
            </div>
          )}
          {versions.length===0 ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>🕐</div>
              Click <b>Backup</b> or <b>Versions</b> to load history
            </div>
          ) : (
            <>
              {versions.length>=2 && (
                <div style={{...css.row,background:T.bg1,borderBottom:`1px solid ${T.border}`}}>
                  <span style={{color:T.txt3,fontSize:10}}>COMPARE:</span>
                  <select style={css.select} value={diffVA}
                    onChange={e=>setDiffVA(Number(e.target.value))}>
                    {versions.map(v=><option key={v.versionNumber} value={v.versionNumber}>v{v.versionNumber}</option>)}
                  </select>
                  <span style={{color:T.txt3}}>→</span>
                  <select style={css.select} value={diffVB}
                    onChange={e=>setDiffVB(Number(e.target.value))}>
                    {versions.map(v=><option key={v.versionNumber} value={v.versionNumber}>v{v.versionNumber}</option>)}
                  </select>
                  <button style={css.btn(T.purple,"#120e2a")} onClick={handleDiff} disabled={loading.diff}>
                    {loading.diff?<Spin/>:"⟷"} Diff
                  </button>
                </div>
              )}
              {versions.map(v=>(
                <div key={v.id} style={css.card}>
                  <div style={{display:"flex",alignItems:"center",gap:10,
                    padding:"8px 12px",background:T.bg3,borderBottom:`1px solid ${T.border}`}}>
                    <span style={{color:T.gold,fontWeight:800,fontSize:13}}>v{v.versionNumber}</span>
                    <Pill label={v.objectType} color={T.purple}/>
                    <span style={{color:T.txt3,fontSize:10,marginLeft:"auto"}}>
                      {new Date(v.createdAt).toLocaleString()}
                    </span>
                    <button style={{...css.btn(T.gold,"#1a1200"),padding:"3px 9px",fontSize:10}}
                      onClick={()=>handleRollback(v.versionNumber)} disabled={loading.rollback}>
                      {loading.rollback?<Spin/>:"↩"} Restore
                    </button>
                    <button style={{...css.btn(T.txt2,T.bg3),padding:"3px 9px",fontSize:10}}
                      onClick={()=>setSqlQuery(v.scriptContent)}>⎘ Load</button>
                  </div>
                  <pre style={{margin:0,padding:"9px 12px",fontSize:10,color:T.txt3,
                    maxHeight:100,overflow:"auto",whiteSpace:"pre-wrap",wordBreak:"break-all"}}>
                    {v.scriptContent?.slice(0,400)}{v.scriptContent?.length>400?"…":""}
                  </pre>
                </div>
              ))}
            </>
          )}
        </div>
      );

      // ── DIFF ─────────────────────────────────────────────
      case "diff": return (
        <div>
          {!diffResult ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>⟷</div>
              Select two versions in <b>Versions</b> tab and click <b>Diff</b>
            </div>
          ) : (
            <>
              <div style={{display:"flex",gap:12,padding:"10px 14px",
                background:T.bg1,borderBottom:`1px solid ${T.border}`,alignItems:"center",flexWrap:"wrap"}}>
                <span style={{color:T.txt2,fontSize:10}}>v{diffResult.oldVersion} → v{diffResult.newVersion}</span>
                <SBadge s={diffResult.riskLevel||"LOW"}/>
                <span style={{color:T.green,fontSize:10}}>+{diffResult.linesAdded} added</span>
                <span style={{color:T.red,  fontSize:10}}>-{diffResult.linesRemoved} removed</span>
              </div>
              {diffResult.aiSummary && (
                <div style={css.warnBox}>
                  <div style={{fontWeight:700,fontSize:10,marginBottom:4,color:T.gold}}>AI SUMMARY</div>
                  <div>{diffResult.aiSummary}</div>
                  {diffResult.keyChanges?.map((k,i)=><div key={i} style={{marginTop:4,color:T.txt}}>• {k}</div>)}
                </div>
              )}
              <div style={{fontFamily:"'JetBrains Mono',monospace",fontSize:11,
                margin:10,border:`1px solid ${T.border}`,borderRadius:6,overflow:"auto",maxHeight:400}}>
                {diffResult.diff?.map((line,i)=>(
                  <div key={i} style={{
                    padding:"1px 12px",
                    background:line.type==="added"?"#0a2a1099":line.type==="removed"?"#2a0a0a99":"transparent",
                    color:line.type==="added"?T.green:line.type==="removed"?T.red:T.txt3,
                  }}>
                    {line.type==="added"?"+ ":line.type==="removed"?"- ":"  "}
                    {line.content}
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      );

      // ── RISK ─────────────────────────────────────────────
      case "risk": return (
        <div>
          {!riskResult ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>⚠</div>
              Click <b>Risk</b> to analyze the query
            </div>
          ) : (
            <>
              <div style={{display:"flex",alignItems:"center",gap:12,padding:"12px 14px",
                background:T.bg1,borderBottom:`1px solid ${T.border}`}}>
                <span style={{color:T.txt2,fontWeight:700,fontSize:11}}>RISK LEVEL</span>
                <SBadge s={riskResult.riskLevel||"UNKNOWN"}/>
              </div>
              <div style={{display:"grid",gridTemplateColumns:"1fr 1fr",gap:10,margin:10}}>
                <div style={css.card}>
                  <div style={{padding:"8px 12px",color:T.red,fontWeight:700,fontSize:10,
                    borderBottom:`1px solid ${T.border}`}}>⛔ RISKS</div>
                  <div style={{padding:"8px 12px"}}>
                    {(riskResult.risks||[]).map((r,i)=>(
                      <div key={i} style={{fontSize:11,color:"#fca5a5",marginBottom:6,
                        paddingLeft:8,borderLeft:`2px solid #7f1d1d`}}>{r}</div>
                    ))}
                    {!riskResult.risks?.length && <span style={{color:T.txt3}}>None identified</span>}
                  </div>
                </div>
                <div style={css.card}>
                  <div style={{padding:"8px 12px",color:T.green,fontWeight:700,fontSize:10,
                    borderBottom:`1px solid ${T.border}`}}>✅ RECOMMENDATIONS</div>
                  <div style={{padding:"8px 12px"}}>
                    {(riskResult.recommendations||[]).map((r,i)=>(
                      <div key={i} style={{fontSize:11,color:"#86efac",marginBottom:6,
                        paddingLeft:8,borderLeft:`2px solid #14401e`}}>{r}</div>
                    ))}
                    {!riskResult.recommendations?.length && <span style={{color:T.txt3}}>None</span>}
                  </div>
                </div>
              </div>
            </>
          )}
        </div>
      );

      // ── EXPLAIN ──────────────────────────────────────────
      case "explain": return (
        <div>
          {!explanation ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>💡</div>
              Click <b>Explain</b> for an AI breakdown
            </div>
          ) : (
            <div style={{margin:14}}>
              <div style={{color:T.cyan,fontWeight:700,fontSize:10,marginBottom:10}}>💡 AI EXPLANATION</div>
              <div style={{background:T.bg2,border:`1px solid ${T.border}`,borderRadius:7,
                padding:14,lineHeight:1.85,fontSize:12,color:T.txt,whiteSpace:"pre-wrap"}}>
                {explanation}
              </div>
            </div>
          )}
        </div>
      );

      // ── SCHEMA ───────────────────────────────────────────
      case "schema": return (
        <div>
          {!schema ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>🗃</div>
              Click <b>Schema</b> to extract database structure
            </div>
          ) : (
            <>
              <div style={{display:"flex",gap:14,padding:"8px 14px",
                background:T.bg1,borderBottom:`1px solid ${T.border}`,flexWrap:"wrap"}}>
                <span style={{color:T.txt2}}>DB: <b style={{color:T.gold}}>{schema.databaseName}</b></span>
                <span style={{color:T.txt2}}>Tables: <b style={{color:T.txt}}>{schema.tables?.length}</b></span>
                <span style={{color:T.txt2}}>Views: <b style={{color:T.txt}}>{schema.views?.length}</b></span>
                <span style={{color:T.txt2}}>Procedures: <b style={{color:T.txt}}>{schema.procedures?.length}</b></span>
              </div>
              {schema.tables?.map(tbl=>(
                <div key={tbl.name} style={css.card}>
                  <div style={{display:"flex",alignItems:"center",gap:10,padding:"7px 12px",
                    background:T.bg3,borderBottom:`1px solid ${T.border}`}}>
                    <span style={{color:T.blue,fontWeight:700}}>{tbl.schema}.{tbl.name}</span>
                    <Pill label="TABLE" color={T.blue}/>
                    <span style={{color:T.txt3,fontSize:10,marginLeft:"auto"}}>
                      {tbl.rowCount?.toLocaleString()} rows
                    </span>
                    <span style={{color:T.txt3,fontSize:10}}>
                      {tbl.columns?.length} cols · {tbl.indexes?.length} idx
                    </span>
                  </div>
                  <div style={{display:"flex",flexWrap:"wrap",gap:6,padding:"8px 12px"}}>
                    {tbl.columns?.map(c=>(
                      <span key={c.name} style={{
                        background:c.isPrimaryKey?"#1a1000":T.bg1,
                        border:`1px solid ${c.isPrimaryKey?T.gold:T.border}`,
                        color:c.isPrimaryKey?T.gold:T.txt2,
                        padding:"2px 7px",borderRadius:4,fontSize:10,
                      }}>
                        {c.isPrimaryKey?"🔑 ":""}{c.name}
                        <span style={{color:T.txt3}}> {c.dataType}{c.isNullable?"":"!"}</span>
                      </span>
                    ))}
                  </div>
                  {tbl.foreignKeys?.length>0 && (
                    <div style={{padding:"4px 12px 8px",fontSize:10,color:T.txt3}}>
                      {tbl.foreignKeys.map((fk,i)=>(
                        <span key={i} style={{marginRight:10}}>
                          FK: {fk.foreignKeyColumn} → {fk.referencedTable}.{fk.referencedColumn}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </>
          )}
        </div>
      );

      // ── CONNECTIONS ──────────────────────────────────────
      case "connections": return (
        <div>
          {connTestResult && (
            <div style={connTestResult.success?css.okBox:css.errBox}>
              {connTestResult.success?"✓":""} {connTestResult.message}
              {connTestResult.serverVersion && ` · ${connTestResult.serverVersion.slice(0,60)}`}
            </div>
          )}
          <div style={css.card}>
            <div style={{padding:"8px 12px",background:T.bg3,
              borderBottom:`1px solid ${T.border}`,color:T.txt2,fontWeight:700,fontSize:10}}>
              ＋ NEW CONNECTION
            </div>
            <div style={{padding:12,display:"grid",gridTemplateColumns:"1fr 1fr",gap:8}}>
              {[["name","Name"],["host","Host"],["databaseName","Database"],["username","Username"]].map(([f,l])=>(
                <label key={f} style={{display:"flex",flexDirection:"column",gap:4,fontSize:10,color:T.txt3}}>
                  {l}
                  <input style={css.input} value={connForm[f]}
                    onChange={e=>setConnForm(p=>({...p,[f]:e.target.value}))}/>
                </label>
              ))}
              <label style={{display:"flex",flexDirection:"column",gap:4,fontSize:10,color:T.txt3}}>
                Password
                <input style={css.input} type="password" value={connForm.password}
                  onChange={e=>setConnForm(p=>({...p,password:e.target.value}))}/>
              </label>
              <label style={{display:"flex",flexDirection:"column",gap:4,fontSize:10,color:T.txt3}}>
                Port
                <input style={css.input} type="number" value={connForm.port}
                  onChange={e=>setConnForm(p=>({...p,port:Number(e.target.value)}))}/>
              </label>
              <label style={{display:"flex",flexDirection:"column",gap:4,fontSize:10,color:T.txt3}}>
                Type
                <select style={css.select} value={connForm.databaseType}
                  onChange={e=>setConnForm(p=>({...p,databaseType:e.target.value}))}>
                  <option>SqlServer</option><option>MongoDB</option>
                </select>
              </label>
            </div>
            <div style={css.row}>
              <button style={css.btn(T.green,"#0a1e10")} onClick={handleSaveConn} disabled={loading.saveConn}>
                {loading.saveConn?<Spin/>:"＋"} Save Profile
              </button>
            </div>
          </div>

          {connections.length>0 && (
            <table style={{width:"100%",borderCollapse:"collapse",fontSize:11,margin:0}}>
              <thead><tr>
                {["Name","Type","Host","Database","Status","Actions"].map(h=><th key={h} style={css.th}>{h}</th>)}
              </tr></thead>
              <tbody>
                {connections.map(c=>(
                  <tr key={c.id} style={{background:"transparent"}}>
                    <td style={{...css.td,color:T.blue,fontWeight:700}}>{c.name}</td>
                    <td style={css.td}><Pill label={c.databaseType} color={T.cyan}/></td>
                    <td style={css.td}>{c.host}:{c.port}</td>
                    <td style={css.td}>{c.databaseName}</td>
                    <td style={css.td}>
                      <SBadge s={c.lastTestOk?"SUCCESS":"UNKNOWN"}/>
                    </td>
                    <td style={css.td}>
                      <button style={{...css.btn(T.green,"#0a1e10"),padding:"3px 8px",fontSize:10}}
                        onClick={()=>handleTestConn(c.id)} disabled={loading.testConn}>
                        {loading.testConn?<Spin/>:"▶"} Test
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {connections.length===0 && (
            <div style={{padding:30,textAlign:"center",color:T.txt3,fontSize:11}}>
              No saved connections. Add one above or click Load to refresh.
              <div style={{marginTop:10}}>
                <button style={css.btn(T.txt2,T.bg3)} onClick={handleLoadConnections}>
                  🔄 Load Connections
                </button>
              </div>
            </div>
          )}
        </div>
      );

      // ── AUDIT LOG ────────────────────────────────────────
      case "audit": return (
        <div>
          {auditLogs.length===0 ? (
            <div style={{padding:40,textAlign:"center",color:T.txt3}}>
              <div style={{fontSize:32,marginBottom:12}}>📋</div>
              Click <b>Audit</b> to load the activity log
            </div>
          ) : (
            <table style={{width:"100%",borderCollapse:"collapse",fontSize:11}}>
              <thead><tr>
                {["#","Action","Object","Type","Status","Model","Duration","Time"].map(h=>(
                  <th key={h} style={css.th}>{h}</th>
                ))}
              </tr></thead>
              <tbody>
                {auditLogs.map((log,i)=>(
                  <tr key={log.id} style={{background:i%2===0?"transparent":T.bg1}}>
                    <td style={{...css.td,color:T.txt3}}>{log.id}</td>
                    <td style={{...css.td,color:T.blue,fontWeight:700}}>{log.action}</td>
                    <td style={{...css.td,maxWidth:160}}>{log.objectName}</td>
                    <td style={css.td}><Pill label={log.objectType||"—"} color={T.purple}/></td>
                    <td style={css.td}><SBadge s={log.status||"—"}/></td>
                    <td style={{...css.td,color:T.txt3}}>{log.modelUsed||"—"}</td>
                    <td style={{...css.td,color:T.gold}}>{fmtMs(log.durationMs)}</td>
                    <td style={{...css.td,color:T.txt3,whiteSpace:"nowrap"}}>
                      {new Date(log.createdAt).toLocaleTimeString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      );

      default: return null;
    }
  };

  // ── Full render ───────────────────────────────────────────
  return (
    <div style={css.root}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600;700;800&display=swap');
        *{box-sizing:border-box}
        ::-webkit-scrollbar{width:5px;height:5px}
        ::-webkit-scrollbar-track{background:${T.bg0}}
        ::-webkit-scrollbar-thumb{background:${T.border};border-radius:3px}
        @keyframes kspin{to{transform:rotate(360deg)}}
        textarea::placeholder,input::placeholder{color:${T.txt3}}
        select option{background:${T.bg2}}
        button:hover{opacity:.82;transform:translateY(-1px)}
        button:active{transform:translateY(0)}
        button:disabled{opacity:.4;cursor:not-allowed;transform:none}
      `}</style>

      {/* Top bar */}
      <div style={css.topbar}>
        <div style={css.logo}>
          <span style={{fontSize:18}}>🦊</span> KITSUNE
          <span style={{color:T.txt3,fontSize:9,fontWeight:400,letterSpacing:".05em"}}>
            AI DATABASE INTELLIGENCE
          </span>
        </div>
        <span style={css.badge(T.blue)}>v2.0</span>

        <div style={{marginLeft:"auto",display:"flex",alignItems:"center",gap:8}}>
          <span style={{color:T.txt3,fontSize:10}}>MODEL</span>
          <select style={{...css.select,color:T.gold,borderColor:`${T.gold}55`,minWidth:180}}
            value={model} onChange={e=>setModel(e.target.value)}>
            {models.map(m=>(
              <option key={m.id} value={m.id}>
                {m.display_name}{m.available===false?" (offline)":""}
              </option>
            ))}
          </select>
          <span style={{width:7,height:7,borderRadius:"50%",background:T.green,flexShrink:0}}/>
        </div>
      </div>

      {/* Main */}
      <div style={css.main}>
        <LeftPane />
        <div style={css.rightPane}>
          <div style={css.tabBar}>
            {ALL_TABS.map(t=>(
              <div key={t.id} style={css.tab(activeTab===t.id)} onClick={()=>setActiveTab(t.id)}>
                {t.label}
              </div>
            ))}
            <div style={{marginLeft:"auto",padding:"8px 12px"}}>
              <button style={{...css.btn(T.txt2,T.bg1),padding:"3px 8px",fontSize:10}}
                onClick={handleLoadConnections}>
                🔌 Connections
              </button>
            </div>
          </div>
          <div style={css.panel}>
            <PanelContent />
          </div>
        </div>
      </div>

      {/* Status bar */}
      <div style={css.statusBar}>
        <span>🦊 KITSUNE v2.0.0</span>
        <span>Backend: {BACKEND}</span>
        <span>AI: {AI_SVC}</span>
        {Object.values(loading).some(Boolean) && (
          <span style={{color:T.gold}}>⟳ Processing…</span>
        )}
        <span style={{marginLeft:"auto",color:T.txt3}}>
          Model: <span style={{color:T.gold}}>
            {models.find(m=>m.id===model)?.display_name || model}
          </span>
        </span>
      </div>
    </div>
  );
}
