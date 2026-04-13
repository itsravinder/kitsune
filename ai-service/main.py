# ============================================================
# KITSUNE – AI Service v5
# Key changes from v4:
#   - Dynamic model routing: any Ollama model name works
#   - Structured JSON output: query/explanation/schema/deepmap/confidence
#   - Robust error handling: timeout 120s, 1 retry, meaningful messages
#   - Confirmation logging (returned in response for UI display)
#   - Schema-aware generation with full column+FK context
# Preserved: all existing endpoints, routes, cache logic
# ============================================================
from __future__ import annotations

import json, re, time, logging
from typing import Any

import httpx
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

logger = logging.getLogger("kitsune")
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

app = FastAPI(title="KITSUNE AI Service", version="5.0.0")
app.add_middleware(CORSMiddleware, allow_origins=["*"],
                   allow_methods=["*"], allow_headers=["*"])

OLLAMA_BASE  = "http://localhost:11434"
BACKEND_BASE = "http://localhost:5000"

# ── Static model registry (local + cloud defaults) ────────────
# User-selected models bypass this — any ollama model name is accepted
MODELS: dict[str, dict[str, Any]] = {
    "auto": {
        "ollama_name" : "auto",
        "display_name": "Auto-Route",
        "type"        : "system",
        "max_tokens"  : 2048,
        "temperature" : 0.05,
    },
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
        "best_for"    : ["stored_procedures","complex_cte","refactoring"],
        "max_tokens"  : 8192,
        "temperature" : 0.1,
    },
}

# ── Schema cache ──────────────────────────────────────────────
_schema_cache: dict[str, dict] = {}
CACHE_TTL_SECONDS = 300


# ── Pydantic models ───────────────────────────────────────────
class GenerateRequest(BaseModel):
    natural_language: str
    database_type   : str = "SqlServer"
    schema          : str | None = None
    model           : str = "auto"
    database_name   : str = ""

class GenerateResponse(BaseModel):
    # Core SQL output
    generated_query : str
    query           : str                # alias — UI reads this
    # Model info
    model_used      : str
    display_name    : str
    confidence_score: float
    tokens_used     : int
    execution_ms    : float
    fallback_used   : bool = False
    # Structured tab content
    explanation     : str = ""           # ExplainTab
    schema          : str = ""           # SchemaTab (text)
    deepmap         : str = ""           # DeepmapTab (tree text)
    tables_used     : list[str] = []
    schema_used     : str = ""
    confidence      : str = "medium"     # high / medium / low (string label)
    # UI confirmation log
    generation_log  : list[str] = []     # shown in genMeta / notify

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


# ── Model resolution ──────────────────────────────────────────

def resolve_model_name(model_key: str) -> tuple[str, str, str]:
    """
    Returns (ollama_model_name, display_name, model_type).
    Handles both registry keys ('sqlcoder') and raw Ollama names ('llama3:8b').
    """
    if model_key in MODELS:
        cfg = MODELS[model_key]
        return cfg["ollama_name"], cfg["display_name"], cfg.get("type", "local")
    # Raw Ollama model name supplied by user (e.g. 'sqlcoder:15b', 'gpt-oss:20b')
    display = model_key.split(":")[0].replace("-", " ").title()
    mtype   = "cloud" if any(x in model_key.lower() for x in ["cloud","gpt","openai"]) else "local"
    return model_key, display, mtype


def get_model_params(model_key: str) -> tuple[int, float]:
    """Returns (max_tokens, temperature) for a model key."""
    if model_key in MODELS:
        return MODELS[model_key].get("max_tokens", 2048), MODELS[model_key].get("temperature", 0.1)
    return 2048, 0.05


def auto_route(text: str, db_type: str) -> str:
    """Pick best model key for the request. Returns MODELS key."""
    if db_type.lower() == "mongodb":
        return "qwen3-coder"
    complex_signals = [
        "stored procedure","sp_","usp_","function","trigger",
        "optimize","refactor","cte","recursive","dynamic sql",
        "pivot","cursor","transaction",
    ]
    q = text.lower()
    for s in complex_signals:
        if s in q:
            return "qwen3-coder"
    return "sqlcoder"


# ── Ollama call — dynamic model, timeout, 1 retry ─────────────

