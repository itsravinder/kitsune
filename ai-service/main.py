# ============================================================
# KITSUNE – AI Service v4
# Enhanced SQL generation:
#   - Table detection via LLM
#   - Schema-aware generation (only relevant tables)
#   - In-memory schema cache per database
#   - Join inference from FK relationships
#   - Structured JSON output with deepmap
# Preserved: /api/generate endpoint (unchanged)
# ============================================================
from __future__ import annotations

import json, re, time
from typing import Any

import httpx
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

app = FastAPI(title="KITSUNE AI Service", version="4.0.0")
app.add_middleware(CORSMiddleware, allow_origins=["*"],
                   allow_methods=["*"], allow_headers=["*"])

OLLAMA_BASE  = "http://localhost:11434"
BACKEND_BASE = "http://localhost:5000"

MODELS: dict[str, dict[str, Any]] = {
    "sqlcoder": {
        "ollama_name" : "defog/sqlcoder",
        "display_name": "SQLCoder (Local)",
        "type"        : "local",
        "best_for"    : ["simple_select","joins","aggregations","filters"],
        "max_tokens"  : 2048,
        "temperature" : 0.05,
    },
    "qwen3-coder": {
        "ollama_name" : "qwen3-coder:480b-cloud",
        "display_name": "Qwen3 480B (Cloud)",
        "type"        : "cloud",
        "best_for"    : ["stored_procedures","functions","optimization",
                         "nosql","complex_cte","refactoring","risk_analysis"],
        "max_tokens"  : 8192,
        "temperature" : 0.1,
    },
}

# ── Schema cache: { database_name -> { tables_data, timestamp } } ──
_schema_cache: dict[str, dict] = {}
CACHE_TTL_SECONDS = 300   # 5 minutes per DB

# ── Pydantic models ───────────────────────────────────────────
class GenerateRequest(BaseModel):
    natural_language: str
    database_type   : str = "SqlServer"
    schema          : str | None = None
    model           : str = "auto"
    database_name   : str = ""

class GenerateResponse(BaseModel):
    generated_query : str
    model_used      : str
    display_name    : str
    explanation     : str
    confidence_score: float
    tokens_used     : int
    execution_ms    : float
    fallback_used   : bool = False
    # Enhanced fields (v4)
    tables_used     : list[str] = []
    deepmap         : str = ""
    schema_used     : str = ""

class ExplainRequest(BaseModel):
    query: str
    model: str = "auto"

class RiskRequest(BaseModel):
    query      : str
    object_type: str = ""

class ChangeSummaryRequest(BaseModel):
    old_script : str
    new_script : str
    object_name: str = ""
    model      : str = "auto"

class SchemaContextRequest(BaseModel):
    schema  : str
    question: str
    model   : str = "auto"

# ── Ollama helpers ────────────────────────────────────────────
def messages_to_prompt(messages: list[dict]) -> str:
    parts = []
    for m in messages:
        role    = m.get("role", "user")
        content = m.get("content", "")
        if role == "system":
            parts.append(f"Instructions:\n{content}")
        elif role == "user":
            parts.append(f"User:\n{content}")
        elif role == "assistant":
            parts.append(f"Assistant:\n{content}")
    parts.append("Assistant:")
    return "\n\n".join(parts)


async def call_ollama(model_key: str, messages: list[dict],
                       json_mode: bool = False) -> tuple[str, int]:
    cfg    = MODELS[model_key]
    prompt = messages_to_prompt(messages)
    payload: dict[str, Any] = {
        "model"  : cfg["ollama_name"],
        "prompt" : prompt,
        "stream" : False,
        "options": {"temperature": cfg["temperature"], "num_predict": cfg["max_tokens"]},
    }
    if json_mode:
        payload["format"] = "json"
    async with httpx.AsyncClient(timeout=180) as c:
        r = await c.post(f"{OLLAMA_BASE}/api/generate", json=payload)
        r.raise_for_status()
        d = r.json()
    return d.get("response", "").strip(), d.get("eval_count", 0)


