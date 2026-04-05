# ============================================================
# KITSUNE – AI Service v2 (FastAPI)
# Endpoints: /generate /models /explain /risk
#            /summarize-change /schema-context /health
# ============================================================
from __future__ import annotations

import json, re, time
from typing import Any

import httpx
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

app = FastAPI(title="KITSUNE AI Service", version="2.0.0")
app.add_middleware(CORSMiddleware, allow_origins=["*"],
                   allow_methods=["*"], allow_headers=["*"])

OLLAMA_BASE = "http://localhost:11434"

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
async def call_ollama(model_key: str, messages: list[dict],
                       json_mode: bool = False) -> tuple[str, int]:
    cfg = MODELS[model_key]
    payload: dict[str, Any] = {
        "model"   : cfg["ollama_name"],
        "messages": messages,
        "stream"  : False,
        "options" : {"temperature": cfg["temperature"], "num_predict": cfg["max_tokens"]},
    }
    if json_mode:
        payload["format"] = "json"
    async with httpx.AsyncClient(timeout=180) as c:
        r = await c.post(f"{OLLAMA_BASE}/api/chat", json=payload)
        r.raise_for_status()
        d = r.json()
    return d.get("message", {}).get("content", "").strip(), d.get("eval_count", 0)


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


def confidence(query: str) -> float:
    words = len(query.split())
    score = min(0.96, 0.55 + words * 0.012)
    if any(x in query.lower() for x in ["i cannot", "i'm unable", "as an ai"]):
        score = 0.20
    return round(score, 2)


# ── Endpoints ─────────────────────────────────────────────────

@app.post("/generate", response_model=GenerateResponse)
async def generate(req: GenerateRequest):
    t0 = time.perf_counter()
    if req.model == "auto":
        preferred, fallback = auto_route(req.natural_language, req.database_type)
    else:
        preferred = req.model
        fallback  = "qwen3-coder" if req.model == "sqlcoder" else "sqlcoder"

    schema_block = f"\n\nDatabase schema:\n{req.schema}" if req.schema else ""
    if req.database_type.lower() == "mongodb":
        system = ("You are an expert MongoDB query engineer. "
                  "Return ONLY valid MongoDB aggregation pipeline JSON. "
                  "No explanation, no markdown fences." + schema_block)
    else:
        system = ("You are an expert Microsoft SQL Server (T-SQL) query engineer. "
                  "Return ONLY valid T-SQL. No explanation, no markdown fences, "
                  "no commentary. Use best practices: proper joins, aliases, "
                  "avoid SELECT *." + schema_block)

    messages = [
        {"role": "system", "content": system},
        {"role": "user",   "content":
            f"Database: {req.database_name or 'target database'}\n"
            f"Request: {req.natural_language}"},
    ]
    raw, tokens, model_used, fb = await call_with_fallback(preferred, fallback, messages)
    cleaned = strip_fences(raw)
    ms      = (time.perf_counter() - t0) * 1000
    return GenerateResponse(
        generated_query=cleaned, model_used=model_used,
        display_name=MODELS[model_used]["display_name"],
        explanation=f"Generated by {MODELS[model_used]['display_name']}.",
        confidence_score=confidence(cleaned), tokens_used=tokens,
        execution_ms=round(ms, 1), fallback_used=fb,
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
                    available = any(m.get("name","").startswith(cfg["ollama_name"].split(":")[0])
                                    for m in r.json().get("models",[]))
        except Exception:
            pass
        results.append({"id": key, "display_name": cfg["display_name"],
                         "type": cfg["type"], "best_for": cfg["best_for"],
                         "available": available})
    return results


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
        return {"riskLevel": "UNKNOWN",
                "risks": ["Could not parse risk assessment."],
                "recommendations": ["Review query manually."]}


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
        return {"summary": "Change summary unavailable. Review diff manually.",
                "key_changes": [], "risk_level": "UNKNOWN", "breaking_changes": []}


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
    return {"status": "ok", "version": "2.0.0",
            "ollama": "connected" if ollama_ok else "unreachable",
            "models_available": models_avail,
            "registered_models": list(MODELS.keys())}
