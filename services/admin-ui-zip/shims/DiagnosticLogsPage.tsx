import { useCallback, useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AlertCircle, Bug, Clock, FileText, RefreshCw, Search, Server, Terminal } from 'lucide-react';

type DiagnosticLogEntry = {
  id: string;
  timestampUtc: string;
  level: string;
  category: string;
  eventName: string;
  message: string;
  service: string;
  environment: string;
  correlationId?: string;
  traceId?: string;
  spanId?: string;
  sessionId?: string;
  actorId?: string;
  entityId?: string;
  commandId?: string;
  sourceFile?: string;
  sourceMember?: string;
  sourceLine: number;
  exceptionType?: string;
  exceptionMessage?: string;
  exceptionStackTrace?: string;
  tags: Record<string, string>;
  properties: Record<string, string>;
  payloadHash?: string;
  isRedacted: boolean;
  retentionClass: string;
};

type DiagnosticQueryResult = {
  total: number;
  skip: number;
  take: number;
  entries: DiagnosticLogEntry[];
};

type ContainerLogFull = {
  containerName: string;
  capturedAtUtc: string;
  available: boolean;
  error?: string;
  tailRequested: number;
  lines: string[];
};

// Normalize any absolute localhost/127.0.0.1 URL saved from a previous session to
// an empty string so requests are sent as relative paths and proxied by nginx
// (container-to-container, no CORS).
function resolveDefaultApiBase(): string {
  const stored = localStorage.getItem('dungen.diagnosticApiBase');
  if (stored) {
    try {
      const { hostname } = new URL(stored);
      // Reject localhost, 127.0.0.1, and bare container hostnames (no dots = docker internal)
      if (hostname === 'localhost' || hostname === '127.0.0.1' || !hostname.includes('.')) return '';
    } catch {
      // not a valid URL — treat as relative
    }
    return stored;
  }
  const envUrl = import.meta.env.VITE_DIAGNOSTIC_API_URL as string | undefined;
  return envUrl || '';
}

function resolveDefaultAdminKey(): string {
  return (
    localStorage.getItem('dungen.diagnosticAdminKey') ||
    (import.meta.env.VITE_ADMIN_API_KEY as string | undefined) ||
    'dev-admin-key'
  );
}

const defaultApiBase = resolveDefaultApiBase();
const defaultAdminKey = resolveDefaultAdminKey();

function levelVariant(level: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (level === 'Error' || level === 'Critical') return 'destructive';
  if (level === 'Warning') return 'outline';
  if (level === 'Debug' || level === 'Trace') return 'secondary';
  return 'default';
}

function shortSource(entry: DiagnosticLogEntry): string {
  const file = entry.sourceFile?.split(/[\\/]/).pop() || 'unknown';
  return `${file}:${entry.sourceLine || 0}`;
}

function keyValueRows(values: Record<string, string>) {
  return Object.entries(values).map(([key, value]) => (
    <div key={key} className="flex items-start justify-between gap-3 rounded border border-border/60 px-2 py-1">
      <span className="text-muted-foreground">{key}</span>
      <span className="break-all text-right font-mono">{value}</span>
    </div>
  ));
}