async def call_with_fallback(preferred: str, fallback: str,
                              messages: list[dict],
                              json_mode: bool = False) -> tuple[str, int, str, bool]:
    for key, is_fb in [(preferred, False), (fallback, True)]:
        try:
            text, tok = await call_ollama(key, messages, json_mode)
            return text, tok, key, is_fb
        except Exception:
            if is_fb:
                raise
    raise RuntimeError("Both models failed")


def auto_route(text: str, db_type: str) -> tuple[str, str]:
    q = text.lower()
    complex_signals = [
        "stored procedure","sp_","usp_","function","trigger",
        "optimize","refactor","cte","recursive","dynamic sql",
        "pivot","cursor","transaction","change summary","explain",
    ]
    if db_type.lower() == "mongodb":
        return "qwen3-coder", "sqlcoder"
    for s in complex_signals:
        if s in q:
            return "qwen3-coder", "sqlcoder"
    return "sqlcoder", "qwen3-coder"


def strip_fences(text: str) -> str:
    text = re.sub(r"^```(?:sql|json|mongodb|python)?\s*", "", text, flags=re.IGNORECASE)
    return re.sub(r"\s*```$", "", text).strip()


def confidence_from_query(query: str) -> float:
    words = len(query.split())
    score = min(0.96, 0.55 + words * 0.012)
    if any(x in query.lower() for x in ["i cannot", "i'm unable", "as an ai"]):
        score = 0.20
    return round(score, 2)


# ── Schema cache helpers ──────────────────────────────────────
async def get_all_table_names(database_name: str) -> list[str]:
    """Fetch all table names from backend for a given DB. Cached."""
    cache_key = f"_names_{database_name}"
    cached = _schema_cache.get(cache_key)
    if cached and (time.time() - cached["ts"]) < CACHE_TTL_SECONDS:
        return cached["data"]
    try:
        db_param = f"?db={database_name}" if database_name else ""
        async with httpx.AsyncClient(timeout=10) as c:
            r = await c.get(f"{BACKEND_BASE}/api/schema/sqlserver{db_param}")
            r.raise_for_status()
            data = r.json()
            # Extract table names from schema response
            tables = [
                t.get("tableName") or t.get("TableName") or t.get("name") or ""
                for t in data.get("tables", [])
            ]
            tables = [t for t in tables if t]
            _schema_cache[cache_key] = {"data": tables, "ts": time.time()}
            return tables
    except Exception:
        return []


async def get_schema_for_tables(table_names: list[str],
                                 database_name: str) -> list[dict]:
    """Fetch compact schema for specific tables. Cached per DB+table combo."""
    if not table_names:
        return []

    cache_key = f"{database_name}::{','.join(sorted(table_names))}"
    cached = _schema_cache.get(cache_key)
    if cached and (time.time() - cached["ts"]) < CACHE_TTL_SECONDS:
        return cached["data"]

    try:
        async with httpx.AsyncClient(timeout=15) as c:
            r = await c.post(
                f"{BACKEND_BASE}/api/schema/for-query",
                json={"tableNames": table_names, "databaseName": database_name},
            )
            r.raise_for_status()
            data = r.json()
            tables = data.get("tables", [])
            _schema_cache[cache_key] = {"data": tables, "ts": time.time()}
            return tables
    except Exception:
        return []


def invalidate_cache(database_name: str) -> None:
    """Remove all cached entries for a database (called when DB changes)."""
    keys = [k for k in _schema_cache if database_name.lower() in k.lower()]
    for k in keys:
        del _schema_cache[k]


def build_schema_text(tables: list[dict]) -> str:
    """Format fetched schema into compact text for LLM prompt."""
    lines = []
    for t in tables:
        full = t.get("fullName") or f"{t.get('schema','dbo')}.{t.get('tableName','?')}"
        lines.append(f"\nTable: {full}")
        for col in t.get("columns", []):
            pk  = " [PK]" if col.get("isPK")       else ""
            nn  = " NOT NULL"  if not col.get("nullable") else ""
            ide = " IDENTITY"  if col.get("isIdentity")   else ""
            lines.append(f"  {col['name']} {col['type'].upper()}{pk}{ide}{nn}")
        for fk in t.get("foreignKeys", []):
            lines.append(f"  FK: {fk['join']}")
    return "\n".join(lines)