async def call_ollama_model(
    ollama_model: str,
    prompt: str,
    max_tokens: int = 2048,
    temperature: float = 0.05,
    json_mode: bool = False,
    timeout: int = 120,
) -> tuple[str, int]:
    """
    Call Ollama /api/generate with any model name.
    Retries once on failure. Raises on both failures.
    """
    payload: dict[str, Any] = {
        "model"  : ollama_model,
        "prompt" : prompt,
        "stream" : False,
        "options": {"temperature": temperature, "num_predict": max_tokens},
    }
    if json_mode:
        payload["format"] = "json"

    last_err: Exception | None = None
    for attempt in range(2):  # 1 retry
        try:
            async with httpx.AsyncClient(timeout=timeout) as c:
                r = await c.post(f"{OLLAMA_BASE}/api/generate", json=payload)
                r.raise_for_status()
                d = r.json()
                text = d.get("response", "").strip()
                tokens = d.get("eval_count", 0)
                if not text:
                    raise ValueError("Empty response from Ollama")
                return text, tokens
        except Exception as e:
            last_err = e
            logger.warning("Ollama attempt %d failed for %s: %s", attempt + 1, ollama_model, e)
            if attempt == 0:
                await asyncio.sleep(1)  # brief pause before retry

    raise RuntimeError(f"Ollama model '{ollama_model}' failed after 2 attempts: {last_err}")


def messages_to_prompt(messages: list[dict]) -> str:
    """Convert messages array to flat prompt for /api/generate."""
    parts = []
    for m in messages:
        role, content = m.get("role", "user"), m.get("content", "")
        if role == "system":
            parts.append(f"Instructions:\n{content}")
        elif role == "user":
            parts.append(f"User:\n{content}")
        elif role == "assistant":
            parts.append(f"Assistant:\n{content}")
    parts.append("Assistant:")
    return "\n\n".join(parts)


async def call_with_messages(
    model_key: str,
    messages: list[dict],
    json_mode: bool = False,
    fallback_key: str | None = None,
) -> tuple[str, int, str, bool]:
    """
    Call Ollama with messages, with optional fallback to another model.
    Returns (text, tokens, model_key_used, fallback_used).
    """
    ollama_name, _, _ = resolve_model_name(model_key)
    max_tok, temp     = get_model_params(model_key)
    prompt            = messages_to_prompt(messages)

    try:
        text, tokens = await call_ollama_model(ollama_name, prompt, max_tok, temp, json_mode)
        return text, tokens, model_key, False
    except Exception as primary_err:
        logger.warning("Primary model %s failed: %s", model_key, primary_err)
        if fallback_key and fallback_key != model_key:
            fb_name, _, _ = resolve_model_name(fallback_key)
            fb_tok, fb_temp = get_model_params(fallback_key)
            try:
                text, tokens = await call_ollama_model(fb_name, prompt, fb_tok, fb_temp, json_mode)
                return text, tokens, fallback_key, True
            except Exception as fb_err:
                logger.error("Fallback model %s also failed: %s", fallback_key, fb_err)
                raise RuntimeError(
                    f"Both models failed.\n"
                    f"  Primary ({model_key}): {primary_err}\n"
                    f"  Fallback ({fallback_key}): {fb_err}"
                )
        raise RuntimeError(f"Model {model_key} failed: {primary_err}")


# ── asyncio import (needed for sleep in retry) ────────────────
import asyncio


# ── Schema helpers (unchanged from v4) ───────────────────────

async def get_all_table_names(database_name: str) -> list[str]:
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
            tables = [
                t.get("tableName") or t.get("TableName") or t.get("name") or ""
                for t in data.get("tables", [])
            ]
            tables = [t for t in tables if t]
            _schema_cache[cache_key] = {"data": tables, "ts": time.time()}
            return tables
    except Exception:
        return []


async def get_schema_for_tables(table_names: list[str], database_name: str) -> list[dict]:
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
    keys = [k for k in _schema_cache if database_name.lower() in k.lower()]
    for k in keys:
        del _schema_cache[k]


def build_schema_text(tables: list[dict]) -> str:
    """Compact schema text for LLM prompt."""
    lines = []
    for t in tables:
        full = t.get("fullName") or f"{t.get('schema','dbo')}.{t.get('tableName','?')}"
        lines.append(f"\nTable: {full}")
        for col in t.get("columns", []):
            pk  = " [PK]"      if col.get("isPK")       else ""
            nn  = " NOT NULL"  if not col.get("nullable") else ""
            ide = " IDENTITY"  if col.get("isIdentity")  else ""
            lines.append(f"  {col['name']} {col['type'].upper()}{pk}{ide}{nn}")
        for fk in t.get("foreignKeys", []):
            lines.append(f"  FK: {fk['join']}")
    return "\n".join(lines)