export default function DiagnosticLogsPage() {
  const [apiBase, setApiBase] = useState(defaultApiBase);
  const [adminKey, setAdminKey] = useState(defaultAdminKey);
  const [level, setLevel] = useState('All');
  const [category, setCategory] = useState('');
  const [entityId, setEntityId] = useState('');
  const [correlationId, setCorrelationId] = useState('');
  const [textContains, setTextContains] = useState('');
  const [result, setResult] = useState<DiagnosticQueryResult>({ total: 0, skip: 0, take: 100, entries: [] });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Container logs state
  const KNOWN_CONTAINERS = [
    'authoritative-primary', 'authoritative-secondary', 'admin-ui',
    'generator-service', 'postgres', 'redis', 'rabbitmq', 'scylla',
    'prometheus', 'grafana', 'redis-exporter', 'postgres-exporter', 'rabbitmq-exporter',
  ];
  const [selectedContainer, setSelectedContainer] = useState(KNOWN_CONTAINERS[0]);
  const [containerTail, setContainerTail] = useState(250);
  const [containerFilter, setContainerFilter] = useState('');
  const [containerLog, setContainerLog] = useState<ContainerLogFull | null>(null);
  const [containerLoading, setContainerLoading] = useState(false);
  const [containerError, setContainerError] = useState<string | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(false);

  const selectedEntry = useMemo(
    () => result.entries.find((entry) => entry.id === selectedId) || result.entries[0] || null,
    [result.entries, selectedId],
  );

  const queryLogs = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    localStorage.setItem('dungen.diagnosticApiBase', apiBase);
    localStorage.setItem('dungen.diagnosticAdminKey', adminKey);

    try {
      const response = await fetch(`${apiBase.replace(/\/$/, '')}/admin/v1/diagnostics/logs/query`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Admin-Key': adminKey,
        },
        body: JSON.stringify({
          levels: level === 'All' ? undefined : [level],
          category: category.trim() || undefined,
          entityId: entityId.trim() || undefined,
          correlationId: correlationId.trim() || undefined,
          textContains: textContains.trim() || undefined,
          take: 100,
          descending: true,
        }),
      });

      if (!response.ok) {
        throw new Error(`Diagnostic API returned ${response.status}`);
      }

      const data = (await response.json()) as DiagnosticQueryResult;
      setResult(data);
      setSelectedId((previous) => {
        if (previous && data.entries.some((entry) => entry.id === previous)) return previous;
        return data.entries[0]?.id ?? null;
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Diagnostic query failed');
      setResult({ total: 0, skip: 0, take: 100, entries: [] });
      setSelectedId(null);
    } finally {
      setIsLoading(false);
    }
  }, [adminKey, apiBase, category, correlationId, entityId, level, textContains]);

  useEffect(() => {
    queryLogs();
  }, [queryLogs]);

  const fetchContainerLogs = useCallback(async () => {
    setContainerLoading(true);
    setContainerError(null);
    try {
      const base = apiBase.replace(/\/$/, '');
      const url = `${base}/admin/observability/containers/${encodeURIComponent(selectedContainer)}/logs?tail=${containerTail}`;
      const response = await fetch(url, {
        headers: { 'X-Admin-Key': adminKey },
      });
      if (!response.ok) throw new Error(`${response.status}: ${await response.text().catch(() => response.statusText)}`);
      const data = (await response.json()) as ContainerLogFull;
      setContainerLog(data);
    } catch (err) {
      setContainerError(err instanceof Error ? err.message : 'Failed to fetch container logs');
    } finally {
      setContainerLoading(false);
    }
  }, [adminKey, apiBase, selectedContainer, containerTail]);

  useEffect(() => {
    void fetchContainerLogs();
  }, [fetchContainerLogs]);

  useEffect(() => {
    if (!autoRefresh) return;
    const id = window.setInterval(() => { void fetchContainerLogs(); }, 5000);
    return () => window.clearInterval(id);
  }, [autoRefresh, fetchContainerLogs]);

  const counts = useMemo(() => {
    return result.entries.reduce(
      (acc, entry) => {
        acc[entry.level] = (acc[entry.level] || 0) + 1;
        return acc;
      },
      {} as Record<string, number>,
    );
  }, [result.entries]);

  const filteredContainerLines = useMemo(() => {
    if (!containerLog?.lines) return [];
    const q = containerFilter.trim().toLowerCase();
    if (!q) return containerLog.lines;
    return containerLog.lines.filter(l => l.toLowerCase().includes(q));
  }, [containerLog?.lines, containerFilter]);

  function lineClass(line: string) {
    const l = line.toLowerCase();
    if (l.includes('error') || l.includes('exception') || l.includes('fatal') || l.includes('crit'))
      return 'text-destructive';
    if (l.includes('warn'))
      return 'text-yellow-500 dark:text-yellow-400';
    return 'text-foreground/80';
  }

  return (
    <div className="flex h-full flex-col overflow-hidden p-4 sm:p-6">
      <div className="mb-4 flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold sm:text-3xl">
            <Bug className="h-7 w-7 text-primary" />
            Backend Logs
          </h1>
          <p className="text-sm text-muted-foreground">Diagnostic events and live Docker container logs</p>
        </div>
      </div>

      <Tabs defaultValue="diagnostic" className="flex min-h-0 flex-1 flex-col">
        <TabsList className="mb-4 w-fit">
          <TabsTrigger value="diagnostic">
            <FileText className="mr-2 h-4 w-4" />
            Diagnostic Logs
          </TabsTrigger>
          <TabsTrigger value="containers">
            <Terminal className="mr-2 h-4 w-4" />
            Container Logs
          </TabsTrigger>
        </TabsList>

        {/* ── Diagnostic Logs Tab ── */}
        <TabsContent value="diagnostic" className="flex min-h-0 flex-1 flex-col">
      <Card className="mb-4">
        <CardContent className="grid gap-3 p-4 md:grid-cols-2 xl:grid-cols-6">
          <Input value={apiBase} onChange={(event) => setApiBase(event.target.value)} aria-label="Diagnostic API URL" placeholder="API base URL" />
          <Input value={adminKey} onChange={(event) => setAdminKey(event.target.value)} aria-label="Admin API key" placeholder="Admin key" type="password" />
          <select
            value={level}
            onChange={(event) => setLevel(event.target.value)}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
            aria-label="Log level"
          >
            <option>All</option>
            <option>Trace</option>
            <option>Debug</option>
            <option>Information</option>
            <option>Warning</option>
            <option>Error</option>
            <option>Critical</option>
          </select>
          <Input value={category} onChange={(event) => setCategory(event.target.value)} placeholder="Category" />
          <Input value={entityId} onChange={(event) => setEntityId(event.target.value)} placeholder="Entity ID" />
          <Input value={correlationId} onChange={(event) => setCorrelationId(event.target.value)} placeholder="Correlation ID" />
          <div className="flex gap-2">
            <Input value={textContains} onChange={(event) => setTextContains(event.target.value)} placeholder="Search text" />
            <Button onClick={queryLogs} variant="outline" size="icon" aria-label="Search logs">
              <Search className="h-4 w-4" />
            </Button>
          </div>
        </CardContent>
      </Card>

      {error && (
        <div className="mb-4 rounded-md border border-destructive/50 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="mr-2 inline h-4 w-4" />
          {error}
        </div>
      )}

      <div className="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Card>
          <CardContent className="flex items-center gap-3 p-4">
            <FileText className="h-5 w-5 text-primary" />
            <div>
              <p className="text-xs text-muted-foreground">Total</p>
              <p className="text-xl font-semibold">{result.total}</p>
            </div>
          </CardContent>
        </Card>
        {['Information', 'Warning', 'Error', 'Critical'].map((name) => (
          <Card key={name}>
            <CardContent className="p-4">
              <p className="text-xs text-muted-foreground">{name}</p>
              <p className="text-xl font-semibold">{counts[name] || 0}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid min-h-0 flex-1 gap-4 xl:grid-cols-[minmax(360px,0.95fr)_minmax(420px,1.05fr)]">
        <Card className="min-h-0 overflow-hidden">
          <CardHeader>
            <CardTitle>Events</CardTitle>
          </CardHeader>
          <CardContent className="h-[calc(100%-5rem)] overflow-hidden p-0">
            <ScrollArea className="h-full px-4 pb-4">
              <div className="space-y-2">
                {result.entries.length === 0 ? (
                  <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                    No diagnostic entries matched the current query.
                  </div>
                ) : (
                  result.entries.map((entry) => (
                    <button
                      key={entry.id}
                      onClick={() => setSelectedId(entry.id)}
                      className={`w-full rounded-md border p-3 text-left transition-colors hover:bg-accent ${
                        selectedEntry?.id === entry.id ? 'border-primary bg-accent' : 'border-border'
                      }`}
                    >
                      <div className="mb-2 flex flex-wrap items-center gap-2">
                        <Badge variant={levelVariant(entry.level)}>{entry.level}</Badge>
                        <span className="text-xs text-muted-foreground">{entry.category}</span>
                      </div>
                      <div className="font-medium">{entry.eventName}</div>
                      <div className="mt-1 line-clamp-2 text-xs text-muted-foreground">{entry.message}</div>
                      <div className="mt-2 flex flex-wrap gap-2 text-[11px] text-muted-foreground">
                        <span className="inline-flex items-center gap-1">
                          <Clock className="h-3 w-3" />
                          {new Date(entry.timestampUtc).toLocaleString()}
                        </span>
                        <span>{shortSource(entry)}</span>
                      </div>
                    </button>
                  ))
                )}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        <Card className="min-h-0 overflow-hidden">
          <CardHeader>
            <CardTitle>Entry Detail</CardTitle>
          </CardHeader>
          <CardContent className="h-[calc(100%-5rem)] overflow-hidden p-0">
            <ScrollArea className="h-full px-6 pb-6">
              {selectedEntry ? (
                <div className="space-y-5 text-sm">
                  <div>
                    <div className="mb-2 flex flex-wrap items-center gap-2">
                      <Badge variant={levelVariant(selectedEntry.level)}>{selectedEntry.level}</Badge>
                      <Badge variant="outline">{selectedEntry.retentionClass}</Badge>
                      {selectedEntry.isRedacted && <Badge variant="secondary">Redacted</Badge>}
                    </div>
                    <h2 className="text-lg font-semibold">{selectedEntry.eventName}</h2>
                    <p className="mt-1 text-muted-foreground">{selectedEntry.message}</p>
                  </div>

                  <Separator />

                  <div className="grid gap-2 md:grid-cols-2">
                    <div className="rounded-md border p-3">
                      <p className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <Server className="h-3.5 w-3.5" />
                        Service
                      </p>
                      <p className="font-mono">{selectedEntry.service}</p>
                    </div>
                    <div className="rounded-md border p-3">
                      <p className="mb-1 text-xs text-muted-foreground">Source</p>
                      <p className="break-all font-mono">{shortSource(selectedEntry)}</p>
                      <p className="mt-1 break-all text-xs text-muted-foreground">{selectedEntry.sourceMember || 'unknown'}</p>
                    </div>
                  </div>

                  <div className="grid gap-2 md:grid-cols-2">
                    {[
                      ['ID', selectedEntry.id],
                      ['Correlation', selectedEntry.correlationId],
                      ['Trace', selectedEntry.traceId],
                      ['Span', selectedEntry.spanId],
                      ['Session', selectedEntry.sessionId],
                      ['Actor', selectedEntry.actorId],
                      ['Entity', selectedEntry.entityId],
                      ['Command', selectedEntry.commandId],
                      ['Payload Hash', selectedEntry.payloadHash],
                    ].map(([label, value]) => (
                      <div key={label} className="rounded-md border px-3 py-2">
                        <p className="text-xs text-muted-foreground">{label}</p>
                        <p className="break-all font-mono text-xs">{value || '-'}</p>
                      </div>
                    ))}
                  </div>

                  {selectedEntry.exceptionType && (
                    <>
                      <Separator />
                      <div>
                        <h3 className="mb-2 font-semibold">Exception</h3>
                        <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3">
                          <p className="font-mono text-xs">{selectedEntry.exceptionType}</p>
                          <p className="mt-1 text-sm">{selectedEntry.exceptionMessage}</p>
                        </div>
                      </div>
                    </>
                  )}

                  <div className="grid gap-4 md:grid-cols-2">
                    <div>
                      <h3 className="mb-2 font-semibold">Tags</h3>
                      <div className="space-y-1 text-xs">
                        {Object.keys(selectedEntry.tags || {}).length > 0 ? keyValueRows(selectedEntry.tags) : <p className="text-muted-foreground">-</p>}
                      </div>
                    </div>
                    <div>
                      <h3 className="mb-2 font-semibold">Properties</h3>
                      <div className="space-y-1 text-xs">
                        {Object.keys(selectedEntry.properties || {}).length > 0 ? keyValueRows(selectedEntry.properties) : <p className="text-muted-foreground">-</p>}
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                  Select a diagnostic entry.
                </div>
              )}
            </ScrollArea>
          </CardContent>
        </Card>
      </div>
        </TabsContent>

        {/* ── Container Logs Tab ── */}
        <TabsContent value="containers" className="flex min-h-0 flex-1 flex-col gap-4">
          {/* Controls */}
          <Card>
            <CardContent className="flex flex-wrap items-center gap-3 p-4">
              <div className="flex flex-wrap gap-1.5">
                {KNOWN_CONTAINERS.map(name => (
                  <button
                    key={name}
                    onClick={() => setSelectedContainer(name)}
                    className={`rounded-md border px-2.5 py-1 text-xs font-mono transition-colors ${
                      selectedContainer === name
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-background hover:bg-accent'
                    }`}
                  >
                    {name}
                  </button>
                ))}
              </div>
              <div className="ml-auto flex items-center gap-2">
                <select
                  value={containerTail}
                  onChange={e => setContainerTail(Number(e.target.value))}
                  className="h-9 rounded-md border border-input bg-background px-2 text-xs"
                >
                  <option value={50}>50 lines</option>
                  <option value={100}>100 lines</option>
                  <option value={250}>250 lines</option>
                  <option value={500}>500 lines</option>
                  <option value={1000}>1000 lines</option>
                </select>
                <label className="flex cursor-pointer items-center gap-1.5 text-xs text-muted-foreground">
                  <input
                    type="checkbox"
                    checked={autoRefresh}
                    onChange={e => setAutoRefresh(e.target.checked)}
                    className="h-3.5 w-3.5"
                  />
                  Auto (5s)
                </label>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => { void fetchContainerLogs(); }}
                  disabled={containerLoading}
                >
                  <RefreshCw className={`mr-2 h-4 w-4 ${containerLoading ? 'animate-spin' : ''}`} />
                  Refresh
                </Button>
              </div>
            </CardContent>
          </Card>

          {containerError && (
            <div className="rounded-md border border-destructive/50 bg-destructive/10 px-4 py-3 text-sm text-destructive">
              <AlertCircle className="mr-2 inline h-4 w-4" />
              {containerError}
            </div>
          )}

          {/* Log viewer */}
          <Card className="flex min-h-0 flex-1 flex-col overflow-hidden">
            <CardHeader className="flex-none pb-2">
              <div className="flex items-center justify-between gap-3">
                <CardTitle className="flex items-center gap-2 font-mono text-base">
                  <Terminal className="h-4 w-4" />
                  {selectedContainer}
                  {containerLog && (
                    <Badge variant={containerLog.available ? 'default' : 'destructive'} className="font-sans text-xs">
                      {containerLog.available ? `${filteredContainerLines.length} lines` : 'unavailable'}
                    </Badge>
                  )}
                </CardTitle>
                <div className="flex items-center gap-2">
                  <Search className="h-4 w-4 text-muted-foreground" />
                  <Input
                    className="h-8 w-48 text-xs"
                    placeholder="Filter lines…"
                    value={containerFilter}
                    onChange={e => setContainerFilter(e.target.value)}
                  />
                </div>
              </div>
              {containerLog && (
                <p className="text-[11px] text-muted-foreground">
                  Captured {new Date(containerLog.capturedAtUtc).toLocaleString()} · tail={containerLog.tailRequested}
                  {containerFilter && ` · showing ${filteredContainerLines.length} of ${containerLog.lines.length}`}
                </p>
              )}
            </CardHeader>
            <CardContent className="min-h-0 flex-1 overflow-hidden p-0">
              <ScrollArea className="h-full">
                {!containerLog ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    {containerLoading ? 'Loading…' : 'Select a container to view its logs.'}
                  </div>
                ) : !containerLog.available ? (
                  <div className="p-6 text-sm text-destructive">
                    <AlertCircle className="mr-2 inline h-4 w-4" />
                    {containerLog.error ?? 'Log source unavailable'}
                  </div>
                ) : filteredContainerLines.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    {containerFilter ? 'No lines match filter.' : 'No log output in this tail window.'}
                  </div>
                ) : (
                  <pre className="p-4 font-mono text-[11px] leading-relaxed">
                    {filteredContainerLines.map((line, i) => (
                      <div key={i} className={`${lineClass(line)} hover:bg-muted/40 rounded px-1`}>
                        {line}
                      </div>
                    ))}
                  </pre>
                )}
              </ScrollArea>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