def build_deepmap(tables: list[dict]) -> str:
    """Build join relationship summary."""
    joins = []
    seen  = set()
    for t in tables:
        for fk in t.get("foreignKeys", []):
            j = fk.get("join", "")
            if j and j not in seen:
                seen.add(j)
                joins.append(f"  JOIN ON {j}")
    if not joins:
        return "No FK relationships found among selected tables."
    return "Join relationships:\n" + "\n".join(joins)


async def detect_relevant_tables(
    natural_language: str,
    all_tables: list[str],
    preferred_model: str,
) -> list[str]:
    """Use LLM to detect which tables are relevant to the request."""
    if not all_tables:
        return []

    table_list = ", ".join(all_tables)
    messages = [
        {"role": "system", "content": (
            "You are a database schema analyst. Given a user request and a list of table names, "
            "identify ONLY the tables needed to answer the request. "
            "Consider: direct mentions, semantic meaning (e.g. 'orders' → Orders table), "
            "and tables needed for joins (e.g. if Orders needs Customers for names). "
            "Return ONLY a JSON array of table names, nothing else. "
            "Example: [\"Orders\", \"Customers\", \"Products\"]"
        )},
        {"role": "user", "content": (
            f"Available tables: {table_list}\n\n"
            f"User request: {natural_language}\n\n"
            "Which tables are needed? Return JSON array only."
        )},
    ]
    try:
        raw, _ = await call_ollama(preferred_model, messages, json_mode=True)
        cleaned = strip_fences(raw)
        parsed  = json.loads(cleaned)
        if isinstance(parsed, list):
            # Validate: only return tables that actually exist
            valid = {t.lower(): t for t in all_tables}
            return [valid[p.lower()] for p in parsed if p.lower() in valid]
        # Handle {"tables": [...]} response shape
        if isinstance(parsed, dict):
            for key in ("tables", "table_names", "tableNames", "result"):
                if key in parsed and isinstance(parsed[key], list):
                    valid = {t.lower(): t for t in all_tables}
                    return [valid[p.lower()] for p in parsed[key] if p.lower() in valid]
    except Exception:
        pass

    # Fallback: keyword matching if LLM fails
    nl_lower = natural_language.lower()
    return [t for t in all_tables if t.lower() in nl_lower]


# ── Endpoints ─────────────────────────────────────────────────