def build_deepmap_tree(tables: list[dict], tables_used: list[str]) -> str:
    """
    Build a tree-format dependency map:
      Step 1: Base table (TableA)
        ├─ Columns: col1, col2
      Step 2: JOIN TableB ON TableA.FK = TableB.PK
        ├─ Columns: col3, col4
      Step 3: Apply filter / sort
    """
    if not tables:
        return "No schema data available for dependency mapping."

    lines = ["📊 Data Flow & Dependency Map", ""]

    # Map FK relationships between tables
    fk_map: dict[str, list[str]] = {}  # table → [join expressions]
    for t in tables:
        name = t.get("tableName", "")
        for fk in t.get("foreignKeys", []):
            j = fk.get("join", "")
            if j:
                fk_map.setdefault(name, []).append(j)

    base = tables[0] if tables else {}
    base_name = base.get("tableName", "?")
    base_full = base.get("fullName", base_name)

    lines.append(f"Step 1: Base Table — {base_full}")
    base_cols = [c["name"] for c in base.get("columns", [])[:6]]
    if base_cols:
        lines.append(f"  ├─ Columns: {', '.join(base_cols)}" + (" …" if len(base.get("columns",[])) > 6 else ""))
    lines.append(f"  └─ Rows: {base.get('rowCount', '?')}")

    for i, t in enumerate(tables[1:], 2):
        t_name = t.get("tableName", "?")
        t_full = t.get("fullName", t_name)
        joins  = fk_map.get(base_name, []) + fk_map.get(t_name, [])
        join_desc = next((j for j in joins if t_name in j or base_name in j), "")

        lines.append(f"\nStep {i}: JOIN — {t_full}")
        if join_desc:
            lines.append(f"  ├─ ON: {join_desc}")
        t_cols = [c["name"] for c in t.get("columns", [])[:5]]
        if t_cols:
            lines.append(f"  └─ Columns: {', '.join(t_cols)}" + (" …" if len(t.get("columns",[])) > 5 else ""))

    step = len(tables) + 1
    lines.append(f"\nStep {step}: Apply Filters / Ordering")
    lines.append(f"  └─ TOP N rows, ORDER BY primary key")

    lines.append(f"\nStep {step + 1}: Result Set")
    all_used = ", ".join(tables_used) if tables_used else ", ".join(t.get("tableName","") for t in tables)
    lines.append(f"  └─ Tables used: {all_used}")

    # Add FK dependency summary
    if fk_map:
        lines.append("\n🔗 Foreign Key Dependencies")
        for tbl, joins_list in fk_map.items():
            for j in joins_list:
                lines.append(f"  {tbl} ──FK──▶ {j.split('=')[1].strip().split('.')[0] if '=' in j else '?'}")
                lines.append(f"    └─ ON {j}")

    return "\n".join(lines)


async def detect_relevant_tables(
    natural_language: str,
    all_tables: list[str],
    model_key: str,
) -> list[str]:
    if not all_tables:
        return []

    table_list = ", ".join(all_tables)
    messages = [
        {"role": "system", "content": (
            "You are a database schema analyst. Given a user request and table names, "
            "identify ONLY the tables needed. Consider semantic meaning and required joins. "
            "Return ONLY a JSON array of table names. Example: [\"Orders\", \"Customers\"]"
        )},
        {"role": "user", "content": (
            f"Available tables: {table_list}\n\n"
            f"User request: {natural_language}\n\n"
            "Which tables are needed? Return JSON array only."
        )},
    ]
    try:
        ollama_name, _, _ = resolve_model_name(model_key)
        max_tok, temp = get_model_params(model_key)
        prompt = messages_to_prompt(messages)
        raw, _ = await call_ollama_model(ollama_name, prompt, max_tok, temp, json_mode=True, timeout=30)
        cleaned = strip_fences(raw)
        parsed  = json.loads(cleaned)
        valid   = {t.lower(): t for t in all_tables}
        if isinstance(parsed, list):
            return [valid[p.lower()] for p in parsed if p.lower() in valid]
        if isinstance(parsed, dict):
            for key in ("tables", "table_names", "tableNames", "result"):
                if key in parsed and isinstance(parsed[key], list):
                    return [valid[p.lower()] for p in parsed[key] if p.lower() in valid]
    except Exception:
        pass
    # Keyword fallback
    nl_lower = natural_language.lower()
    return [t for t in all_tables if t.lower() in nl_lower]


