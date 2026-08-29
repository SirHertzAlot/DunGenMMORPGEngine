import { useState, useEffect, useMemo } from 'react';
import { runtimeManager } from '../lib/runtimeManager';
import { queueManager } from '../lib/queueManager';
import type { EventMessage } from '../lib/queueManager';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Activity, Database, Cpu, Zap, AlertCircle, Pause, RotateCcw, Play, Square, RefreshCw, ShieldAlert, Terminal } from 'lucide-react';
import type { Entity, PerformanceMetrics } from '../types/runtime';

type DbPanelSnapshot = {
  name: string;
  displayName: string;
  isUp: boolean;
  capturedAtUtc: string;
  metrics: Record<string, number | null>;
  maintenanceActions: string[];
  notes: string;
};

type DbSnapshot = {
  capturedAtUtc: string;
  databases: DbPanelSnapshot[];
};

type PromQueryResult = {
  database: string;
  query: string;
  success: boolean;
  value: number | null;
  message: string;
  capturedAtUtc: string;
};

type MaintenanceResult = {
  database: string;
  action: string;
  success: boolean;
  message: string;
};

type RedisKeyValueResult = {
  key: string;
  exists: boolean;
  value: string;
  timeToLiveSeconds: number | null;
  success: boolean;
  message: string;
};

type RedisKeyMutationResult = {
  key: string;
  success: boolean;
  message: string;
};

type BackendTimelineEvent = {
  eventId: string;
  sessionId: string;
  eventType: string;
  category: string;
  frame: number;
  entityId: string;
  message: string;
  timestampUtc: string;
  data: Record<string, string>;
};

type ExporterRawResult = {
  exporter: string;
  fetchedAtUtc: string;
  success: boolean;
  error: string | null;
  lineCount: number;
  lines: string[];
};

