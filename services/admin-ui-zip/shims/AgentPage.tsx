import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import {
  Bot,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Clock,
  Loader2,
  RefreshCw,
  Send,
  SquareX,
  Terminal,
  XCircle,
} from 'lucide-react';

type TaskStatus = 'pending' | 'running' | 'done' | 'failed' | 'cancelled';

type AgentTask = {
  id: string;
  status: TaskStatus;
  description: string;
  result: string | null;
  agentLog: string;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
};

function resolveApiBase(): string {
  const stored = localStorage.getItem('agentApiBase');
  if (stored && !stored.startsWith('http://authoritative') && !stored.startsWith('http://agent')) return stored;
  return `${window.location.protocol}//${window.location.hostname}:${window.location.port || '80'}`;
}

function StatusBadge({ status }: { status: TaskStatus }) {
  const variants: Record<TaskStatus, { label: string; className: string; icon: React.ReactNode }> = {
    pending:   { label: 'Pending',   className: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',   icon: <Clock className="mr-1 h-3 w-3" /> },
    running:   { label: 'Running',   className: 'bg-blue-500/20 text-blue-400 border-blue-500/30',         icon: <Loader2 className="mr-1 h-3 w-3 animate-spin" /> },
    done:      { label: 'Done',      className: 'bg-green-500/20 text-green-400 border-green-500/30',       icon: <CheckCircle2 className="mr-1 h-3 w-3" /> },
    failed:    { label: 'Failed',    className: 'bg-red-500/20 text-red-400 border-red-500/30',             icon: <XCircle className="mr-1 h-3 w-3" /> },
    cancelled: { label: 'Cancelled', className: 'bg-gray-500/20 text-gray-400 border-gray-500/30',         icon: <SquareX className="mr-1 h-3 w-3" /> },
  };
  const v = variants[status];
  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${v.className}`}>
      {v.icon}{v.label}
    </span>
  );
}

function lineClass(line: string): string {
  if (line.startsWith('[TOOL]'))      return 'text-cyan-400';
  if (line.startsWith('[RESULT]'))    return 'text-gray-400';
  if (line.startsWith('[DONE]'))      return 'text-green-400 font-semibold';
  if (line.startsWith('[ERROR]'))     return 'text-red-400 font-semibold';
  if (line.startsWith('[CANCELLED]')) return 'text-yellow-400';
  if (line.startsWith('[MAX_ITER'))   return 'text-orange-400';
  if (line.startsWith('--- Iteration')) return 'text-indigo-400 font-semibold';
  if (line.startsWith('[') && line.includes('] Agent started')) return 'text-emerald-400';
  if (line.startsWith('Task:'))       return 'text-white/80';
  return 'text-gray-300';
}

export default function AgentPage() {
  const [apiBase, setApiBase] = useState(resolveApiBase);
  const [adminKey, setAdminKey] = useState(() => localStorage.getItem('adminKey') ?? 'dev-admin-key');
  const [settingsOpen, setSettingsOpen] = useState(false);

  const [description, setDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [tasks, setTasks] = useState<AgentTask[]>([]);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [tasksError, setTasksError] = useState<string | null>(null);

  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [selectedTask, setSelectedTask] = useState<AgentTask | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [logFilter, setLogFilter] = useState('');
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  const logScrollRef = useRef<HTMLDivElement>(null);
  const prevLogLenRef = useRef(0);

  const headers = useCallback(() => ({
    'Content-Type': 'application/json',
    'X-Admin-Key': adminKey,
  }), [adminKey]);

  const base = useMemo(() => apiBase.replace(/\/$/, ''), [apiBase]);

  // Persist settings
  useEffect(() => { localStorage.setItem('agentApiBase', apiBase); }, [apiBase]);
  useEffect(() => { localStorage.setItem('adminKey', adminKey); }, [adminKey]);

  // ── Task list polling ──────────────────────────────────────────────────────
  const fetchTasks = useCallback(async () => {
    setTasksLoading(true);
    setTasksError(null);
    try {
      const res = await fetch(`${base}/admin/agent/tasks`, { headers: headers() });
      if (!res.ok) throw new Error(`${res.status}: ${res.statusText}`);
      const data = (await res.json()) as AgentTask[];
      setTasks(data);
    } catch (e) {
      setTasksError(e instanceof Error ? e.message : 'Failed to load tasks');
    } finally {
      setTasksLoading(false);
    }
  }, [base, headers]);

  useEffect(() => { void fetchTasks(); }, [fetchTasks]);

  useEffect(() => {
    const id = window.setInterval(() => void fetchTasks(), 3000);
    return () => window.clearInterval(id);
  }, [fetchTasks]);

  // ── Selected task detail polling ───────────────────────────────────────────
  const fetchDetail = useCallback(async () => {
    if (!selectedTaskId) return;
    setDetailLoading(true);
    try {
      const res = await fetch(`${base}/admin/agent/tasks/${selectedTaskId}`, { headers: headers() });
      if (!res.ok) throw new Error(`${res.status}`);
      const data = (await res.json()) as AgentTask;
      setSelectedTask(data);
    } catch { /* keep stale data */ }
    finally { setDetailLoading(false); }
  }, [base, headers, selectedTaskId]);

  useEffect(() => { void fetchDetail(); }, [fetchDetail]);

  useEffect(() => {
    if (!selectedTask || !['running', 'pending'].includes(selectedTask.status)) return;
    const id = window.setInterval(() => void fetchDetail(), 2000);
    return () => window.clearInterval(id);
  }, [selectedTask, fetchDetail]);

  // Auto-scroll log when running
  useEffect(() => {
    if (!selectedTask || selectedTask.status !== 'running') return;
    const logLen = selectedTask.agentLog.length;
    if (logLen !== prevLogLenRef.current) {
      prevLogLenRef.current = logLen;
      if (logScrollRef.current) {
        logScrollRef.current.scrollTop = logScrollRef.current.scrollHeight;
      }
    }
  }, [selectedTask]);

  // ── Submit ─────────────────────────────────────────────────────────────────
  const handleSubmit = async () => {
    if (!description.trim()) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      const res = await fetch(`${base}/admin/agent/tasks`, {
        method: 'POST',
        headers: headers(),
        body: JSON.stringify({ description: description.trim() }),
      });
      if (!res.ok) throw new Error(`${res.status}: ${await res.text().catch(() => res.statusText)}`);
      const task = (await res.json()) as AgentTask;
      setDescription('');
      setTasks(prev => [task, ...prev]);
      setSelectedTaskId(task.id);
      setSelectedTask(task);
    } catch (e) {
      setSubmitError(e instanceof Error ? e.message : 'Failed to submit task');
    } finally {
      setSubmitting(false);
    }
  };

  // ── Cancel ─────────────────────────────────────────────────────────────────
  const handleCancel = async (taskId: string) => {
    setCancellingId(taskId);
    try {
      await fetch(`${base}/admin/agent/tasks/${taskId}`, {
        method: 'DELETE',
        headers: headers(),
      });
      await fetchTasks();
      if (selectedTaskId === taskId) void fetchDetail();
    } finally {
      setCancellingId(null);
    }
  };

  // ── Log rendering ──────────────────────────────────────────────────────────
  const filteredLogLines = useMemo(() => {
    if (!selectedTask) return [];
    const lines = selectedTask.agentLog.split('\n');
    if (!logFilter.trim()) return lines;
    const q = logFilter.toLowerCase();
    return lines.filter(l => l.toLowerCase().includes(q));
  }, [selectedTask, logFilter]);

  const formatTime = (iso: string) => {
    try { return new Date(iso).toLocaleTimeString(); } catch { return iso; }
  };

  const formatRelTime = (iso: string) => {
    const diffMs = Date.now() - new Date(iso).getTime();
    if (diffMs < 60000) return `${Math.floor(diffMs / 1000)}s ago`;
    if (diffMs < 3600000) return `${Math.floor(diffMs / 60000)}m ago`;
    return `${Math.floor(diffMs / 3600000)}h ago`;
  };

  return (
    <div className="flex h-full flex-col overflow-hidden p-4 sm:p-6">
      {/* ── Header ── */}
      <div className="mb-4 flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="flex items-center gap-2 text-2xl font-bold sm:text-3xl">
              <Bot className="h-7 w-7 text-primary" />
              AI Coding Agent
            </h1>
            <p className="text-sm text-muted-foreground">GPT-4o autonomous coding agent — reads, writes, and runs code in the container</p>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setSettingsOpen(v => !v)}
            className="gap-1 text-muted-foreground"
          >
            {settingsOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
            Settings
          </Button>
        </div>

        {settingsOpen && (
          <Card>
            <CardContent className="grid gap-3 p-4 md:grid-cols-2">
              <Input
                value={apiBase}
                onChange={e => setApiBase(e.target.value)}
                placeholder="API base URL"
                aria-label="API base URL"
              />
              <Input
                value={adminKey}
                onChange={e => setAdminKey(e.target.value)}
                placeholder="Admin key"
                type="password"
                aria-label="Admin key"
              />
            </CardContent>
          </Card>
        )}
      </div>

      {/* ── Main split layout ── */}
      <div className="flex min-h-0 flex-1 gap-4 overflow-hidden">

        {/* ── Left: Submit + Queue ── */}
        <div className="flex w-72 flex-none flex-col gap-3 overflow-hidden">
          {/* Submit */}
          <Card className="flex-none">
            <CardHeader className="pb-2 pt-3 px-4">
              <CardTitle className="flex items-center gap-2 text-sm font-semibold">
                <Send className="h-4 w-4 text-primary" />
                New Task
              </CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-4 pt-0">
              <Textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                placeholder="Describe what you want the agent to do…&#10;&#10;e.g. Add a /admin/ping endpoint that returns {ok: true} and wire it to the admin UI."
                className="mb-2 min-h-[120px] resize-none font-mono text-xs"
                onKeyDown={e => {
                  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) void handleSubmit();
                }}
              />
              {submitError && (
                <p className="mb-2 text-xs text-destructive">{submitError}</p>
              )}
              <Button
                className="w-full"
                onClick={handleSubmit}
                disabled={submitting || !description.trim()}
              >
                {submitting ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Send className="mr-2 h-4 w-4" />
                )}
                {submitting ? 'Submitting…' : 'Run Agent'}
              </Button>
              <p className="mt-1.5 text-center text-[10px] text-muted-foreground">Ctrl+Enter to submit</p>
            </CardContent>
          </Card>

          {/* Task queue */}
          <Card className="flex min-h-0 flex-1 flex-col">
            <CardHeader className="flex-none pb-2 pt-3 px-4">
              <CardTitle className="flex items-center justify-between text-sm font-semibold">
                <span className="flex items-center gap-2">
                  <Terminal className="h-4 w-4 text-muted-foreground" />
                  Queue
                  <Badge variant="secondary" className="text-xs">{tasks.length}</Badge>
                </span>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  onClick={() => void fetchTasks()}
                  disabled={tasksLoading}
                >
                  <RefreshCw className={`h-3 w-3 ${tasksLoading ? 'animate-spin' : ''}`} />
                </Button>
              </CardTitle>
            </CardHeader>
            <Separator />
            <ScrollArea className="flex-1">
              {tasksError ? (
                <p className="p-3 text-xs text-destructive">{tasksError}</p>
              ) : tasks.length === 0 ? (
                <p className="p-4 text-center text-xs text-muted-foreground">No tasks yet</p>
              ) : (
                <div className="p-2">
                  {tasks.map(task => (
                    <button
                      key={task.id}
                      onClick={() => {
                        setSelectedTaskId(task.id);
                        setSelectedTask(task);
                        void fetchDetail();
                      }}
                      className={`mb-1.5 w-full rounded-md p-2 text-left transition-colors hover:bg-muted/60 ${
                        selectedTaskId === task.id ? 'bg-muted ring-1 ring-primary/40' : ''
                      }`}
                    >
                      <div className="mb-1 flex items-center justify-between gap-1">
                        <StatusBadge status={task.status} />
                        <span className="text-[10px] text-muted-foreground">{formatRelTime(task.createdAt)}</span>
                      </div>
                      <p className="line-clamp-2 text-xs text-foreground/80">
                        {task.description}
                      </p>
                    </button>
                  ))}
                </div>
              )}
            </ScrollArea>
          </Card>
        </div>

        {/* ── Right: Task detail ── */}
        <Card className="flex min-h-0 flex-1 flex-col overflow-hidden">
          {selectedTask ? (
            <>
              <CardHeader className="flex-none pb-2">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <div className="mb-1 flex flex-wrap items-center gap-2">
                      <StatusBadge status={selectedTask.status} />
                      <span className="font-mono text-[10px] text-muted-foreground">
                        {selectedTask.id}
                      </span>
                      {detailLoading && <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />}
                    </div>
                    <p className="text-sm text-foreground/90 line-clamp-2">{selectedTask.description}</p>
                    <div className="mt-1 flex flex-wrap gap-3 text-[11px] text-muted-foreground">
                      <span>Created: {formatTime(selectedTask.createdAt)}</span>
                      {selectedTask.completedAt && (
                        <span>Completed: {formatTime(selectedTask.completedAt)}</span>
                      )}
                    </div>
                  </div>
                  <div className="flex flex-none gap-2">
                    {['pending', 'running'].includes(selectedTask.status) && (
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => void handleCancel(selectedTask.id)}
                        disabled={cancellingId === selectedTask.id}
                      >
                        {cancellingId === selectedTask.id ? (
                          <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                        ) : (
                          <SquareX className="mr-1 h-3 w-3" />
                        )}
                        Cancel
                      </Button>
                    )}
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void fetchDetail()}
                    >
                      <RefreshCw className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <Separator />

              {/* Result banner */}
              {selectedTask.result && selectedTask.status !== 'running' && (
                <>
                  <div className={`flex-none px-4 py-3 ${
                    selectedTask.status === 'done' ? 'bg-green-950/30' :
                    selectedTask.status === 'failed' ? 'bg-red-950/30' : 'bg-muted/30'
                  }`}>
                    <p className="mb-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                      {selectedTask.status === 'done' ? '✓ Result' : '✗ Failure Reason'}
                    </p>
                    <p className="whitespace-pre-wrap text-sm">{selectedTask.result}</p>
                  </div>
                  <Separator />
                </>
              )}

              {/* Log toolbar */}
              <div className="flex-none flex items-center gap-2 px-4 py-2">
                <Terminal className="h-4 w-4 flex-none text-muted-foreground" />
                <span className="text-xs font-medium text-muted-foreground">Agent Log</span>
                <Badge variant="outline" className="text-[10px]">
                  {filteredLogLines.length} lines
                </Badge>
                <div className="relative ml-auto w-44">
                  <Input
                    value={logFilter}
                    onChange={e => setLogFilter(e.target.value)}
                    placeholder="Filter log…"
                    className="h-7 pr-2 text-xs"
                  />
                </div>
              </div>

              {/* Log viewer */}
              <div
                ref={logScrollRef}
                className="min-h-0 flex-1 overflow-auto"
              >
                <pre className="p-4 text-[11px] leading-relaxed">
                  {filteredLogLines.length === 0 ? (
                    <span className="text-muted-foreground italic">
                      {selectedTask.agentLog ? 'No lines match filter' : 'Waiting for agent output…'}
                    </span>
                  ) : (
                    filteredLogLines.map((line, i) => (
                      <span key={i} className={`block ${lineClass(line)}`}>{line}</span>
                    ))
                  )}
                  {selectedTask.status === 'running' && (
                    <span className="inline-flex items-center gap-1 text-blue-400">
                      <Loader2 className="h-3 w-3 animate-spin" /> agent running…
                    </span>
                  )}
                </pre>
              </div>
            </>
          ) : (
            <div className="flex flex-1 flex-col items-center justify-center gap-3 text-muted-foreground">
              <Bot className="h-12 w-12 opacity-20" />
              <p className="text-sm">Select a task from the queue or submit a new one</p>
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