def strip_fences(text: str) -> str:
    text = re.sub(r"^```(?:sql|json|mongodb|python)?\s*", "", text, flags=re.IGNORECASE)
    return re.sub(r"\s*```$", "", text).strip()


def confidence_label(score: float) -> str:
    if score >= 0.80: return "high"
    if score >= 0.55: return "medium"
    return "low"


def confidence_from_query(query: str) -> float:
    words = len(query.split())
    score = min(0.96, 0.55 + words * 0.012)
    if any(x in query.lower() for x in ["i cannot", "i'm unable", "as an ai"]):
        score = 0.20
    return round(score, 2)


def build_schema_summary(tables: list[dict], tables_used: list[str]) -> str:
    """Human-readable schema summary for the Schema tab."""
    lines = ["📋 Tables & Columns Used in Query", ""]
    for t in tables:
        full  = t.get("fullName") or t.get("tableName", "?")
        cols  = t.get("columns", [])
        fks   = t.get("foreignKeys", [])
        lines.append(f"▸ {full}")
        for col in cols:
            pk  = " 🔑" if col.get("isPK") else ""
            ide = " AUTO" if col.get("isIdentity") else ""
            nn  = ""      if col.get("nullable") else " NN"
            lines.append(f"    {col['name']} ({col['type'].upper()}{pk}{ide}{nn})")
        for fk in fks:
            lines.append(f"    🔗 FK: {fk.get('join','')}")
        lines.append("")
    if not tables:
        lines.append("(No schema data available)")
    return "\n".join(lines)


# ── Main generate endpoint ────────────────────────────────────

