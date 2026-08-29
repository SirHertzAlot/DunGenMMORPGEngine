import { useState, useEffect, useCallback } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import {
  getSnapshot,
  getEvents,
  getPipelineRuntime,
  getContainerHealth,
  getContainerLogInsights,
  getDatabaseObservabilitySnapshot,
  queryDatabasePrometheus,
  runDatabaseMaintenance,
  type ObservabilityEvent,
  type ContainerHealthStatus,
  type ContainerLogInsight,
  type DatabasePanelSnapshot,
  type PrometheusQueryResult,
  type DatabaseMaintenanceResult,
} from '@/lib/api'
import { Loader2, RefreshCw, Activity, Server, Clock, AlertTriangle, FileText } from 'lucide-react'

function formatTs(ts: string) {
  try {
    const d = new Date(ts)
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
  } catch { return ts }
}

function eventBadgeVariant(type: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (type?.includes('error') || type?.includes('fail')) return 'destructive'
  if (type?.includes('complete') || type?.includes('success')) return 'default'
  return 'secondary'
}

export default function Observability() {
  const [snapshot,  setSnapshot]  = useState<Record<string, unknown> | null>(null)
  const [runtime,   setRuntime]   = useState<Record<string, unknown> | null>(null)
  const [events,    setEvents]    = useState<ObservabilityEvent[]>([])
  const [containerHealth, setContainerHealth] = useState<ContainerHealthStatus[]>([])
  const [containerLogs, setContainerLogs] = useState<ContainerLogInsight[]>([])
  const [databasePanels, setDatabasePanels] = useState<DatabasePanelSnapshot[]>([])
  const [dbQuery, setDbQuery] = useState<Record<string, string>>({})
  const [dbQueryResult, setDbQueryResult] = useState<Record<string, PrometheusQueryResult | null>>({})
  const [dbMaintenanceResult, setDbMaintenanceResult] = useState<Record<string, DatabaseMaintenanceResult | null>>({})
  const [dbBusy, setDbBusy] = useState<Record<string, boolean>>({})
  const [dangerConfirm, setDangerConfirm] = useState<Record<string, boolean>>({})
  const [loading,   setLoading]   = useState(false)
  const [error,     setError]     = useState<string | null>(null)
  const [autoRefresh, setAuto]    = useState(false)
  const [lastFetch, setLastFetch] = useState<Date | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const [snap, rt, ev, health, logs, dbs] = await Promise.allSettled([
        getSnapshot(),
        getPipelineRuntime(),
        getEvents(50),
        getContainerHealth(),
        getContainerLogInsights(250),
        getDatabaseObservabilitySnapshot(),
      ])
      if (snap.status   === 'fulfilled') setSnapshot(snap.value as Record<string, unknown>)
      if (rt.status     === 'fulfilled') setRuntime(rt.value   as Record<string, unknown>)
      if (ev.status     === 'fulfilled') {
        const data = ev.value as { events?: ObservabilityEvent[] } | ObservabilityEvent[]
        setEvents(Array.isArray(data) ? data : (data as { events?: ObservabilityEvent[] }).events ?? [])
      }
      if (health.status === 'fulfilled') setContainerHealth(health.value as ContainerHealthStatus[])
      if (logs.status === 'fulfilled') setContainerLogs(logs.value as ContainerLogInsight[])
      if (dbs.status === 'fulfilled') {
        const panels = dbs.value?.databases ?? []
        setDatabasePanels(panels)
        setDbQuery(prev => {
          const next = { ...prev }
          for (const panel of panels) {
            if (!next[panel.name])
              next[panel.name] = `max(up{job="${panel.name}"})`
          }
          return next
        })
      }
      setLastFetch(new Date())
    } catch (e) {
      setError(String(e))
    } finally {
      setLoading(false)
    }
  }, [])

  // Initial load
  useEffect(() => { void refresh() }, [refresh])

  // Auto-refresh every 5s
  useEffect(() => {
    if (!autoRefresh) return
    const id = setInterval(() => { void refresh() }, 5000)
    return () => clearInterval(id)
  }, [autoRefresh, refresh])

  const containerOnline = containerHealth.filter(x => x.isOnline).length
  const containerOffline = Math.max(0, containerHealth.length - containerOnline)
  const unhealthyLogs = containerLogs.filter(x => x.errorCount > 0 || !x.sourceAvailable).length

  const runPromQuery = async (database: string) => {
    const query = (dbQuery[database] ?? '').trim()
    if (!query) return

    setDbBusy(prev => ({ ...prev, [database]: true }))
    try {
      const result = await queryDatabasePrometheus(database, query)
      setDbQueryResult(prev => ({ ...prev, [database]: result }))
    } catch (e) {
      setDbQueryResult(prev => ({
        ...prev,
        [database]: {
          database,
          query,
          success: false,
          value: null,
          message: String(e),
          capturedAtUtc: new Date().toISOString(),
        },
      }))
    } finally {
      setDbBusy(prev => ({ ...prev, [database]: false }))
    }
  }

  const runMaintenanceAction = async (database: string, action: string) => {
    const key = `${database}:${action}`
    setDbBusy(prev => ({ ...prev, [key]: true }))
    try {
      const confirmed = dangerConfirm[key] ?? false
      const result = await runDatabaseMaintenance(database, action, confirmed)
      setDbMaintenanceResult(prev => ({ ...prev, [database]: result }))
      if (result.success)
        void refresh()
    } catch (e) {
      setDbMaintenanceResult(prev => ({
        ...prev,
        [database]: {
          database,
          action,
          success: false,
          message: String(e),
        },
      }))
    } finally {
      setDbBusy(prev => ({ ...prev, [key]: false }))
    }
  }

  const formatMetricValue = (value: number | null | undefined) => {
    if (typeof value !== 'number' || Number.isNaN(value)) return '—'
    if (Math.abs(value) >= 1000) return value.toLocaleString(undefined, { maximumFractionDigits: 2 })
    return value.toFixed(2)
  }

  const isDestructiveAction = (action: string) =>
    ['vacuum', 'checkpoint', 'memory-purge', 'bgsave', 'compact', 'cleanup'].includes(action)

  const statusCards = [
    {
      label: 'Pipeline',
      icon: Server,
      value: runtime ? ((runtime.status as string) ?? 'loaded') : '—',
      sub:   runtime ? ((runtime.pipelineName as string) ?? 'unknown') : '',
    },
    {
      label: 'Last Execution',
      icon: Activity,
      value: snapshot ? ((snapshot.lastExecutionId as string)?.slice(0, 8) ?? '—') : '—',
      sub:   snapshot ? ((snapshot.lastExecutedAt as string)
        ? formatTs(snapshot.lastExecutedAt as string) : '') : '',
    },
    {
      label: 'Events Tracked',
      icon: Clock,
      value: events.length > 0 ? String(events.length) : (snapshot?.totalEvents as string ?? '—'),
      sub:   lastFetch ? `Refreshed ${formatTs(lastFetch.toISOString())}` : '',
    },
    {
      label: 'Services Online',
      icon: Activity,
      value: containerHealth.length > 0 ? `${containerOnline}/${containerHealth.length}` : '—',
      sub: containerOffline > 0 ? `${containerOffline} offline` : 'all reachable',
    },
    {
      label: 'Log Alerts',
      icon: AlertTriangle,
      value: containerLogs.length > 0 ? String(unhealthyLogs) : '—',
      sub: unhealthyLogs > 0 ? 'inspect container logs' : 'no error patterns',
    },
  ]

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Observability</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Live pipeline state, execution history, and event feed.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant={autoRefresh ? 'default' : 'outline'}
            size="sm"
            onClick={() => setAuto(v => !v)}
          >
            <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${autoRefresh ? 'animate-spin' : ''}`} />
            {autoRefresh ? 'Auto' : 'Manual'}
          </Button>
          <Button variant="outline" size="sm" onClick={refresh} disabled={loading}>
            {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCw className="h-3.5 w-3.5" />}
          </Button>
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-destructive/10 border border-destructive/30 p-3 text-sm text-destructive-foreground">
          {error}
        </div>
      )}

      {/* Status Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
        {statusCards.map(({ label, icon: Icon, value, sub }) => (
          <Card key={label}>
            <CardContent className="p-4">
              <div className="flex items-center gap-2 text-muted-foreground text-xs mb-2">
                <Icon className="h-3.5 w-3.5" />
                {label}
              </div>
              <div className="text-xl font-semibold font-mono">{value}</div>
              {sub && <div className="text-xs text-muted-foreground mt-0.5">{sub}</div>}
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Runtime Snapshot */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Pipeline Runtime</CardTitle>
            {runtime && (
              <CardDescription className="text-xs font-mono">
                {(runtime.sessionId as string) ?? (runtime.pipelineId as string) ?? ''}
              </CardDescription>
            )}
          </CardHeader>
          <CardContent>
            {!runtime ? (
              <p className="text-sm text-muted-foreground">No runtime data.</p>
            ) : (
              <div className="space-y-2 text-sm">
                {Object.entries(runtime)
                  .filter(([k]) => !['sessionId','pipelineId'].includes(k))
                  .slice(0, 12)
                  .map(([k, v]) => (
                    <div key={k} className="flex items-center justify-between">
                      <span className="text-muted-foreground capitalize">{k.replace(/([A-Z])/g, ' $1').trim()}</span>
                      <span className="font-mono text-xs text-right max-w-[180px] truncate">
                        {typeof v === 'object' ? JSON.stringify(v) : String(v ?? '—')}
                      </span>
                    </div>
                  ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* World Snapshot */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">World Snapshot</CardTitle>
          </CardHeader>
          <CardContent>
            {!snapshot ? (
              <p className="text-sm text-muted-foreground">No snapshot data.</p>
            ) : (
              <div className="space-y-2 text-sm">
                {Object.entries(snapshot).slice(0, 12).map(([k, v]) => (
                  <div key={k} className="flex items-center justify-between">
                    <span className="text-muted-foreground capitalize">{k.replace(/([A-Z])/g, ' $1').trim()}</span>
                    <span className="font-mono text-xs text-right max-w-[180px] truncate">
                      {typeof v === 'object' ? JSON.stringify(v) : String(v ?? '—')}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Container Health</CardTitle>
            <CardDescription className="text-xs">Composed service probes across HTTP and TCP endpoints.</CardDescription>
          </CardHeader>
          <CardContent>
            {containerHealth.length === 0 ? (
              <p className="text-sm text-muted-foreground">No health data yet.</p>
            ) : (
              <ScrollArea className="h-64">
                <div className="space-y-2 pr-3">
                  {containerHealth.map(item => (
                    <div key={item.name} className="rounded-md border border-border p-2">
                      <div className="flex items-center justify-between gap-2">
                        <div className="text-sm font-medium">{item.name}</div>
                        <Badge variant={item.isOnline ? 'default' : 'destructive'}>
                          {item.isOnline ? 'online' : 'offline'}
                        </Badge>
                      </div>
                      <div className="mt-1 text-xs text-muted-foreground font-mono truncate">{item.target}</div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {item.kind.toUpperCase()} · {item.responseTimeMs}ms
                        {item.statusCode ? ` · ${item.statusCode}` : ''}
                      </div>
                      {item.message && (
                        <div className="mt-1 text-xs text-muted-foreground truncate">{item.message}</div>
                      )}
                    </div>
                  ))}
                </div>
              </ScrollArea>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Container Log Insights</CardTitle>
            <CardDescription className="text-xs">Tail parse for warning/error signals per container.</CardDescription>
          </CardHeader>
          <CardContent>
            {containerLogs.length === 0 ? (
              <p className="text-sm text-muted-foreground">No log insight data yet.</p>
            ) : (
              <ScrollArea className="h-64">
                <div className="space-y-2 pr-3">
                  {containerLogs.map(item => (
                    <div key={item.containerName} className="rounded-md border border-border p-2">
                      <div className="flex items-center justify-between gap-2">
                        <div className="text-sm font-medium flex items-center gap-1.5">
                          <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                          {item.containerName}
                        </div>
                        <Badge variant={item.errorCount > 0 || !item.sourceAvailable ? 'destructive' : 'secondary'}>
                          {item.healthHint || 'unknown'}
                        </Badge>
                      </div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        lines {item.lineCount} · warn {item.warningCount} · error {item.errorCount}
                      </div>
                      {item.message && (
                        <div className="mt-1 text-xs text-muted-foreground truncate">{item.message}</div>
                      )}
                    </div>
                  ))}
                </div>
              </ScrollArea>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Database Observability</CardTitle>
          <CardDescription className="text-xs">
            Redis, Postgres, and ScyllaDB telemetry sourced from exporter metrics with guarded maintenance controls.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {databasePanels.length === 0 ? (
            <p className="text-sm text-muted-foreground">No database panel data yet.</p>
          ) : (
            <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
              {databasePanels.map(panel => (
                <div key={panel.name} className="rounded-md border border-border p-3 space-y-3">
                  <div className="flex items-center justify-between gap-2">
                    <div>
                      <div className="text-sm font-semibold">{panel.displayName || panel.name}</div>
                      <div className="text-xs text-muted-foreground">{panel.notes}</div>
                    </div>
                    <Badge variant={panel.isUp ? 'default' : 'destructive'}>
                      {panel.isUp ? 'online' : 'offline'}
                    </Badge>
                  </div>

                  <div className="space-y-1.5">
                    {Object.entries(panel.metrics).map(([metric, value]) => (
                      <div key={metric} className="flex items-center justify-between text-xs">
                        <span className="text-muted-foreground font-mono">{metric}</span>
                        <span className="font-mono">{formatMetricValue(value)}</span>
                      </div>
                    ))}
                  </div>

                  <Separator />

                  <div className="space-y-2">
                    <div className="text-xs font-medium">Prometheus Query</div>
                    <div className="flex items-center gap-2">
                      <Input
                        value={dbQuery[panel.name] ?? ''}
                        onChange={e => setDbQuery(prev => ({ ...prev, [panel.name]: e.target.value }))}
                        placeholder="Enter PromQL"
                        className="h-8 text-xs font-mono"
                      />
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => { void runPromQuery(panel.name) }}
                        disabled={dbBusy[panel.name]}
                      >
                        Query
                      </Button>
                    </div>
                    {dbQueryResult[panel.name] && (
                      <div className="text-xs text-muted-foreground font-mono">
                        {dbQueryResult[panel.name]?.success
                          ? `value ${formatMetricValue(dbQueryResult[panel.name]?.value ?? null)}`
                          : dbQueryResult[panel.name]?.message}
                      </div>
                    )}
                  </div>

                  <Separator />

                  <div className="space-y-2">
                    <div className="text-xs font-medium">Maintenance</div>
                    <div className="flex flex-wrap gap-1.5">
                      {panel.maintenanceActions.map(action => {
                        const actionKey = `${panel.name}:${action}`
                        return (
                          <Button
                            key={action}
                            variant="outline"
                            size="sm"
                            className="h-7 text-[11px]"
                            onClick={() => { void runMaintenanceAction(panel.name, action) }}
                            disabled={dbBusy[actionKey]}
                          >
                            {action}
                          </Button>
                        )
                      })}
                    </div>
                    {panel.maintenanceActions.some(isDestructiveAction) && (
                      <div className="space-y-1">
                        {panel.maintenanceActions.filter(isDestructiveAction).map(action => {
                          const actionKey = `${panel.name}:${action}`
                          return (
                            <label key={actionKey} className="flex items-center gap-2 text-xs text-muted-foreground">
                              <input
                                type="checkbox"
                                checked={dangerConfirm[actionKey] ?? false}
                                onChange={e => setDangerConfirm(prev => ({ ...prev, [actionKey]: e.target.checked }))}
                              />
                              confirm {action}
                            </label>
                          )
                        })}
                      </div>
                    )}
                    {dbMaintenanceResult[panel.name] && (
                      <div className="text-xs font-mono text-muted-foreground">
                        {dbMaintenanceResult[panel.name]?.success ? 'ok:' : 'error:'} {dbMaintenanceResult[panel.name]?.message}
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Event Feed */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">Event Feed</CardTitle>
            <Badge variant="secondary">{events.length} events</Badge>
          </div>
        </CardHeader>
        <CardContent>
          <Separator className="mb-3" />
          {events.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">No events yet.</p>
          ) : (
            <ScrollArea className="h-80">
              <div className="space-y-1 pr-3">
                {[...events].reverse().map((ev, i) => (
                  <div key={ev.eventId ?? i} className="flex items-start gap-3 py-2 text-sm border-b border-border last:border-0">
                    <span className="text-xs text-muted-foreground font-mono shrink-0 w-20">
                      {ev.timestamp ? formatTs(ev.timestamp) : '—'}
                    </span>
                    <Badge variant={eventBadgeVariant(ev.type)} className="text-xs shrink-0">
                      {ev.type ?? 'event'}
                    </Badge>
                    <span className="text-xs text-muted-foreground font-mono truncate">
                      {ev.payload ? JSON.stringify(ev.payload).slice(0, 120) : ''}
                    </span>
                  </div>
                ))}
              </div>
            </ScrollArea>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