@app.post("/generate", response_model=GenerateResponse)
async def generate(req: GenerateRequest):
    """
    Enhanced SQL generation pipeline:
    1. Get all table names for the database (cached)
    2. LLM detects which tables are relevant
    3. Fetch compact schema for those tables only (cached)
    4. Generate SQL with full column + FK context
    5. Return structured response with deepmap and tables_used
    """
    t0 = time.perf_counter()

    if req.model == "auto":
        preferred, fallback = auto_route(req.natural_language, req.database_type)
    else:
        preferred = req.model
        fallback  = "qwen3-coder" if req.model == "sqlcoder" else "sqlcoder"

    tables_used: list[str] = []
    schema_text             = ""
    deepmap_text            = ""

    # ── For SQL Server: intelligent schema detection ──────────
    if req.database_type.lower() != "mongodb" and req.database_name:
        try:
            # Step 1: Get all table names (cached)
            all_tables = await get_all_table_names(req.database_name)

            if all_tables:
                # Step 2: LLM detects relevant tables
                tables_used = await detect_relevant_tables(
                    req.natural_language, all_tables, preferred
                )

                # Fallback: if detection returns nothing, use all tables (max 20)
                if not tables_used:
                    tables_used = all_tables[:20]

                # Step 3: Fetch schema for detected tables only (cached)
                schema_data = await get_schema_for_tables(tables_used, req.database_name)

                if schema_data:
                    schema_text  = build_schema_text(schema_data)
                    deepmap_text = build_deepmap(schema_data)
        except Exception:
            pass  # Graceful degradation — generate without schema if backend unreachable

    # ── Use caller-supplied schema if no schema fetched ───────
    if not schema_text and req.schema:
        schema_text = req.schema

    # ── Build generation prompt ───────────────────────────────
    if req.database_type.lower() == "mongodb":
        system = (
            "You are an expert MongoDB query engineer. "
            "Return ONLY valid MongoDB aggregation pipeline JSON. "
            "No explanation, no markdown fences."
        )
        if schema_text:
            system += f"\n\nDatabase schema:\n{schema_text}"

        messages = [
            {"role": "system", "content": system},
            {"role": "user",   "content":
                f"Database: {req.database_name or 'target database'}\n"
                f"Request: {req.natural_language}"},
        ]
    else:
        # SQL Server — rich schema-aware prompt
        schema_block = f"\n\n--- DATABASE SCHEMA ---\n{schema_text}\n--- END SCHEMA ---" \
                       if schema_text else ""

        join_block = f"\n\n--- JOIN RELATIONSHIPS ---\n{deepmap_text}\n--- END JOINS ---" \
                     if deepmap_text and "No FK" not in deepmap_text else ""

        system = (
            "You are an expert Microsoft SQL Server (T-SQL) query engineer. "
            "RULES:\n"
            "- Use ONLY tables and columns listed in the schema below\n"
            "- Do NOT guess column or table names\n"
            "- Use JOIN relationships shown below — do not invent joins\n"
            "- Always use schema prefix (dbo.TableName)\n"
            "- Use table aliases for readability\n"
            "- Add TOP 1000 to SELECT queries unless user specifies a limit\n"
            "- ORDER BY primary key or most relevant column\n"
            "- Return ONLY valid T-SQL, no explanation, no markdown fences"
            + schema_block
            + join_block
        )

        messages = [
            {"role": "system", "content": system},
            {"role": "user",   "content":
                f"Database: {req.database_name or 'target database'}\n"
                f"Tables to use: {', '.join(tables_used) if tables_used else 'auto-detect'}\n"
                f"Request: {req.natural_language}"},
        ]

    raw, tokens, model_used, fb = await call_with_fallback(preferred, fallback, messages)
    cleaned = strip_fences(raw)
    ms      = (time.perf_counter() - t0) * 1000

    return GenerateResponse(
        generated_query  = cleaned,
        model_used       = model_used,
        display_name     = MODELS[model_used]["display_name"],
        explanation      = (
            f"Generated by {MODELS[model_used]['display_name']}. "
            f"Used {len(tables_used)} table(s): {', '.join(tables_used)}."
            if tables_used else
            f"Generated by {MODELS[model_used]['display_name']}."
        ),
        confidence_score = confidence_from_query(cleaned),
        tokens_used      = tokens,
        execution_ms     = round(ms, 1),
        fallback_used    = fb,
        tables_used      = tables_used,
        deepmap          = deepmap_text,
        schema_used      = schema_text[:500] if schema_text else "",
    )


@app.get("/models")
async def list_models():
    results = []
    for key, cfg in MODELS.items():
        available = False
        try:
            async with httpx.AsyncClient(timeout=4) as c:
                r = await c.get(f"{OLLAMA_BASE}/api/tags")
                if r.status_code == 200:
                    available = any(
                        m.get("name", "").startswith(cfg["ollama_name"].split(":")[0])
                        for m in r.json().get("models", [])
                    )
        except Exception:
            pass
        results.append({"id": key, "display_name": cfg["display_name"],
                         "type": cfg["type"], "best_for": cfg["best_for"],
                         "available": available})
    return results


@app.post("/detect-tables")
async def detect_tables(req: dict):
    """
    POST /detect-tables
    { natural_language, database_name, model? }
    Returns: { tables_detected, all_tables_count }
    Useful for previewing what tables would be used before generating.
    """
    nl      = req.get("natural_language", "")
    db_name = req.get("database_name", "")
    model   = req.get("model", "sqlcoder")

    all_tables  = await get_all_table_names(db_name)
    detected    = await detect_relevant_tables(nl, all_tables, model)
    return {"tables_detected": detected, "all_tables_count": len(all_tables)}


@app.post("/cache/invalidate")
async def invalidate_schema_cache(req: dict):
    """POST /cache/invalidate { database_name }  — clears cached schema for a DB."""
    db = req.get("database_name", "")
    if db:
        invalidate_cache(db)
        return {"message": f"Cache cleared for database '{db}'"}
    _schema_cache.clear()
    return {"message": "All schema caches cleared"}