@app.post("/generate", response_model=GenerateResponse)
async def generate(req: GenerateRequest):
    t0  = time.perf_counter()
    log: list[str] = []   # confirmation messages returned to UI

    # ── 1. Resolve model ──────────────────────────────────────
    if req.model in ("auto", ""):
        model_key    = auto_route(req.natural_language, req.database_type)
        fallback_key = "qwen3-coder" if model_key == "sqlcoder" else "sqlcoder"
    else:
        model_key    = req.model
        fallback_key = "sqlcoder" if model_key != "sqlcoder" else "qwen3-coder"

    ollama_name, display_name, model_type = resolve_model_name(model_key)
    log.append(f"Model: {display_name} ({model_type}) — {ollama_name}")
    logger.info("[GENERATE] model=%s ollama=%s type=%s db=%s", model_key, ollama_name, model_type, req.database_name)

    tables_used: list[str] = []
    schema_data: list[dict] = []
    schema_text              = ""

    # ── 2. Schema detection (SQL Server only) ─────────────────
    if req.database_type.lower() != "mongodb" and req.database_name:
        try:
            all_tables = await get_all_table_names(req.database_name)
            log.append(f"Available tables: {len(all_tables)}")

            if all_tables:
                tables_used = await detect_relevant_tables(
                    req.natural_language, all_tables, model_key
                )
                if not tables_used:
                    tables_used = all_tables[:15]
                    log.append(f"Table detection fallback — using first {len(tables_used)} tables")
                else:
                    log.append(f"Tables detected: {', '.join(tables_used)}")

                schema_data = await get_schema_for_tables(tables_used, req.database_name)
                if schema_data:
                    schema_text = build_schema_text(schema_data)
                    log.append(f"Schema sent to LLM: {', '.join(tables_used)}")
                    logger.info("[GENERATE] Schema for tables: %s", tables_used)
        except Exception as e:
            log.append(f"Schema fetch skipped: {e}")

    if not schema_text and req.schema:
        schema_text = req.schema
        log.append("Using caller-supplied schema")

    # ── 3. Determine if model is local or cloud ───────────────
    # Local models (SQLCoder, gpt-oss, llama, etc.) cannot reliably
    # produce structured JSON. Use a plain SQL prompt for them, then
    # build explanation/schema/deepmap ourselves from the schema data.
    # Cloud models (qwen3-coder:*-cloud) can follow JSON instructions.
    _, _, model_type = resolve_model_name(model_key)
    use_json_prompt = (model_type == "cloud" or "cloud" in model_key.lower())

    # ── 4. Build FK join lines (used in both prompt styles) ───
    fk_lines: list[str] = []
    if schema_data:
        seen_fk: set[str] = set()
        for t in schema_data:
            for fk in t.get("foreignKeys", []):
                j = fk.get("join", "")
                if j and j not in seen_fk:
                    seen_fk.add(j)
                    fk_lines.append(j)

    schema_block = (
        f"\n\nDATABASE SCHEMA:\n{schema_text}"
        if schema_text else ""
    )
    join_block = (
        "\n\nFOREIGN KEY JOINS (use these exactly):\n" + "\n".join(f"  {j}" for j in fk_lines)
        if fk_lines else ""
    )

    if req.database_type.lower() == "mongodb":
        # MongoDB always uses JSON prompt
        system_prompt = (
            "You are an expert MongoDB query engineer. "
            "Return ONLY a valid MongoDB aggregation pipeline JSON array. "
            "No explanation, no markdown."
        )
        if schema_text:
            system_prompt += f"\n\nSchema:\n{schema_text}"
        use_json_prompt = False  # MongoDB: just return the pipeline

    elif use_json_prompt:
        # CLOUD model — can handle structured JSON output
        system_prompt = (
            "You are an expert Microsoft SQL Server (T-SQL) engineer.\n"
            "Generate a T-SQL query and return ONLY valid JSON (no markdown, no extra text):\n"
            '{"query":"SELECT...","explanation":"plain English","schema":"tables+columns used","deepmap":"Step 1: base\nStep 2: join\nStep 3: filter","tables_used":["T1"],"confidence":"high"}'
            + schema_block + join_block
        )
    else:
        # LOCAL model — ask ONLY for SQL, nothing else
        # Local models (SQLCoder, gpt-oss, llama) fail when asked for JSON.
        # We build explanation/schema/deepmap ourselves from the schema data.
        table_list_str = "\n".join(
            f"  {t.get('fullName', t.get('tableName',''))}" for t in schema_data
        ) if schema_data else "  (auto-detect from schema)"

        # SQLCoder format: the model is trained to complete from "### SQL Query:"
        # Do NOT wrap in system/user roles — use the flat prompt format directly.
        # This is what prevents gpt-oss/sqlcoder from returning their own reasoning.
        fk_section = ""
        if fk_lines:
            fk_section = "\n-- Foreign Key Relationships:\n" + "\n".join(
                f"-- {j}" for j in fk_lines
            )

        system_prompt = (
            "### Instructions:\n"
            "Your task is to convert a question into a SQL query, given a MS SQL Server database schema.\n"
            "Adhere to these rules:\n"
            "- Use ONLY tables and columns defined in the schema below.\n"
            "- Do NOT guess column or table names.\n"
            "- Use dbo.TableName prefix for all table references.\n"
            "- For JOINs, use the foreign key relationships shown.\n"
            "- Add TOP 1000 to SELECT unless specific row count requested.\n"
            "- Output ONLY the SQL query. No explanation. No JSON. No markdown.\n"
            "\n### Database Schema:\n"
            f"-- Database: {req.database_name or 'target'}\n"
            + (schema_text if schema_text else "-- (schema not available)")
            + fk_section
            + f"\n\n### Question:\n{req.natural_language}\n"
            + "\n### SQL Query:\n"
        )
        # For SQLCoder, send as a single flat prompt (no role structure)
        messages = [{"role": "user", "content": system_prompt}]

    # ── 5. Call LLM with retry + fallback ─────────────────────
    raw = ""
    tokens = 0
    model_used_key = model_key
    fallback_used  = False

    try:
        raw, tokens, model_used_key, fallback_used = await call_with_messages(
            model_key, messages,
            json_mode=use_json_prompt,   # only request JSON from cloud models
            fallback_key=fallback_key
        )
        log.append(f"LLM response received ({tokens} tokens)")
    except RuntimeError as e:
        # Both models failed — return meaningful error, not "failed to fetch"
        err_msg = str(e)
        log.append(f"⚠ LLM call failed: {err_msg}")
        logger.error("[GENERATE] All models failed: %s", err_msg)

        # Return a structured error response (not a crash)
        return GenerateResponse(
            generated_query  = f"-- Error: {err_msg[:200]}",
            query            = f"-- Error: {err_msg[:200]}",
            model_used       = model_key,
            display_name     = display_name,
            explanation      = f"Query generation failed. {err_msg[:300]}",
            schema           = schema_text[:500] if schema_text else "",
            deepmap          = "",
            confidence_score = 0.0,
            confidence       = "low",
            tokens_used      = 0,
            execution_ms     = round((time.perf_counter() - t0) * 1000, 1),
            fallback_used    = True,
            tables_used      = tables_used,
            schema_used      = schema_text[:300] if schema_text else "",
            generation_log   = log,
        )

    # ── 6. Parse response ────────────────────────────────────
    cleaned          = strip_fences(raw)
    query_sql        = cleaned
    explanation_text = ""
    schema_summary   = ""
    deepmap_text     = ""
    confidence_str   = "medium"
    parsed_tables    = tables_used

    if use_json_prompt:
        # Cloud model returned JSON — parse it
        try:
            parsed = json.loads(cleaned)
            query_sql        = parsed.get("query", cleaned).strip()
            explanation_text = parsed.get("explanation", "").strip()
            schema_summary   = parsed.get("schema", "").strip()
            deepmap_text     = parsed.get("deepmap", "").strip()
            confidence_str   = parsed.get("confidence", "medium").lower()
            raw_tables       = parsed.get("tables_used", [])
            if raw_tables:
                parsed_tables = raw_tables
            log.append("✓ Structured JSON parsed")
        except (json.JSONDecodeError, ValueError):
            log.append("⚠ JSON parse failed — using raw response as SQL")
            query_sql = cleaned
    else:
        # Local model returned plain SQL — use it directly
        query_sql = cleaned
        log.append("✓ SQL extracted from local model response")

    # ── 6. Build rich tab content if LLM didn't return it ─────
    if not explanation_text:
        t_list = ", ".join(parsed_tables) if parsed_tables else "unknown tables"
        explanation_text = (
            f"This query retrieves data from {t_list} in the {req.database_name or 'target'} database. "
            f"Generated by {display_name}."
        )

    if not schema_summary and schema_data:
        schema_summary = build_schema_summary(schema_data, parsed_tables)

    if not deepmap_text and schema_data:
        deepmap_text = build_deepmap_tree(schema_data, parsed_tables)

    ms           = round((time.perf_counter() - t0) * 1000, 1)
    conf_score   = confidence_from_query(query_sql)
    used_name    = resolve_model_name(model_used_key)[1]

    log.append(f"Done in {ms}ms · Confidence: {confidence_str}")

    return GenerateResponse(
        generated_query  = query_sql,
        query            = query_sql,
        model_used       = model_used_key,
        display_name     = used_name,
        explanation      = explanation_text,
        schema           = schema_summary,
        deepmap          = deepmap_text,
        confidence_score = conf_score,
        confidence       = confidence_str,
        tokens_used      = tokens,
        execution_ms     = ms,
        fallback_used    = fallback_used,
        tables_used      = parsed_tables,
        schema_used      = schema_text[:800] if schema_text else "",
        generation_log   = log,
    )


