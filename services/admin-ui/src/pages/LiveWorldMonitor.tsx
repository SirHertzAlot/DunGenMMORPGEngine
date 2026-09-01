import { useState, useEffect, useRef, useCallback } from 'react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  getObservabilityStreamUrl,
  getSessionTimeline,
  type SessionTimelineEntry,
} from '@/lib/api'
import { Radio, RefreshCw, CircleDot, Activity } from 'lucide-react'

function json(v: unknown): string {
  try { return JSON.stringify(v) ?? '' } catch { return String(v) }
}

export default function LiveWorldMonitor() {
  const [sessionId, setSessionId] = useState('')
  const [subscribedId, setSubscribedId] = useState<string | undefined>(undefined)
  const [connected, setConnected] = useState(false)
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null)
  const [snapshot, setSnapshot] = useState<Record<string, unknown> | null>(null)
  const [frames, setFrames] = useState<Array<{ at: Date; data: Record<string, unknown> }>>([])
  const [timeline, setTimeline] = useState<SessionTimelineEntry[]>([])
  const [timelineLoading, setTimelineLoading] = useState(false)
  const esRef = useRef<EventSource | null>(null)

  const loadTimeline = useCallback(async (sid: string) => {
    if (!sid) return
    setTimelineLoading(true)
    try {
      const t = await getSessionTimeline(sid, 100)
      setTimeline(Array.isArray(t) ? t : [])
    } catch { setTimeline([]) } finally { setTimelineLoading(false) }
  }, [])

  const start = useCallback((sid: string | undefined) => {
    stop()
    if (sid) loadTimeline(sid)
    else setTimeline([])
    setFrames([])
    setSubscribedId(sid)
    setSnapshot(null)
    const es = new EventSource(getObservabilityStreamUrl(sid))
    esRef.current = es
    es.onopen = () => setConnected(true)
    es.onerror = () => setConnected(false)
    es.addEventListener('snapshot', (ev: MessageEvent) => {
      try {
        const parsed = JSON.parse(ev.data as string) as Record<string, unknown>
        setSnapshot(parsed)
        setLastUpdate(new Date())
        setFrames(prev => [...prev.slice(-59), { at: new Date(), data: parsed }])
      } catch { /* ignore malformed frame */ }
    })
    es.onmessage = () => { /* default handler not used */ }
  }, [loadTimeline])

  const stop = useCallback(() => {
    esRef.current?.close()
    esRef.current = null
    setConnected(false)
  }, [])

  useEffect(() => () => { esRef.current?.close() }, [])

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-semibold flex items-center gap-2">
          <Radio className="h-5 w-5 text-primary" /> Live World Monitor
        </h1>
        <p className="text-muted-foreground text-sm mt-1">
          Streams the backend observability snapshot over SSE every second.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Stream</CardTitle>
          <CardDescription>
            {connected
              ? <span className="flex items-center gap-1.5 text-emerald-400"><CircleDot className="h-3 w-3" /> Connected</span>
              : <span className="flex items-center gap-1.5 text-muted-foreground"><CircleDot className="h-3 w-3" /> Disconnected</span>}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1.5">
              <Label>Session filter (optional)</Label>
              <Input
                className="w-72 font-mono"
                placeholder="session-abc"
                value={sessionId}
                onChange={e => setSessionId(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') start(sessionId.trim() || undefined) }}
              />
            </div>
            <Button onClick={() => start(sessionId.trim() || undefined)}>
              <Radio className="h-4 w-4 mr-1.5" /> Subscribe
            </Button>
            <Button variant="outline" onClick={stop}>Stop</Button>
          </div>
          {subscribedId !== undefined && (
            <p className="text-xs text-muted-foreground font-mono">
              subscribed {subscribedId ? `to ${subscribedId}` : 'to all sessions'} · last frame {lastUpdate?.toLocaleTimeString() ?? '—'}
            </p>
          )}
        </CardContent>
      </Card>

      {snapshot && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Activity className="h-4 w-4" /> Live Snapshot
              </CardTitle>
              <CardDescription>Latest push from SSE feed</CardDescription>
            </CardHeader>
            <CardContent>
              <SummaryGrid snapshot={snapshot} />
              <Separator className="my-3" />
              <ScrollArea className="h-72 rounded-md border border-border">
                <pre className="p-3 text-xs font-mono text-muted-foreground whitespace-pre-wrap">
                  {JSON.stringify(snapshot, null, 2)}
                </pre>
              </ScrollArea>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <RefreshCw className="h-4 w-4" /> Recent Frames
              </CardTitle>
              <CardDescription>Last 60 snapshot pushes</CardDescription>
            </CardHeader>
            <CardContent>
              <ScrollArea className="h-80 rounded-md border border-border">
                <div className="divide-y divide-border">
                  {frames.map((f, i) => (
                    <div key={i} className="p-2 text-xs font-mono">
                      <span className="text-muted-foreground">{f.at.toLocaleTimeString()}</span>
                      <span className="ml-2 text-muted-foreground">{JSON.stringify(Object.keys(f.data)).slice(0, 120)}</span>
                    </div>
                  ))}
                  {frames.length === 0 && (
                    <p className="p-3 text-sm text-muted-foreground">No frames yet. Subscribe to begin streaming.</p>
                  )}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        </div>
      )}

      {!snapshot && connected && (
        <p className="text-sm text-muted-foreground">Waiting for first snapshot frame…</p>
      )}

      {subscribedId && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Session Timeline</CardTitle>
            <CardDescription>{timelineLoading ? 'Loading…' : `${timeline.length} entries`}</CardDescription>
          </CardHeader>
          <CardContent>
            <ScrollArea className="h-72 rounded-md border border-border">
              <div className="divide-y divide-border">
                {timeline.length === 0 && !timelineLoading && (
                  <p className="p-3 text-sm text-muted-foreground">No timeline entries for this session.</p>
                )}
                {timeline.map((t, i) => (
                  <div key={i} className="p-2.5 text-xs">
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary" className="font-mono">{t.type ?? 'event'}</Badge>
                      {t.frame !== undefined && <span className="text-muted-foreground font-mono">frame {t.frame}</span>}
                      {t.timestampUtc && <span className="text-muted-foreground ml-auto">{new Date(t.timestampUtc).toLocaleTimeString()}</span>}
                    </div>
                    {t.entityId && <div className="text-muted-foreground mt-1 font-mono">entity {t.entityId}</div>}
                    {t.data ? <pre className="mt-1 text-[11px] text-muted-foreground whitespace-pre-wrap">{json(t.data)}</pre> : null}
                  </div>
                ))}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function SummaryGrid({ snapshot }: { snapshot: Record<string, unknown> }) {
  const pick = (key: string) => snapshot?.[key]
  const exec = (pick('latestExecution') ?? pick('latestExecutionRecord')) as Record<string, unknown> | null
  const world = exec?.world as Record<string, unknown> | null
  const stats: Array<[string, string]> = []
  const add = (label: string, v: unknown) => {
    if (v !== undefined && v !== null) stats.push([label, String(v)])
  }
  add('Status', exec?.status)
  add('Execution', exec?.executionId)
  add('Session', exec?.sessionId)
  add('Rooms', world ? (world as { rooms?: unknown[] }).rooms?.length : undefined)
  add('Enemies', world ? (world as { enemies?: unknown[] }).enemies?.length : undefined)
  add('Loot', world ? (world as { loot?: unknown[] }).loot?.length : undefined)
  add('RequestedBy', exec?.requestedBy)
  add('StartedAt', exec?.startedAtUtc)
  add('CompletedAt', exec?.completedAtUtc)

  if (stats.length === 0) {
    return <p className="text-sm text-muted-foreground">No structured fields to summarize yet.</p>
  }

  return (
    <div className="grid grid-cols-2 gap-2">
      {stats.map(([label, value]) => (
        <div key={label} className="rounded-md bg-secondary px-3 py-2">
          <div className="text-[11px] text-muted-foreground uppercase tracking-wide">{label}</div>
          <div className="text-sm font-medium font-mono break-all mt-0.5">{value || '—'}</div>
        </div>
      ))}
    </div>
  )
}