export default function RuntimeSystem() {
  const adminKey = useMemo(() => {
    const url = new URL(window.location.href);
    return url.searchParams.get('adminKey') ?? 'dev-admin-key';
  }, []);

  const sessionId = useMemo(() => {
    const url = new URL(window.location.href);
    return url.searchParams.get('sessionId') ?? 'session-001';
  }, []);

  const adminHeaders = useMemo(() => ({
    'Content-Type': 'application/json',
    'X-Admin-Key': adminKey,
  }), [adminKey]);

  const [entities, setEntities] = useState<Entity[]>([]);
  const [metrics, setMetrics] = useState<PerformanceMetrics>(runtimeManager.getMetrics());
  const [selectedEntity, setSelectedEntity] = useState<Entity | null>(null);
  const [queueStats, setQueueStats] = useState(queueManager.getStats());
  const [recentEvents, setRecentEvents] = useState<string[]>([]);
  const [dbSnapshot, setDbSnapshot] = useState<DbSnapshot | null>(null);
  const [dbError, setDbError] = useState<string>('');
  const [dbLoading, setDbLoading] = useState(false);
  const [dbBusy, setDbBusy] = useState<Record<string, boolean>>({});
  const [dbQueries, setDbQueries] = useState<Record<string, string>>({});
  const [dbQueryResults, setDbQueryResults] = useState<Record<string, PromQueryResult | null>>({});
  const [maintenanceResults, setMaintenanceResults] = useState<Record<string, MaintenanceResult | null>>({});
  const [dangerConfirmed, setDangerConfirmed] = useState<Record<string, boolean>>({});
  const [redisKey, setRedisKey] = useState('');
  const [redisValue, setRedisValue] = useState('');
  const [redisTtlSeconds, setRedisTtlSeconds] = useState('');
  const [redisReadResult, setRedisReadResult] = useState<RedisKeyValueResult | null>(null);
  const [redisWriteResult, setRedisWriteResult] = useState<RedisKeyMutationResult | null>(null);

  const [backendTimeline, setBackendTimeline] = useState<BackendTimelineEvent[]>([]);
  const [backendTimelineError, setBackendTimelineError] = useState('');
  const [backendTimelineLoading, setBackendTimelineLoading] = useState(false);

  const [persistentHistory, setPersistentHistory] = useState<BackendTimelineEvent[]>([]);
  const [persistentHistoryError, setPersistentHistoryError] = useState('');
  const [persistentHistoryLoading, setPersistentHistoryLoading] = useState(false);
  const [sessionSummary, setSessionSummary] = useState<{
    totalEvents: number;
    entitySnapshotCount: number;
    systemEventCount: number;
    turnCount: number;
    firstEventUtc: string | null;
    lastEventUtc: string | null;
  } | null>(null);

  const EXPORTERS = ['redis', 'postgres', 'rabbitmq', 'scylla'] as const;
  type ExporterName = typeof EXPORTERS[number];
  const [exporterData, setExporterData] = useState<Partial<Record<ExporterName, ExporterRawResult>>>({});
  const [exporterLoading, setExporterLoading] = useState<Partial<Record<ExporterName, boolean>>>({});
  const [exporterFilter, setExporterFilter] = useState<Partial<Record<ExporterName, string>>>({});
  const [activeExporter, setActiveExporter] = useState<ExporterName>('redis');

  const isDangerous = (action: string) =>
    ['vacuum', 'checkpoint', 'memory-purge', 'bgsave', 'compact', 'cleanup'].includes(action);

  const formatMetric = (value: number | null | undefined) => {
    if (typeof value !== 'number' || Number.isNaN(value)) return '—';
    if (Math.abs(value) >= 1000) return value.toLocaleString(undefined, { maximumFractionDigits: 2 });
    return value.toFixed(2);
  };

  const request = async <T,>(path: string, init?: RequestInit): Promise<T> => {
    const res = await fetch(path, {
      ...init,
      headers: {
        ...adminHeaders,
        ...(init?.headers ?? {}),
      },
    });

    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText);
      throw new Error(`${res.status}: ${text}`);
    }

    return await res.json() as T;
  };

  const loadDatabaseSnapshot = async () => {
    setDbLoading(true);
    setDbError('');
    try {
      const snapshot = await request<DbSnapshot>('/admin/observability/databases/snapshot');
      setDbSnapshot(snapshot);
      setDbQueries(prev => {
        const next = { ...prev };
        for (const db of snapshot.databases) {
          if (!next[db.name]) next[db.name] = `max(up{job="${db.name}"})`;
        }
        return next;
      });
    } catch (e) {
      setDbError(String(e));
    } finally {
      setDbLoading(false);
    }
  };

  const loadBackendTimeline = async () => {
    setBackendTimelineLoading(true);
    setBackendTimelineError('');
    try {
      const events = await request<BackendTimelineEvent[]>(
        `/admin/observability/sessions/${encodeURIComponent(sessionId)}/timeline?take=120`
      );
      setBackendTimeline(events);
    } catch (e) {
      setBackendTimelineError(String(e));
    } finally {
      setBackendTimelineLoading(false);
    }
  };

  const loadPersistentHistory = async () => {
    setPersistentHistoryLoading(true);
    setPersistentHistoryError('');
    try {
      const [events, summary] = await Promise.all([
        request<BackendTimelineEvent[]>(
          `/admin/observability/sessions/${encodeURIComponent(sessionId)}/events/history?take=200`
        ),
        request<{
          totalEvents: number;
          entitySnapshotCount: number;
          systemEventCount: number;
          turnCount: number;
          firstEventUtc: string | null;
          lastEventUtc: string | null;
        }>(`/admin/observability/sessions/${encodeURIComponent(sessionId)}/events/summary`),
      ]);
      setPersistentHistory(events);
      setSessionSummary(summary);
    } catch (e) {
      setPersistentHistoryError(String(e));
    } finally {
      setPersistentHistoryLoading(false);
    }
  };

  const openGrafana = () => {
    const host = window.location.hostname || 'localhost';
    const url = `http://${host}:3000/d/mmo-backend-overview`;
    window.open(url, '_blank');
  };

  const runPromQuery = async (database: string) => {
    const query = (dbQueries[database] ?? '').trim();
    if (!query) return;

    const busyKey = `query:${database}`;
    setDbBusy(prev => ({ ...prev, [busyKey]: true }));
    try {
      const result = await request<PromQueryResult>(
        `/admin/observability/databases/${encodeURIComponent(database)}/query?query=${encodeURIComponent(query)}`
      );
      setDbQueryResults(prev => ({ ...prev, [database]: result }));
    } catch (e) {
      setDbQueryResults(prev => ({
        ...prev,
        [database]: {
          database,
          query,
          success: false,
          value: null,
          message: String(e),
          capturedAtUtc: new Date().toISOString(),
        },
      }));
    } finally {
      setDbBusy(prev => ({ ...prev, [busyKey]: false }));
    }
  };

  const runMaintenance = async (database: string, action: string) => {
    const key = `${database}:${action}`;
    setDbBusy(prev => ({ ...prev, [key]: true }));
    try {
      const result = await request<MaintenanceResult>(
        `/admin/observability/databases/${encodeURIComponent(database)}/maintenance`,
        {
          method: 'POST',
          body: JSON.stringify({ action, confirmed: dangerConfirmed[key] ?? false }),
        }
      );
      setMaintenanceResults(prev => ({ ...prev, [database]: result }));
      await loadDatabaseSnapshot();
    } catch (e) {
      setMaintenanceResults(prev => ({
        ...prev,
        [database]: {
          database,
          action,
          success: false,
          message: String(e),
        },
      }));
    } finally {
      setDbBusy(prev => ({ ...prev, [key]: false }));
    }
  };

  const redisGetKey = async () => {
    const key = redisKey.trim();
    if (!key) return;
    setDbBusy(prev => ({ ...prev, redisRead: true }));
    try {
      const result = await request<RedisKeyValueResult>(`/admin/observability/databases/redis/keys/${encodeURIComponent(key)}`);
      setRedisReadResult(result);
      if (result.exists) setRedisValue(result.value ?? '');
    } catch (e) {
      setRedisReadResult({ key, exists: false, value: '', timeToLiveSeconds: null, success: false, message: String(e) });
    } finally {
      setDbBusy(prev => ({ ...prev, redisRead: false }));
    }
  };

  const redisUpsertKey = async () => {
    const key = redisKey.trim();
    if (!key) return;
    const ttl = Number(redisTtlSeconds);
    setDbBusy(prev => ({ ...prev, redisWrite: true }));
    try {
      const result = await request<RedisKeyMutationResult>('/admin/observability/databases/redis/keys', {
        method: 'POST',
        body: JSON.stringify({
          key,
          value: redisValue,
          timeToLiveSeconds: Number.isFinite(ttl) && ttl > 0 ? ttl : null,
        }),
      });
      setRedisWriteResult(result);
      await loadDatabaseSnapshot();
    } catch (e) {
      setRedisWriteResult({ key, success: false, message: String(e) });
    } finally {
      setDbBusy(prev => ({ ...prev, redisWrite: false }));
    }
  };

  const fetchExporterRaw = async (exporter: ExporterName) => {
    setExporterLoading(prev => ({ ...prev, [exporter]: true }));
    try {
      const result = await request<ExporterRawResult>(`/admin/observability/exporters/${exporter}/raw`);
      setExporterData(prev => ({ ...prev, [exporter]: result }));
    } catch (e) {
      setExporterData(prev => ({
        ...prev,
        [exporter]: {
          exporter,
          fetchedAtUtc: new Date().toISOString(),
          success: false,
          error: String(e),
          lineCount: 0,
          lines: [],
        },
      }));
    } finally {
      setExporterLoading(prev => ({ ...prev, [exporter]: false }));
    }
  };

  const redisDeleteKey = async () => {
    const key = redisKey.trim();
    if (!key) return;
    setDbBusy(prev => ({ ...prev, redisDelete: true }));
    try {
      const result = await request<RedisKeyMutationResult>(`/admin/observability/databases/redis/keys/${encodeURIComponent(key)}`, {
        method: 'DELETE',
      });
      setRedisWriteResult(result);
      setRedisReadResult(null);
      await loadDatabaseSnapshot();
    } catch (e) {
      setRedisWriteResult({ key, success: false, message: String(e) });
    } finally {
      setDbBusy(prev => ({ ...prev, redisDelete: false }));
    }
  };

  useEffect(() => {
    const unsubscribeEvents = queueManager.subscribe<EventMessage>('runtime:events', (message) => {
      const eventStr = `[${new Date(message.timestamp).toLocaleTimeString()}] ${message.data.eventType}`;
      setRecentEvents(prev => [eventStr, ...prev].slice(0, 10));
    });

    return () => {
      unsubscribeEvents();
    };
  }, []);

  useEffect(() => {
    const interval = setInterval(() => {
      setEntities(runtimeManager.getAllEntities());
      setMetrics(runtimeManager.getMetrics());
      setQueueStats(queueManager.getStats());
    }, 100);

    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    void loadDatabaseSnapshot();
    const interval = window.setInterval(() => {
      void loadDatabaseSnapshot();
    }, 5000);

    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    void loadBackendTimeline();
    const interval = window.setInterval(() => {
      void loadBackendTimeline();
    }, 3000);
    return () => window.clearInterval(interval);
  }, [sessionId]);

  useEffect(() => {
    void loadPersistentHistory();
    const interval = window.setInterval(() => {
      void loadPersistentHistory();
    }, 10000);
    return () => window.clearInterval(interval);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  useEffect(() => {
    void fetchExporterRaw(activeExporter);
    const interval = window.setInterval(() => {
      void fetchExporterRaw(activeExporter);
    }, 15000);
    return () => window.clearInterval(interval);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeExporter]);

  const handleStart = () => {
    runtimeManager.start();
  };

  const handleStop = () => {
    runtimeManager.stop();
  };

  const handlePause = () => {
    runtimeManager.pause();
  };

  const handleResume = () => {
    runtimeManager.resume();
  };

  const handleReset = () => {
    runtimeManager.reset();
  };

  const handleCreateTestEntity = () => {
    const entity = runtimeManager.createEntity();
    runtimeManager.addComponent(entity.id, 'Transform', {
      position: { x: 0, y: 0, z: 0 },
      rotation: { x: 0, y: 0, z: 0 },
      scale: { x: 1, y: 1, z: 1 },
    });
    runtimeManager.addComponent(entity.id, 'Health', {
      max: 100,
      current: 100,
      regenerationRate: 1,
    });

    queueManager.publish<EventMessage>('runtime:events', {
      eventType: 'entity:created',
      entityId: entity.id,
    });
  };

  const runtimeState = runtimeManager.getState();
  const isFrontendRunning = runtimeState.isRunning;
  const isFrontendPaused = runtimeState.isPaused;

  return (
    <div className="flex h-full flex-col overflow-hidden p-4 sm:p-6">
      <div className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold sm:text-3xl">Runtime System</h1>
          <p className="text-sm text-muted-foreground">
            Runtime diagnostics plus live database observability and operations
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            onClick={handleStart}
            disabled={isFrontendRunning}
            variant="default"
            size="sm"
          >
            <Play className="mr-2 h-4 w-4" />
            Start
          </Button>
          <Button
            onClick={handleStop}
            disabled={!isFrontendRunning}
            variant="destructive"
            size="sm"
          >
            <Square className="mr-2 h-4 w-4" />
            Stop
          </Button>
          <Button
            onClick={isFrontendPaused ? handleResume : handlePause}
            disabled={!isFrontendRunning}
            variant="outline"
            size="sm"
          >
            <Pause className="mr-2 h-4 w-4" />
            {isFrontendPaused ? 'Resume' : 'Pause'}
          </Button>
          <Button
            onClick={handleReset}
            variant="outline"
            size="sm"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            Reset
          </Button>
          <Button
            onClick={handleCreateTestEntity}
            variant="outline"
            size="sm"
          >
            <Zap className="mr-2 h-4 w-4" />
            Create Entity
          </Button>
          <Button onClick={openGrafana} variant="outline" size="sm">
            <Activity className="mr-2 h-4 w-4" />
            Open Grafana Dashboard
          </Button>
        </div>
      </div>

      <Alert className="mb-4">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          Database metrics and maintenance call the authoritative backend in real time. Dangerous actions require explicit confirmation. Session: <span className="font-mono">{sessionId}</span>
        </AlertDescription>
      </Alert>

      <Card className="mb-4">
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle>Backend Session Timeline</CardTitle>
              <CardDescription>Live events currently stored by authoritative observability for this session.</CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => { void loadBackendTimeline(); }}
              disabled={backendTimelineLoading}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${backendTimelineLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {backendTimelineError && (
            <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/10 p-2 text-xs text-destructive">
              {backendTimelineError}
            </div>
          )}

          <ScrollArea className="h-48 rounded-md border bg-muted/30">
            <div className="space-y-1 p-2">
              {backendTimeline.length === 0 ? (
                <p className="p-3 text-xs text-muted-foreground">No backend timeline events for this session yet.</p>
              ) : (
                backendTimeline.map((evt) => (
                  <div key={evt.eventId} className="rounded border-l-2 border-primary bg-background p-2 text-xs">
                    <div className="mb-1 flex items-center gap-2">
                      <Badge variant="outline" className="font-mono">{evt.eventType}</Badge>
                      <Badge variant="secondary">{evt.category}</Badge>
                      <span className="text-muted-foreground font-mono">frame {evt.frame}</span>
                      <span className="text-muted-foreground ml-auto">{new Date(evt.timestampUtc).toLocaleTimeString()}</span>
                    </div>
                    <div className="font-mono text-[11px] text-muted-foreground">{evt.message}</div>
                  </div>
                ))
              )}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      <Card className="mb-4">
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle>Persistent Event History</CardTitle>
              <CardDescription>Events stored in Postgres — survives backend restarts. Auto-refreshes every 10s.</CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => { void loadPersistentHistory(); }}
              disabled={persistentHistoryLoading}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${persistentHistoryLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {persistentHistoryError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-2 text-xs text-destructive">
              {persistentHistoryError}
            </div>
          )}

          {sessionSummary && (
            <div className="grid grid-cols-2 gap-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-3 lg:grid-cols-6">
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">Total Events</span>
                <span className="font-mono text-sm font-semibold">{sessionSummary.totalEvents.toLocaleString()}</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">Snapshots</span>
                <span className="font-mono text-sm font-semibold">{sessionSummary.entitySnapshotCount.toLocaleString()}</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">System Events</span>
                <span className="font-mono text-sm font-semibold">{sessionSummary.systemEventCount.toLocaleString()}</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">Max Turn</span>
                <span className="font-mono text-sm font-semibold">{sessionSummary.turnCount}</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">First Event</span>
                <span className="font-mono text-xs text-muted-foreground">
                  {sessionSummary.firstEventUtc ? new Date(sessionSummary.firstEventUtc).toLocaleTimeString() : '—'}
                </span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-[10px] uppercase tracking-wide text-muted-foreground">Last Event</span>
                <span className="font-mono text-xs text-muted-foreground">
                  {sessionSummary.lastEventUtc ? new Date(sessionSummary.lastEventUtc).toLocaleTimeString() : '—'}
                </span>
              </div>
            </div>
          )}

          <ScrollArea className="h-64 rounded-md border bg-muted/30">
            <div className="space-y-1 p-2">
              {persistentHistory.length === 0 ? (
                <p className="p-3 text-xs text-muted-foreground">
                  {persistentHistoryLoading ? 'Loading…' : 'No persistent events for this session yet. Events are written to Postgres as gameplay runs.'}
                </p>
              ) : (
                persistentHistory.map((evt) => (
                  <div key={evt.eventId} className="rounded border-l-2 border-green-500/60 bg-background p-2 text-xs">
                    <div className="mb-1 flex items-center gap-2">
                      <Badge variant="outline" className="font-mono text-[10px]">{evt.eventType}</Badge>
                      <Badge variant="secondary" className="text-[10px]">{evt.category}</Badge>
                      {evt.entityId && <span className="font-mono text-[10px] text-muted-foreground">{evt.entityId}</span>}
                      <span className="font-mono text-[10px] text-muted-foreground">f{evt.frame}</span>
                      <span className="ml-auto font-mono text-[10px] text-muted-foreground">
                        {new Date(evt.timestampUtc).toLocaleTimeString()}
                      </span>
                    </div>
                    <div className="font-mono text-[10px] text-muted-foreground">{evt.message}</div>
                  </div>
                ))
              )}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      <Card className="mb-4">
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <div>
              <CardTitle>Database Observability</CardTitle>
              <CardDescription>Exporter-backed metrics, protected maintenance commands, and Redis CRUD operations.</CardDescription>
            </div>
            <Button variant="outline" size="sm" onClick={() => { void loadDatabaseSnapshot(); }} disabled={dbLoading}>
              <RefreshCw className={`mr-2 h-4 w-4 ${dbLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {dbError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-2 text-xs text-destructive">
              {dbError}
            </div>
          )}

          <div className="grid gap-4 lg:grid-cols-3">
            {(dbSnapshot?.databases ?? []).map(db => (
              <div key={db.name} className="rounded-lg border p-3 space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-semibold">{db.displayName || db.name}</p>
                    <p className="text-xs text-muted-foreground">{db.notes}</p>
                  </div>
                  <Badge variant={db.isUp ? 'default' : 'destructive'}>{db.isUp ? 'online' : 'offline'}</Badge>
                </div>

                <div className="space-y-1">
                  {Object.entries(db.metrics).map(([k, v]) => (
                    <div key={k} className="flex items-center justify-between text-xs">
                      <span className="font-mono text-muted-foreground">{k}</span>
                      <span className="font-mono">{formatMetric(v)}</span>
                    </div>
                  ))}
                </div>

                <Separator />

                <div className="space-y-2">
                  <p className="text-xs font-medium">Prometheus Query</p>
                  <div className="flex items-center gap-2">
                    <Input
                      className="h-8 text-xs font-mono"
                      value={dbQueries[db.name] ?? ''}
                      onChange={(e) => setDbQueries(prev => ({ ...prev, [db.name]: e.target.value }))}
                      placeholder="sum(rate(metric[5m]))"
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={dbBusy[`query:${db.name}`]}
                      onClick={() => { void runPromQuery(db.name); }}
                    >
                      Query
                    </Button>
                  </div>
                  {dbQueryResults[db.name] && (
                    <div className="text-xs text-muted-foreground font-mono">
                      {dbQueryResults[db.name]?.success
                        ? `value: ${formatMetric(dbQueryResults[db.name]?.value ?? null)}`
                        : dbQueryResults[db.name]?.message}
                    </div>
                  )}
                </div>

                <Separator />

                <div className="space-y-2">
                  <p className="text-xs font-medium">Maintenance</p>
                  <div className="flex flex-wrap gap-2">
                    {db.maintenanceActions.map(action => {
                      const dangerKey = `${db.name}:${action}`;
                      const dangerous = isDangerous(action);
                      return (
                        <Button
                          key={action}
                          variant="outline"
                          size="sm"
                          className="h-7 text-[11px]"
                          disabled={dbBusy[dangerKey] || (dangerous && !dangerConfirmed[dangerKey])}
                          onClick={() => { void runMaintenance(db.name, action); }}
                        >
                          {action}
                        </Button>
                      );
                    })}
                  </div>
                  {db.maintenanceActions.filter(isDangerous).map(action => {
                    const dangerKey = `${db.name}:${action}`;
                    return (
                      <label key={dangerKey} className="flex items-center gap-2 text-xs text-muted-foreground">
                        <input
                          type="checkbox"
                          checked={dangerConfirmed[dangerKey] ?? false}
                          onChange={(e) => setDangerConfirmed(prev => ({ ...prev, [dangerKey]: e.target.checked }))}
                        />
                        <ShieldAlert className="h-3.5 w-3.5" />
                        confirm dangerous command: {action}
                      </label>
                    );
                  })}
                  {maintenanceResults[db.name] && (
                    <div className="text-xs font-mono text-muted-foreground">
                      {maintenanceResults[db.name]?.success ? 'ok: ' : 'error: '}
                      {maintenanceResults[db.name]?.message}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Redis Key-Value CRUD</CardTitle>
              <CardDescription>Read, upsert, and delete Redis keys directly from observability.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-3 md:grid-cols-3">
                <Input value={redisKey} onChange={(e) => setRedisKey(e.target.value)} placeholder="key" />
                <Input value={redisValue} onChange={(e) => setRedisValue(e.target.value)} placeholder="value" />
                <Input value={redisTtlSeconds} onChange={(e) => setRedisTtlSeconds(e.target.value)} placeholder="ttl seconds (optional)" />
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" size="sm" disabled={dbBusy.redisRead} onClick={() => { void redisGetKey(); }}>
                  Read
                </Button>
                <Button variant="outline" size="sm" disabled={dbBusy.redisWrite} onClick={() => { void redisUpsertKey(); }}>
                  Upsert
                </Button>
                <Button variant="destructive" size="sm" disabled={dbBusy.redisDelete} onClick={() => { void redisDeleteKey(); }}>
                  Delete
                </Button>
              </div>
              {redisReadResult && (
                <div className="rounded border p-2 text-xs font-mono">
                  {redisReadResult.success
                    ? (redisReadResult.exists
                      ? `key=${redisReadResult.key} ttl=${redisReadResult.timeToLiveSeconds ?? 'none'} value=${redisReadResult.value}`
                      : `key=${redisReadResult.key} not found`)
                    : redisReadResult.message}
                </div>
              )}
              {redisWriteResult && (
                <div className="rounded border p-2 text-xs font-mono">
                  {(redisWriteResult.success ? 'ok: ' : 'error: ') + redisWriteResult.message}
                </div>
              )}
            </CardContent>
          </Card>
        </CardContent>
      </Card>

      <Card className="mb-4">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Frontend Runtime Status</CardTitle>
              <CardDescription>Local ECS runtime diagnostics</CardDescription>
            </div>
            <Badge variant={isFrontendRunning ? (isFrontendPaused ? 'outline' : 'default') : 'secondary'}>
              {isFrontendPaused ? 'Paused' : isFrontendRunning ? 'Running' : 'Stopped'}
            </Badge>
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Database className="h-4 w-4" />
                <span className="text-xs">Entities</span>
              </div>
              <p className="text-lg font-semibold">{metrics.entityCount}</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Activity className="h-4 w-4" />
                <span className="text-xs">FPS</span>
              </div>
              <p className="text-lg font-semibold">{metrics.fps.toFixed(1)}</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Cpu className="h-4 w-4" />
                <span className="text-xs">Tick Time</span>
              </div>
              <p className="text-lg font-semibold">{metrics.tickTime.toFixed(2)}ms</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Zap className="h-4 w-4" />
                <span className="text-xs">Systems</span>
              </div>
              <p className="text-lg font-semibold">{metrics.activeSystemCount}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Queue System Status</CardTitle>
          <CardDescription>Local pub/sub message queue</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Queue Size</span>
              <p className="text-lg font-semibold">{queueStats.queueSize}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Command Subscribers</span>
              <p className="text-lg font-semibold">{queueStats.subscriberCounts['runtime:command'] || 0}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Event Subscribers</span>
              <p className="text-lg font-semibold">{queueStats.subscriberCounts['runtime:events'] || 0}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Processing</span>
              <Badge variant={queueStats.isProcessing ? 'default' : 'secondary'}>
                {queueStats.isProcessing ? 'Active' : 'Idle'}
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="entities" className="flex-1 overflow-hidden">
        <TabsList className="grid w-full grid-cols-5">
          <TabsTrigger value="entities">Entities</TabsTrigger>
          <TabsTrigger value="systems">Systems</TabsTrigger>
          <TabsTrigger value="metrics">Metrics</TabsTrigger>
          <TabsTrigger value="events">Events</TabsTrigger>
          <TabsTrigger value="exporters"><Terminal className="mr-1 h-3.5 w-3.5 inline" />Exporters</TabsTrigger>
        </TabsList>

        <TabsContent value="entities" className="h-[calc(100%-3rem)] overflow-hidden">
          <div className="grid h-full gap-4 lg:grid-cols-2">
            <Card className="flex flex-col overflow-hidden">
              <CardHeader>
                <CardTitle>Entity List</CardTitle>
                <CardDescription>
                  {entities.length} active entities
                </CardDescription>
              </CardHeader>
              <CardContent className="flex-1 overflow-hidden p-0">
                <ScrollArea className="h-full px-6 pb-6">
                  <div className="space-y-2">
                    {entities.length === 0 ? (
                      <p className="py-8 text-center text-sm text-muted-foreground">
                        No entities in runtime. Create a test entity to get started.
                      </p>
                    ) : (
                      entities.map((entity) => (
                        <div
                          key={entity.id}
                          className={`cursor-pointer rounded-lg border p-3 transition-colors hover:bg-accent ${
                            selectedEntity?.id === entity.id ? 'border-primary bg-accent' : ''
                          }`}
                          onClick={() => setSelectedEntity(entity)}
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-2">
                              <Badge variant={entity.active ? 'default' : 'secondary'}>
                                {entity.active ? 'Active' : 'Inactive'}
                              </Badge>
                              <span className="text-sm font-mono">{entity.id.slice(0, 12)}...</span>
                            </div>
                            <span className="text-xs text-muted-foreground">
                              {entity.components.size} components
                            </span>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </ScrollArea>
              </CardContent>
            </Card>

            <Card className="flex flex-col overflow-hidden">
              <CardHeader>
                <CardTitle>Entity Inspector</CardTitle>
                <CardDescription>
                  {selectedEntity ? `Inspecting ${selectedEntity.id}` : 'Select an entity'}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex-1 overflow-hidden p-0">
                <ScrollArea className="h-full px-6 pb-6">
                  {selectedEntity ? (
                    <div className="space-y-4">
                      <div>
                        <h3 className="mb-2 text-sm font-semibold">Entity Info</h3>
                        <div className="space-y-1 text-sm">
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">ID:</span>
                            <span className="font-mono">{selectedEntity.id}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Active:</span>
                            <span>{selectedEntity.active ? 'Yes' : 'No'}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Created:</span>
                            <span>{new Date(selectedEntity.createdAt).toLocaleTimeString()}</span>
                          </div>
                        </div>
                      </div>

                      <Separator />

                      <div>
                        <h3 className="mb-2 text-sm font-semibold">Components</h3>
                        <div className="space-y-3">
                          {Array.from(selectedEntity.components.entries()).map(([type, data]) => (
                            <div key={type} className="rounded-lg border p-3">
                              <h4 className="mb-2 text-sm font-medium">{type}</h4>
                              <pre className="overflow-x-auto text-xs">
                                {JSON.stringify(data, null, 2)}
                              </pre>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      Select an entity to inspect its components
                    </p>
                  )}
                </ScrollArea>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="systems" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>System Profiler</CardTitle>
              <CardDescription>
                {metrics.activeSystemCount} active systems
              </CardDescription>
            </CardHeader>
            <CardContent className="overflow-hidden p-0">
              <ScrollArea className="h-full px-6 pb-6">
                <div className="space-y-2">
                  {Array.from(metrics.systemExecutionTimes.entries()).map(([systemId, time]) => (
                    <div key={systemId} className="rounded-lg border p-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium">{systemId}</span>
                        <Badge variant="outline">{time.toFixed(2)}ms</Badge>
                      </div>
                    </div>
                  ))}
                  {metrics.systemExecutionTimes.size === 0 && (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No systems registered yet
                    </p>
                  )}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="metrics" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>Performance Metrics</CardTitle>
              <CardDescription>
                Real-time runtime performance data
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Frame Rate</p>
                    <p className="text-2xl font-bold">{metrics.fps.toFixed(1)} FPS</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Tick Time</p>
                    <p className="text-2xl font-bold">{metrics.tickTime.toFixed(2)}ms</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Entity Count</p>
                    <p className="text-2xl font-bold">{metrics.entityCount}</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Active Systems</p>
                    <p className="text-2xl font-bold">{metrics.activeSystemCount}</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="events" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>Queue Events</CardTitle>
              <CardDescription>
                Recent local queue messages
              </CardDescription>
            </CardHeader>
            <CardContent className="overflow-hidden p-0">
              <ScrollArea className="h-full px-6 pb-6">
                <div className="space-y-1">
                  {recentEvents.length === 0 ? (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No events yet
                    </p>
                  ) : (
                    recentEvents.map((event, index) => (
                      <div key={index} className="rounded border-l-2 border-primary bg-muted/50 p-2 text-xs font-mono">
                        {event}
                      </div>
                    ))
                  )}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="exporters" className="h-[calc(100%-3rem)] overflow-hidden">
          <Card className="h-full flex flex-col overflow-hidden">
            <CardHeader>
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle>Exporter Verbose Output</CardTitle>
                  <CardDescription>Raw Prometheus /metrics pages from each exporter container. Useful for diagnosing partition health, scrape gaps, and metric cardinality.</CardDescription>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={exporterLoading[activeExporter]}
                  onClick={() => { void fetchExporterRaw(activeExporter); }}
                >
                  <RefreshCw className={`mr-2 h-4 w-4 ${exporterLoading[activeExporter] ? 'animate-spin' : ''}`} />
                  Refresh
                </Button>
              </div>
              <div className="mt-2 flex flex-wrap gap-2">
                {EXPORTERS.map(exp => {
                  const d = exporterData[exp];
                  return (
                    <button
                      key={exp}
                      onClick={() => setActiveExporter(exp)}
                      className={`rounded-md border px-3 py-1 text-xs font-medium transition-colors ${
                        activeExporter === exp
                          ? 'border-primary bg-primary text-primary-foreground'
                          : 'border-border bg-transparent hover:bg-accent'
                      }`}
                    >
                      {exp}
                      {d && (
                        <Badge
                          variant={d.success ? 'default' : 'destructive'}
                          className="ml-2 text-[10px] py-0 px-1"
                        >
                          {d.success ? `${d.lineCount}L` : 'err'}
                        </Badge>
                      )}
                    </button>
                  );
                })}
              </div>
            </CardHeader>
            <CardContent className="flex flex-col gap-3 flex-1 overflow-hidden">
              {(() => {
                const d = exporterData[activeExporter];
                const filter = (exporterFilter[activeExporter] ?? '').toLowerCase();
                if (!d) {
                  return (
                    <div className="text-xs text-muted-foreground">
                      {exporterLoading[activeExporter] ? 'Loading…' : 'No data yet — click Refresh.'}
                    </div>
                  );
                }
                if (!d.success) {
                  return (
                    <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-xs text-destructive font-mono">
                      Error fetching {d.exporter} metrics: {d.error}
                    </div>
                  );
                }
                const lines = filter
                  ? d.lines.filter(l => l.toLowerCase().includes(filter))
                  : d.lines;
                return (
                  <>
                    <div className="flex items-center gap-3">
                      <Input
                        className="h-8 text-xs font-mono max-w-sm"
                        placeholder="Filter metrics (e.g. memory, up, connected)"
                        value={exporterFilter[activeExporter] ?? ''}
                        onChange={(e) => setExporterFilter(prev => ({ ...prev, [activeExporter]: e.target.value }))}
                      />
                      <span className="text-xs text-muted-foreground whitespace-nowrap">
                        {lines.length} / {d.lineCount} lines &mdash; fetched {new Date(d.fetchedAtUtc).toLocaleTimeString()}
                      </span>
                    </div>
                    <ScrollArea className="flex-1 rounded-md border bg-muted/30">
                      <pre className="p-3 text-[11px] font-mono leading-[1.6] whitespace-pre-wrap break-all">
                        {lines.length === 0
                          ? '(no lines match filter)'
                          : lines.map((line, i) => {
                              const isComment = line.startsWith('#');
                              const isHelp = line.startsWith('# HELP');
                              const isType = line.startsWith('# TYPE');
                              return (
                                <span
                                  key={i}
                                  className={
                                    isHelp ? 'text-blue-400' :
                                    isType ? 'text-yellow-400' :
                                    isComment ? 'text-muted-foreground' :
                                    'text-foreground'
                                  }
                                >
                                  {line}
                                  {'\n'}
                                </span>
                              );
                            })}
                      </pre>
                    </ScrollArea>
                  </>
                );
              })()}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