# ── /models — live list from Ollama + registry ────────────────

@app.get("/models")
async def list_models():
    """Returns all Ollama-installed models plus cloud entries."""
    live_models = []
    try:
        async with httpx.AsyncClient(timeout=5) as c:
            r = await c.get(f"{OLLAMA_BASE}/api/tags")
            if r.status_code == 200:
                for m in r.json().get("models", []):
                    raw_name = m.get("name", "")
                    size     = m.get("size", 0)
                    size_fmt = (
                        f"{size/1_000_000_000:.1f}GB" if size >= 1_000_000_000
                        else f"{size/1_000_000:.0f}MB" if size >= 1_000_000
                        else ""
                    )
                    display = raw_name.split("/")[-1].split(":")[0].replace("-", " ").title()
                    live_models.append({
                        "id"          : raw_name,
                        "display_name": f"{display} (Local)",
                        "type"        : "local",
                        "available"   : True,
                        "sizeFormatted": size_fmt,
                        "best_for"    : ["sql","general"],
                    })
    except Exception:
        pass

    # Add static cloud/system entries that are always shown
    static = [
        {"id": "auto",        "display_name": "⚡ Auto-Route",        "type": "system", "available": True,  "best_for": ["all"]},
        {"id": "qwen3-coder", "display_name": "Qwen3 480B (Cloud)",  "type": "cloud",  "available": False, "best_for": ["complex","procedures"]},
    ]

    # Merge: live Ollama models first (de-dup by id)
    seen_ids = {m["id"] for m in live_models}
    for s in static:
        if s["id"] not in seen_ids:
            live_models.append(s)

    # Put auto first
    live_models.sort(key=lambda m: (0 if m["id"] == "auto" else 1 if m["type"] == "local" else 2, m["id"]))
    return live_models


