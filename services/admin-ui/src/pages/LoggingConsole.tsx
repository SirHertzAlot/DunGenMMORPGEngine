import { useState, useEffect, useCallback } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  getEvents,
  getContainerLogInsights,
  getAgentTasks,
  type ObservabilityEvent,
  type ContainerLogInsight,
  type AgentTask,
} from '@/lib/api'
import { Loader2, RefreshCw, Terminal, FileText, ListChecks } from 'lucide-react'

function json(v: unknown): string {
  try { return JSON.stringify(v) ?? '' } catch { return String(v) }
}

function fmtTs(ts: string) {
  try { return new Date(ts).toLocaleString() } catch { return ts }
}

function eventVariant(type: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (type?.includes('error') || type?.includes('fail')) return 'destructive'
  return type?.includes('complete') || type?.includes('success') ? 'default' : 'secondary'
}

export default function LoggingConsole() {
  const [tab, setTab] = useState('events')
  const [events, setEvents] = useState<ObservabilityEvent[]>([])
  const [logs, setLogs] = useState<ContainerLogInsight[]>([])
  const [tasks, setTasks] = useState<AgentTask[]>([])
  const [filter, setFilter] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const [ev, lg, tk] = await Promise.allSettled([
        getEvents(200),
        getContainerLogInsights(250),
        getAgentTasks(),
      ])
      if (ev.status === 'fulfilled') {
        const data = ev.value as unknown
        const arr = Array.isArray(data)
          ? data
          : ((data as { events?: ObservabilityEvent[] }).events ?? [])
        setEvents(arr as ObservabilityEvent[])
      }
      if (lg.status === 'fulfilled') setLogs(lg.value as ContainerLogInsight[])
      if (tk.status === 'fulfilled') setTasks(tk.value as AgentTask[])
    } catch (e) { setError(String(e)) } finally { setLoading(false) }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const filterText = filter.trim().toLowerCase()

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <Terminal className="h-5 w-5 text-primary" /> Logging Console
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            Aggregated observability events, container log insights, and agent tasks.
          </p>
        </div>
        <Button variant="outline" onClick={refresh} disabled={loading}>
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
        </Button>
      </div>

      {error && (
        <div className="rounded-md bg-destructive/10 border border-destructive/30 p-3 text-xs text-destructive-foreground font-mono">
          {error}
        </div>
      )}

      <div className="flex items-center gap-3">
        <Input
          value={filter}
          onChange={e => setFilter(e.target.value)}
          placeholder="Filter by text…"
          className="max-w-sm"
        />
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="events" className="gap-1.5"><FileText className="h-3.5 w-3.5" /> Events ({events.length})</TabsTrigger>
            <TabsTrigger value="containers" className="gap-1.5"><Terminal className="h-3.5 w-3.5" /> Containers ({logs.length})</TabsTrigger>
            <TabsTrigger value="tasks" className="gap-1.5"><ListChecks className="h-3.5 w-3.5" /> Agent Tasks ({tasks.length})</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {tab === 'events' && (
        <Card>
          <CardHeader><CardTitle className="text-base">Observability Events</CardTitle></CardHeader>
          <CardContent>
            <ScrollArea className="h-[560px] rounded-md border border-border">
              <div className="divide-y divide-border">
                {events
                  .filter(e => !filterText || JSON.stringify(e).toLowerCase().includes(filterText))
                  .map(e => (
                    <div key={e.eventId} className="p-3">
                      <div className="flex items-center gap-2">
                        <Badge variant={eventVariant(e.type)}>{e.type}</Badge>
                        <span className="text-xs text-muted-foreground">{fmtTs(e.timestamp)}</span>
                        <span className="text-muted-foreground ml-auto font-mono text-[11px]">{e.eventId}</span>
                      </div>
                      {e.payload ? (
                        <pre className="mt-1 text-[11px] text-muted-foreground whitespace-pre-wrap">
                          {json(e.payload)}
                        </pre>
                      ) : null}
                    </div>
                  ))}
                {events.length === 0 && <p className="p-3 text-sm text-muted-foreground">No events recorded.</p>}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>
      )}

      {tab === 'containers' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {logs
            .filter(l => !filterText || l.containerName.toLowerCase().includes(filterText))
            .map(l => (
              <Card key={l.containerName}>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <CardTitle className="text-base font-mono">{l.containerName}</CardTitle>
                    <Badge
                      variant={
                        l.errorCount > 0 ? 'destructive'
                        : l.warningCount > 0 ? 'secondary'
                        : 'default'
                      }
                    >
                      {l.errorCount > 0 ? `${l.errorCount} err` : l.warningCount > 0 ? `${l.warningCount} warn` : 'ok'}
                    </Badge>
                  </div>
                  <CardDescription>
                    {l.lineCount} lines · {fmtTs(l.capturedAtUtc)} · {l.sourceAvailable ? 'live' : 'unavailable'}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {l.healthHint && <p className="text-xs text-muted-foreground mb-2">{l.healthHint}</p>}
                  <ScrollArea className="h-56 rounded-md border border-border">
                    <pre className="p-2 text-[11px] font-mono text-muted-foreground whitespace-pre-wrap">
                      {(l.lastLines ?? []).join('\n') || l.message}
                    </pre>
                  </ScrollArea>
                </CardContent>
              </Card>
            ))}
          {logs.length === 0 && <p className="text-sm text-muted-foreground">No container log insights available.</p>}
        </div>
      )}

      {tab === 'tasks' && (
        <Card>
          <CardHeader><CardTitle className="text-base">Agent Tasks</CardTitle></CardHeader>
          <CardContent>
            <ScrollArea className="h-[560px] rounded-md border border-border">
              <div className="divide-y divide-border">
                {tasks
                  .filter(t => !filterText || JSON.stringify(t).toLowerCase().includes(filterText))
                  .map(t => (
                    <div key={t.id} className="p-3">
                      <div className="flex items-center gap-2">
                        <Badge variant={t.status?.includes('complete') ? 'default' : t.status?.includes('fail') ? 'destructive' : 'secondary'}>
                          {t.status ?? 'unknown'}
                        </Badge>
                        <span className="font-mono text-[11px] text-muted-foreground">{t.id}</span>
                      </div>
                      <div className="mt-1 text-sm">{t.description}</div>
                      <div className="mt-1 flex gap-4 text-[11px] text-muted-foreground">
                        <span>created {t.createdAtUtc ? fmtTs(t.createdAtUtc) : '—'}</span>
                        {t.completedAtUtc && <span>completed {fmtTs(t.completedAtUtc)}</span>}
                      </div>
                    </div>
                  ))}
                {tasks.length === 0 && <p className="p-3 text-sm text-muted-foreground">No agent tasks.</p>}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
