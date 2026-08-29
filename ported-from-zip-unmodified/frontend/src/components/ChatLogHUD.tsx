/**
 * ChatLogHUD — parchment chat/log window at bottom-left.
 * Subscribes to window.__dungeonLog via interval polling.
 * Exports addDungeonLog() for other systems to push messages.
 */

import React, { useEffect, useRef, useState } from "react";

// ── Global log store ─────────────────────────────────────────────────────────

export type DungeonLogType = "combat" | "spawn" | "loot" | "system";

export interface DungeonLogEntry {
  id: number;
  timestamp: number;
  message: string;
  type: DungeonLogType;
}

// Attach to window so any system can push without importing this component
declare global {
  interface Window {
    __dungeonLog?: DungeonLogEntry[];
    __dungeonLogSeq?: number;
  }
}

if (typeof window !== "undefined") {
  if (!window.__dungeonLog) window.__dungeonLog = [];
  if (!window.__dungeonLogSeq) window.__dungeonLogSeq = 0;
}

/** Call from any system to push a message to the chat log. */
export function addDungeonLog(
  message: string,
  type: DungeonLogType = "system",
) {
  if (typeof window === "undefined") return;
  if (!window.__dungeonLog) window.__dungeonLog = [];
  if (!window.__dungeonLogSeq) window.__dungeonLogSeq = 0;

  const entry: DungeonLogEntry = {
    id: ++window.__dungeonLogSeq,
    timestamp: Date.now(),
    message,
    type,
  };

  window.__dungeonLog.push(entry);

  // Trim to last 50
  if (window.__dungeonLog.length > 50) {
    window.__dungeonLog = window.__dungeonLog.slice(-50);
  }
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatTime(ts: number): string {
  const d = new Date(ts);
  const hh = String(d.getHours()).padStart(2, "0");
  const mm = String(d.getMinutes()).padStart(2, "0");
  const ss = String(d.getSeconds()).padStart(2, "0");
  return `${hh}:${mm}:${ss}`;
}

const TYPE_COLORS: Record<DungeonLogType, string> = {
  combat: "#dc2626", // red
  spawn: "#1a6fc4", // blue
  loot: "#c9a227", // gold
  system: "#9ca3af", // grey ambient
};

// ── Component ─────────────────────────────────────────────────────────────────

const TABS = ["General", "Guild", "Party"] as const;
type Tab = (typeof TABS)[number];

export default function ChatLogHUD() {
  const [expanded, setExpanded] = useState(false);
  const [activeTab, setActiveTab] = useState<Tab>("General");
  const [entries, setEntries] = useState<DungeonLogEntry[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const lastIdRef = useRef(0);
  const entriesLen = entries.length;

  // Poll the window global every 300ms
  useEffect(() => {
    const id = setInterval(() => {
      if (!window.__dungeonLog) return;
      const all = window.__dungeonLog;
      if (all.length === 0) return;
      const latest = all[all.length - 1];
      if (latest.id <= lastIdRef.current) return;
      lastIdRef.current = latest.id;
      setEntries([...all]);
    }, 300);
    return () => clearInterval(id);
  }, []);

  // Auto-scroll to bottom when new entries arrive (expanded only)
  // biome-ignore lint/correctness/useExhaustiveDependencies: entriesLen is the intentional trigger
  useEffect(() => {
    if (!expanded) return;
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [entriesLen, expanded]);

  // Seed a few startup messages on mount
  useEffect(() => {
    addDungeonLog("Dungeon session started.", "system");
    addDungeonLog("Mobs have spawned in the starting room.", "spawn");
  }, []);

  const handleTabClick = (tab: Tab) => {
    setActiveTab(tab);
    setExpanded(true);
  };

  return (
    <>
      <style>{`
        .chat-log-scrollbar::-webkit-scrollbar { width: 5px; }
        .chat-log-scrollbar::-webkit-scrollbar-track { background: rgba(255,255,255,0.04); border-radius: 4px; }
        .chat-log-scrollbar::-webkit-scrollbar-thumb { background: #c9a22766; border-radius: 4px; }
        .chat-log-scrollbar::-webkit-scrollbar-thumb:hover { background: #c9a227aa; }
      `}</style>

      <div
        data-ocid="chat-log-hud"
        style={{
          position: "absolute",
          bottom: 8,
          left: 8,
          zIndex: 50,
          width: 280,
          pointerEvents: "auto",
          userSelect: "none",
          fontFamily: "monospace, sans-serif",
        }}
      >
        {/* Container */}
        <div
          style={{
            backgroundColor: "#1a1208",
            border: "2px solid #c9a227",
            borderRadius: 8,
            overflow: "hidden",
            boxShadow:
              "0 4px 24px rgba(0,0,0,0.75), 0 0 0 1px rgba(201,162,39,0.1)",
          }}
        >
          {/* Header row — toggle area + tab buttons */}
          <div
            style={{
              width: "100%",
              minHeight: 32,
              display: "flex",
              alignItems: "center",
              gap: 8,
              padding: "0 10px",
              background: "transparent",
              borderBottom: expanded
                ? "1px solid rgba(201,162,39,0.3)"
                : "none",
            }}
          >
            {/* Toggle button (icon + collapse indicator) */}
            <button
              type="button"
              data-ocid="chat-log.toggle"
              onClick={() => setExpanded((v) => !v)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") setExpanded((v) => !v);
              }}
              aria-expanded={expanded}
              aria-label={expanded ? "Minimize chat log" : "Expand chat log"}
              style={{
                background: "none",
                border: "none",
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                gap: 4,
                padding: 0,
                WebkitTapHighlightColor: "transparent",
                minHeight: 32,
              }}
            >
              <span style={{ fontSize: 13, lineHeight: 1 }}>💬</span>
              <span
                style={{
                  color: "#c9a22799",
                  fontSize: 10,
                  lineHeight: 1,
                  transition: "transform 0.2s",
                  transform: expanded ? "rotate(180deg)" : "rotate(0deg)",
                  display: "block",
                }}
              >
                ▲
              </span>
            </button>

            {/* Tab labels */}
            <div style={{ display: "flex", gap: 10, flex: 1 }}>
              {TABS.map((tab) => (
                <button
                  key={tab}
                  type="button"
                  data-ocid={`chat-log.tab.${tab.toLowerCase()}`}
                  onClick={() => handleTabClick(tab)}
                  style={{
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    fontSize: 10,
                    fontWeight: activeTab === tab ? 700 : 400,
                    color: activeTab === tab ? "#c9a227" : "#9ca3af",
                    borderBottom:
                      activeTab === tab
                        ? "1px solid #c9a227"
                        : "1px solid transparent",
                    padding: "2px 0",
                    lineHeight: 1.4,
                    letterSpacing: "0.04em",
                    WebkitTapHighlightColor: "transparent",
                    minHeight: 32,
                  }}
                >
                  {tab}
                </button>
              ))}
            </div>
          </div>

          {/* Message area — only visible when expanded */}
          {expanded && (
            <div
              ref={scrollRef}
              className="chat-log-scrollbar"
              data-ocid="chat-log.message_area"
              style={{
                height: 200,
                overflowY: "scroll",
                overflowX: "hidden",
                background: "linear-gradient(to bottom, #2a1e0e, #1a1208)",
                padding: "6px 8px",
                display: "flex",
                flexDirection: "column",
                gap: 2,
              }}
            >
              {entries.length === 0 ? (
                <div
                  data-ocid="chat-log.empty_state"
                  style={{
                    color: "#9ca3af",
                    fontSize: 11,
                    textAlign: "center",
                    marginTop: 8,
                  }}
                >
                  No messages yet…
                </div>
              ) : (
                entries.map((entry) => (
                  <div
                    key={entry.id}
                    data-ocid={`chat-log.entry.${entry.id}`}
                    style={{
                      fontSize: 11,
                      lineHeight: 1.5,
                      color:
                        entry.type === "system"
                          ? "#9ca3af"
                          : TYPE_COLORS[entry.type],
                      wordBreak: "break-word",
                    }}
                  >
                    <span style={{ color: "#c9a22777", marginRight: 4 }}>
                      [{formatTime(entry.timestamp)}]
                    </span>
                    {entry.message}
                  </div>
                ))
              )}
            </div>
          )}
        </div>
      </div>
    </>
  );
}