@app.get("/cache/status")
async def cache_status():
    """GET /cache/status — shows what is currently cached."""
    entries = []
    now = time.time()
    for k, v in _schema_cache.items():
        age = int(now - v["ts"])
        entries.append({"key": k, "age_seconds": age, "expired": age > CACHE_TTL_SECONDS})
    return {"entries": entries, "count": len(entries), "ttl_seconds": CACHE_TTL_SECONDS}


@app.post("/explain")
async def explain(req: ExplainRequest):
    preferred = "qwen3-coder" if req.model == "auto" else req.model
    messages  = [
        {"role": "system", "content": (
            "You are a SQL expert tutor. Explain the SQL query in clear plain English. "
            "Cover: what it does, how JOINs/filters work, aggregations, "
            "performance concerns, and what the result set looks like.")},
        {"role": "user", "content": req.query},
    ]
    raw, _ = await call_ollama(preferred, messages)
    return {"explanation": raw, "model_used": preferred}


@app.post("/risk")
async def risk(req: RiskRequest):
    messages = [
        {"role": "system", "content": (
            "You are a SQL safety auditor. Analyze for:\n"
            "1. Data loss risk (DELETE/UPDATE without WHERE, DROP, TRUNCATE)\n"
            "2. Performance risk (full scans, missing indexes, Cartesian joins)\n"
            "3. Security risk (injection, excessive permissions)\n"
            "4. Logic errors (NULL handling, always-true conditions)\n\n"
            "Return ONLY JSON: riskLevel (LOW|MEDIUM|HIGH|CRITICAL), "
            "risks (array), recommendations (array). No markdown.")},
        {"role": "user", "content": req.query},
    ]
    raw, _ = await call_ollama("qwen3-coder", messages, json_mode=True)
    try:
        return json.loads(strip_fences(raw))
    except Exception:
        return {"riskLevel": "UNKNOWN", "risks": ["Parse failed."], "recommendations": []}


@app.post("/summarize-change")
async def summarize_change(req: ChangeSummaryRequest):
    messages = [
        {"role": "system", "content": (
            "You are a SQL code reviewer. Compare OLD and NEW versions and produce "
            "a change summary as JSON with keys:\n"
            "- summary (string, 2-3 sentences)\n"
            "- key_changes (array of strings)\n"
            "- risk_level (LOW|MEDIUM|HIGH)\n"
            "- breaking_changes (array of strings)\n"
            "No markdown, no preamble.")},
        {"role": "user", "content":
            f"Object: {req.object_name}\n\n"
            f"=== OLD VERSION ===\n{req.old_script}\n\n"
            f"=== NEW VERSION ===\n{req.new_script}"},
    ]
    preferred = "qwen3-coder" if req.model == "auto" else req.model
    raw, _    = await call_ollama(preferred, messages, json_mode=True)
    try:
        return json.loads(strip_fences(raw))
    except Exception:
        return {"summary": "Unavailable.", "key_changes": [],
                "risk_level": "UNKNOWN", "breaking_changes": []}


@app.post("/schema-context")
async def schema_context(req: SchemaContextRequest):
    preferred, fallback = auto_route(req.question, "SqlServer")
    messages = [
        {"role": "system", "content":
            f"You are a database expert. Use this schema as context:\n\n{req.schema}"},
        {"role": "user", "content": req.question},
    ]
    raw, tok, model_used, _ = await call_with_fallback(preferred, fallback, messages)
    return {"answer": raw, "model_used": model_used, "tokens_used": tok}


@app.get("/health")
async def health():
    ollama_ok, models_avail = False, []
    try:
        async with httpx.AsyncClient(timeout=3) as c:
            r = await c.get(f"{OLLAMA_BASE}/api/tags")
            if r.status_code == 200:
                ollama_ok    = True
                models_avail = [m["name"] for m in r.json().get("models", [])]
    except Exception:
        pass
    return {
        "status"           : "ok",
        "version"          : "4.0.0",
        "ollama"           : "connected" if ollama_ok else "unreachable",
        "models_available" : models_avail,
        "registered_models": list(MODELS.keys()),
        "ollama_endpoint"  : f"{OLLAMA_BASE}/api/generate",
        "schema_cache_size": len(_schema_cache),
    }
