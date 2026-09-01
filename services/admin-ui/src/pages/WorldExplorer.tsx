import { useState, useEffect, useCallback, useRef } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  getAdminWorldSessions,
  getAdminWorldSession,
  ingestWorldSession,
  type WorldSessionSummary,
  type WorldSessionDetail,
  type WorldRoomRow,
  type WorldEnemyRow,
  type WorldLootRow,
  type WorldIngestResult,
} from '@/lib/api'
import { RefreshCw, Loader2, Map as MapIcon, Database, Send, ChevronDown, ChevronUp, X } from 'lucide-react'

function fmtTs(ts?: string) {
  if (!ts) return '—'
  try { return new Date(ts).toLocaleString() } catch { return ts }
}

export default function WorldExplorer() {
  const [sessions, setSessions] = useState<WorldSessionSummary[]>([])
  const [loading, setLoading] = useState(false)
  const [detail, setDetail] = useState<WorldSessionDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Import/ingest
  const [importOpen, setImportOpen] = useState(false)
  const [importSession, setImportSession] = useState('')
  const [importText, setImportText] = useState('')
  const [ingestBusy, setIngestBusy] = useState(false)
  const [ingestResult, setIngestResult] = useState<WorldIngestResult | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const res = await getAdminWorldSessions()
      setSessions(res.sessions ?? [])
      if (selectedId && !res.sessions.some(s => s.sessionId === selectedId)) {
        setDetail(null); setSelectedId(null)
      }
    } catch (e) { setError(String(e)) } finally { setLoading(false) }
  }, [selectedId])

  useEffect(() => { refresh() }, [refresh])

  const openDetail = async (sessionId: string) => {
    setSelectedId(sessionId); setDetail(null); setDetailLoading(true); setError(null)
    try { setDetail(await getAdminWorldSession(sessionId)) }
    catch (e) { setError(String(e)) } finally { setDetailLoading(false) }
  }

  const doImport = async () => {
    setError(null); setIngestResult(null)
    let world: unknown
    try { world = JSON.parse(importText) } catch { setError('Import payload is not valid JSON.'); return }
    if (!world || typeof world !== 'object') { setError('Import payload must be an object.'); return }
    const sid = importSession.trim() || `import-${Date.now()}`
    setIngestBusy(true)
    try {
      const res = await ingestWorldSession(sid, { world: world as never, notes: 'Imported via admin World Explorer' })
      setIngestResult(res)
      setImportText(''); setImportSession(''); setImportOpen(false)
      await refresh()
      await openDetail(res.sessionId)
    } catch (e) { setError(String(e)) } finally { setIngestBusy(false) }
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <MapIcon className="h-5 w-5 text-primary" /> World Explorer
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            All generated worlds persisted in ScyllaDB, with drill-down detail and a live map render.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => setImportOpen(v => !v)}>
            <Send className="h-4 w-4 mr-1.5" /> Import & Ingest
          </Button>
          <Button variant="outline" onClick={refresh} disabled={loading}>
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
          </Button>
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-destructive/10 border border-destructive/30 p-3 text-xs text-destructive-foreground font-mono">
          {error}
        </div>
      )}

      {importOpen && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Import Generated World → Ingest to Authority</CardTitle>
              <button onClick={() => setImportOpen(false)} className="text-muted-foreground hover:text-foreground">
                <X className="h-4 w-4" />
              </button>
            </div>
            <CardDescription>
              Paste a GeneratedWorldArtifact JSON (rooms/enemies/loot). It is validated and sanitized on the backend before persisting to ScyllaDB + Redis hot state.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-1.5">
              <Label>Session ID (optional)</Label>
              <Input value={importSession} onChange={e => setImportSession(e.target.value)} placeholder="session-abc" />
            </div>
            <div className="space-y-1.5">
              <Label>World JSON</Label>
              <textarea
                value={importText}
                onChange={e => setImportText(e.target.value)}
                rows={8}
                placeholder='{ "seed": 123, "width": 80, "height": 48, "dungeonLevel": 5, "rooms": [...], "enemies": [...], "loot": [...] }'
                className="w-full rounded-md border border-border bg-background p-3 text-xs font-mono resize-y"
              />
            </div>
            <Button onClick={doImport} disabled={ingestBusy || !importText.trim()}>
              {ingestBusy ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Database className="h-4 w-4 mr-1.5" />}
              Validate & Ingest
            </Button>
            {ingestResult && (
              <div className="rounded-md border border-emerald-500/30 bg-emerald-500/10 p-3 text-xs font-mono text-emerald-400">
                Ingested session {ingestResult.sessionId} — rooms {ingestResult.rooms} · enemies {ingestResult.enemies} · loot {ingestResult.loot}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Session list */}
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-base">Persisted Sessions</CardTitle>
            <CardDescription>{sessions.length} total</CardDescription>
          </CardHeader>
          <CardContent>
            <ScrollArea className="h-[560px] pr-3">
              <div className="space-y-2">
                {sessions.length === 0 && !loading && (
                  <p className="text-sm text-muted-foreground">No worlds persisted yet. Generate or import one.</p>
                )}
                {sessions.map(s => (
                  <button
                    key={s.sessionId}
                    onClick={() => openDetail(s.sessionId)}
                    className={`w-full text-left rounded-md border p-3 transition-colors ${
                      selectedId === s.sessionId
                        ? 'border-primary bg-primary/5'
                        : 'border-border hover:bg-accent'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium font-mono truncate">{s.sessionId}</span>
                      <Badge variant="secondary" className="shrink-0">L{s.dungeonLevel}</Badge>
                    </div>
                    <div className="text-xs text-muted-foreground mt-1">
                      {s.roomCount} rooms · {s.enemyCount} enemies · {s.lootCount} loot
                    </div>
                    <div className="text-[11px] text-muted-foreground mt-1 font-mono">
                      {s.width}×{s.height} · seed {s.seed}
                    </div>
                    <div className="text-[11px] text-muted-foreground/70 mt-0.5">{fmtTs(s.persistedAtUtc)}</div>
                  </button>
                ))}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        {/* Detail */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">
              {detail ? `Session ${detail.session?.sessionId}` : 'Session Detail'}
            </CardTitle>
            {detail && (
              <CardDescription className="font-mono text-xs">
                exec {detail.session.executionId} · pipeline {detail.session.pipelineId}
              </CardDescription>
            )}
          </CardHeader>
          <CardContent>
            {!detail && !detailLoading && (
              <p className="text-sm text-muted-foreground">Select a session to view its world parts and map.</p>
            )}
            {detailLoading && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading world detail…
              </div>
            )}
            {detail && (
              <SessionDetailView detail={detail} />
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function SessionDetailView({ detail }: { detail: WorldSessionDetail }) {
  const rooms: WorldRoomRow[] = detail.rooms ?? []
  const enemies: WorldEnemyRow[] = detail.enemies ?? []
  const loot: WorldLootRow[] = detail.loot ?? []
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-4 gap-2 text-center">
        {[
          { label: 'Rooms', value: rooms.length },
          { label: 'Enemies', value: enemies.length },
          { label: 'Loot', value: loot.length },
          { label: 'Size', value: `${detail.session.width}×${detail.session.height}` },
        ].map(({ label, value }) => (
          <div key={label} className="rounded-md bg-secondary p-3">
            <div className="text-lg font-bold">{value}</div>
            <div className="text-xs text-muted-foreground">{label}</div>
          </div>
        ))}
      </div>

      <WorldMap rooms={rooms} enemies={enemies} loot={loot} width={detail.session.width} height={detail.session.height} />

      <Tabs title="Rooms" rows={rooms.length}>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
          {rooms.map((r, i) => (
            <div key={i} className="rounded-md border border-border p-2 text-xs font-mono">
              #{r.roomId} · ({r.x},{r.y}) {r.width}×{r.height}
            </div>
          ))}
        </div>
      </Tabs>

      <Tabs title="Enemies" rows={enemies.length}>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
          {enemies.map((e, i) => (
            <div key={i} className="rounded-md border border-border p-2 text-xs font-mono">
              #{e.enemyId} {e.archetype} · L{e.level} · ({e.x},{e.y})
            </div>
          ))}
        </div>
      </Tabs>

      <Tabs title="Loot" rows={loot.length}>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
          {loot.map((l, i) => (
            <div key={i} className="rounded-md border border-border p-2 text-xs font-mono">
              {l.itemType} [{l.tier}] · {l.itemId} · ({l.x},{l.y})
            </div>
          ))}
        </div>
      </Tabs>
    </div>
  )
}

function Tabs({ title, rows, children }: { title: string; rows: number; children: React.ReactNode }) {
  const [open, setOpen] = useState(false)
  return (
    <div className="rounded-md border border-border">
      <button onClick={() => setOpen(v => !v)} className="w-full flex items-center justify-between p-3 text-sm font-medium hover:bg-accent transition-colors">
        <span>{title} <span className="text-muted-foreground font-normal">({rows})</span></span>
        {open ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
      </button>
      {open && <Separator />}
      {open && <div className="p-3">{children}</div>}
    </div>
  )
}

function WorldMap({ rooms, enemies, loot, width, height }: {
  rooms: WorldRoomRow[]; enemies: WorldEnemyRow[]; loot: WorldLootRow[]; width: number; height: number
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const [legendOpen, setLegendOpen] = useState(true)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const prefix = 0.92 * Math.min(canvas.clientWidth, 640)
    const pw = Math.max(1, prefix / Math.max(1, width))
    const ph = Math.max(1, prefix / Math.max(1, height))
    const W = Math.max(1, width * pw)
    const H = Math.max(1, height * ph)
    canvas.width = Math.floor(W)
    canvas.height = Math.floor(H)
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    ctx.fillStyle = '#0c0f16'
    ctx.fillRect(0, 0, canvas.width, canvas.height)

    // grid
    ctx.strokeStyle = 'rgba(255,255,255,0.04)'
    ctx.lineWidth = 1
    for (let x = 0; x <= width; x++) { ctx.beginPath(); ctx.moveTo(x * pw, 0); ctx.lineTo(x * pw, canvas.height); ctx.stroke() }
    for (let y = 0; y <= height; y++) { ctx.beginPath(); ctx.moveTo(0, y * ph); ctx.lineTo(canvas.width, y * ph); ctx.stroke() }

    // rooms
    ctx.fillStyle = '#1e3a5f'
    for (const r of rooms) {
      ctx.fillRect(r.x * pw, r.y * ph, Math.max(1, r.width * pw), Math.max(1, r.height * ph))
    }
    ctx.strokeStyle = '#3b82f6'
    ctx.lineWidth = 1
    for (const r of rooms) {
      ctx.strokeRect(r.x * pw, r.y * ph, Math.max(1, r.width * pw), Math.max(1, r.height * ph))
    }

    // loot (diamonds, gold)
    ctx.fillStyle = '#f59e0b'
    for (const l of loot) {
      const cx = l.x * pw + pw / 2, cy = l.y * ph + ph / 2
      const s = Math.max(2, pw / 3)
      ctx.beginPath()
      ctx.moveTo(cx, cy - s); ctx.lineTo(cx + s, cy); ctx.lineTo(cx, cy + s); ctx.lineTo(cx - s, cy)
      ctx.closePath(); ctx.fill()
    }

    // enemies (red dots)
    ctx.fillStyle = '#ef4444'
    for (const e of enemies) {
      ctx.beginPath()
      ctx.arc(e.x * pw + pw / 2, e.y * ph + ph / 2, Math.max(2, pw / 2.2), 0, Math.PI * 2)
      ctx.fill()
    }
  }, [rooms, enemies, loot, width, height])

  return (
    <div className="rounded-md border border-border overflow-hidden">
      <div className="flex items-center justify-between p-3">
        <span className="text-sm font-medium">World Map</span>
        <button onClick={() => setLegendOpen(v => !v)} className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
          Legend {legendOpen ? '▲' : '▼'}
        </button>
      </div>
      {legendOpen && <Separator />}
      {legendOpen && (
        <div className="flex flex-wrap gap-4 px-3 py-2 text-xs text-muted-foreground">
          <span className="flex items-center gap-1.5"><span className="inline-block h-3 w-3 rounded-sm bg-[#1e3a5f] border border-blue-500" /> Room</span>
          <span className="flex items-center gap-1.5"><span className="inline-block h-3 w-3 rounded-full bg-red-500" /> Enemy</span>
          <span className="flex items-center gap-1.5"><span className="inline-block h-3 w-3 rotate-45 bg-amber-500" /> Loot</span>
        </div>
      )}
      <Separator />
      <div className="flex justify-center p-3 bg-black/20">
        <canvas ref={canvasRef} style={{ maxWidth: '100%', height: 'auto', aspectRatio: `${width} / ${height}` }} />
      </div>
    </div>
  )
}
