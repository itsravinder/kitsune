// ============================================================
// KITSUNE – WebSocket Service
// Real-time audit log streaming and live execution progress
// Falls back gracefully when WebSocket unavailable
// ============================================================

const WS_BASE = process.env.REACT_APP_WS_URL || 'ws://localhost:5000';

class KitsuneWebSocket {
  constructor() {
    this.ws          = null;
    this.listeners   = new Map();  // event → Set of callbacks
    this.reconnectMs = 3000;
    this.enabled     = false;
    this.retries     = 0;
    this.maxRetries  = 5;
  }

  // ── Connect ────────────────────────────────────────────────
  connect() {
    if (this.ws?.readyState === WebSocket.OPEN) return;
    this.enabled = true;

    try {
      this.ws = new WebSocket(`${WS_BASE}/ws`);

      this.ws.onopen = () => {
        console.log('[KITSUNE WS] Connected');
        this.retries = 0;
        this.emit('connected', { timestamp: new Date() });
      };

      this.ws.onmessage = (event) => {
        try {
          const msg = JSON.parse(event.data);
          this.emit(msg.type || 'message', msg);
          this.emit('*', msg); // wildcard listener
        } catch {
          this.emit('raw', event.data);
        }
      };

      this.ws.onerror = (err) => {
        console.warn('[KITSUNE WS] Error (falling back to polling)', err);
        this.emit('error', { message: 'WebSocket error' });
      };

      this.ws.onclose = () => {
        console.log('[KITSUNE WS] Disconnected');
        this.emit('disconnected', {});
        if (this.enabled && this.retries < this.maxRetries) {
          this.retries++;
          setTimeout(() => this.connect(), this.reconnectMs * this.retries);
        }
      };
    } catch (e) {
      // WebSocket not available – silently skip
      console.info('[KITSUNE WS] Not available, using polling mode');
    }
  }

  // ── Disconnect ─────────────────────────────────────────────
  disconnect() {
    this.enabled = false;
    this.ws?.close();
    this.ws = null;
  }

  // ── Subscribe to event type ────────────────────────────────
  on(event, callback) {
    if (!this.listeners.has(event))
      this.listeners.set(event, new Set());
    this.listeners.get(event).add(callback);
    // Return unsubscribe function
    return () => this.listeners.get(event)?.delete(callback);
  }

  // ── Send message to server ─────────────────────────────────
  send(type, payload = {}) {
    if (this.ws?.readyState === WebSocket.OPEN)
      this.ws.send(JSON.stringify({ type, ...payload }));
  }

  // ── Internal emit ──────────────────────────────────────────
  emit(event, data) {
    this.listeners.get(event)?.forEach(cb => {
      try { cb(data); } catch { /* ignore listener errors */ }
    });
  }

  get isConnected() {
    return this.ws?.readyState === WebSocket.OPEN;
  }
}

// Singleton instance
export const kitsuneWS = new KitsuneWebSocket();

// ── React hook for WS events ──────────────────────────────────
import { useEffect, useState, useRef } from 'react';

export function useWebSocketLogs(maxEntries = 100) {
  const [logs,      setLogs]      = useState([]);
  const [connected, setConnected] = useState(false);
  const unsubs = useRef([]);

  useEffect(() => {
    kitsuneWS.connect();

    unsubs.current.push(
      kitsuneWS.on('connected',    ()  => setConnected(true)),
      kitsuneWS.on('disconnected', ()  => setConnected(false)),
      kitsuneWS.on('*', (msg) => {
        setLogs(prev => {
          const updated = [{ ...msg, receivedAt: new Date().toISOString() }, ...prev];
          return updated.slice(0, maxEntries);
        });
      }),
    );

    return () => {
      unsubs.current.forEach(fn => fn());
      // Don't disconnect on unmount – keep connection alive
    };
  }, [maxEntries]);

  const clearLogs = () => setLogs([]);

  return { logs, connected, clearLogs };
}

// ── Polling fallback for audit log ────────────────────────────
export function useAuditPolling(intervalMs = 10000, enabled = false) {
  const [logs, setLogs] = useState([]);

  useEffect(() => {
    if (!enabled) return;
    const fetchLogs = async () => {
      try {
        const res  = await fetch('http://localhost:5000/api/audit?top=20');
        const data = await res.json();
        setLogs(data.logs || []);
      } catch { /* silent */ }
    };
    fetchLogs();
    const id = setInterval(fetchLogs, intervalMs);
    return () => clearInterval(id);
  }, [intervalMs, enabled]);

  return logs;
}