# ── Unchanged endpoints ───────────────────────────────────────

@app.post("/detect-tables")
async def detect_tables(req: dict):
    nl      = req.get("natural_language", "")
    db_name = req.get("database_name", "")
    model   = req.get("model", "sqlcoder")
    all_tables = await get_all_table_names(db_name)
    detected   = await detect_relevant_tables(nl, all_tables, model)
    return {"tables_detected": detected, "all_tables_count": len(all_tables)}


@app.post("/cache/invalidate")
async def invalidate_schema_cache(req: dict):
    db = req.get("database_name", "")
    if db:
        invalidate_cache(db)
        return {"message": f"Cache cleared for '{db}'"}
    _schema_cache.clear()
    return {"message": "All caches cleared"}


@app.get("/cache/status")
async def cache_status():
    now = time.time()
    entries = [
        {"key": k, "age_seconds": int(now - v["ts"]), "expired": now - v["ts"] > CACHE_TTL_SECONDS}
        for k, v in _schema_cache.items()
    ]
    return {"entries": entries, "count": len(entries), "ttl_seconds": CACHE_TTL_SECONDS}


@app.post("/explain")
async def explain(req: ExplainRequest):
    model_key = req.model if req.model != "auto" else "qwen3-coder"
    messages  = [
        {"role": "system", "content": (
            "Explain the SQL query in plain English at a business level. "
            "Cover: what it does, joins/filters used, what the result set looks like.")},
        {"role": "user", "content": req.query},
    ]
    try:
        raw, tokens, used, _ = await call_with_messages(model_key, messages)
        return {"explanation": strip_fences(raw), "model_used": used}
    except Exception as e:
        return {"explanation": f"Explanation unavailable: {e}", "model_used": model_key}


@app.post("/risk")
async def risk(req: RiskRequest):
    messages = [
        {"role": "system", "content": (
            "You are a SQL safety auditor. Return ONLY JSON: "
            "{riskLevel: LOW|MEDIUM|HIGH|CRITICAL, risks: [], recommendations: []}. No markdown.")},
        {"role": "user", "content": req.query},
    ]
    try:
        raw, _, _, _ = await call_with_messages("qwen3-coder", messages, json_mode=True)
        return json.loads(strip_fences(raw))
    except Exception:
        return {"riskLevel": "UNKNOWN", "risks": ["Parse failed."], "recommendations": []}


@app.post("/summarize-change")
async def summarize_change(req: ChangeSummaryRequest):
    messages = [
        {"role": "system", "content": (
            "Compare SQL versions. Return ONLY JSON: "
            "{summary, key_changes: [], risk_level: LOW|MEDIUM|HIGH, breaking_changes: []}. No markdown.")},
        {"role": "user", "content": f"Object: {req.object_name}\n\nOLD:\n{req.old_script}\n\nNEW:\n{req.new_script}"},
    ]
    model_key = req.model if req.model != "auto" else "qwen3-coder"
    try:
        raw, _, _, _ = await call_with_messages(model_key, messages, json_mode=True)
        return json.loads(strip_fences(raw))
    except Exception:
        return {"summary": "Unavailable.", "key_changes": [], "risk_level": "UNKNOWN", "breaking_changes": []}


@app.post("/schema-context")
async def schema_context(req: SchemaContextRequest):
    model_key = auto_route(req.question, "SqlServer")
    messages  = [
        {"role": "system", "content": f"You are a database expert.\n\nSchema:\n{req.schema}"},
        {"role": "user",   "content": req.question},
    ]
    try:
        raw, tokens, used, _ = await call_with_messages(model_key, messages, fallback_key="qwen3-coder")
        return {"answer": strip_fences(raw), "model_used": used, "tokens_used": tokens}
    except Exception as e:
        return {"answer": f"Unavailable: {e}", "model_used": model_key, "tokens_used": 0}


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
        "status"            : "ok",
        "version"           : "5.0.0",
        "ollama"            : "connected" if ollama_ok else "unreachable",
        "models_available"  : models_avail,
        "ollama_endpoint"   : f"{OLLAMA_BASE}/api/generate",
        "schema_cache_size" : len(_schema_cache),
    }
