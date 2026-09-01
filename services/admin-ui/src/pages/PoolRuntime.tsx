import { useState, useEffect, useCallback } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  getDungeonPoolStatus,
  claimDungeon,
  generatePoolBatch,
  setPoolConfig,
  getPipelineRequests,
  approvePipelineRequest,
  rejectPipelineRequest,
  getPipelineExecutions,
  reloadPipelineRuntime,
  executePipeline,
  type PipelineRequest,
  type PipelineExecutionRecord,
} from '@/lib/api'
import { Loader2, RefreshCw, Gauge, Server, CheckCircle2, XCircle, Rocket } from 'lucide-react'

export default function PoolRuntime() {
  const [pool, setPool] = useState<Record<string, unknown> | null>(null)
  const [requests, setRequests] = useState<PipelineRequest[]>([])
  const [executions, setExecutions] = useState<PipelineExecutionRecord[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [claimLevel, setClaimLevel] = useState(5)
  const [batchLevel, setBatchLevel] = useState(5)
  const [batchCount, setBatchCount] = useState(5)
  const [ratio, setRatio] = useState(0.75)

  const refresh = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const [p, r, e] = await Promise.allSettled([
        getDungeonPoolStatus(),
        getPipelineRequests(),
        getPipelineExecutions(25),
      ])
      if (p.status === 'fulfilled') setPool(p.value as Record<string, unknown>)
      if (r.status === 'fulfilled') setRequests((r.value as PipelineRequest[]) ?? [])
      if (e.status === 'fulfilled') setExecutions((e.value as PipelineExecutionRecord[]) ?? [])
    } catch (e) { setError(String(e)) } finally { setLoading(false) }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const guard = (label: string, fn: () => Promise<void>) => async () => {
    setError(null); setNotice(null)
    try { await fn() } catch (e) { setError(`${label}: ${String(e)}`) }
  }

  return (
    <div className="p-6 space-y-6 max-w-6xl">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <Gauge className="h-5 w-5 text-primary" /> Pool & Runtime
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage the dungeon pool, generation ratio, and pipeline request/execution lifecycle.
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
      {notice && (
        <div className="rounded-md bg-emerald-500/10 border border-emerald-500/30 p-3 text-xs text-emerald-400 font-mono">
          {notice}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Pool status + controls */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Server className="h-4 w-4" /> Dungeon Pool
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-md bg-secondary p-3 text-xs font-mono text-muted-foreground whitespace-pre-wrap">
              {pool ? JSON.stringify(pool, null, 2) : 'Loading pool status…'}
            </div>

            <Separator />

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Claim difficulty</Label>
                <Input type="number" min={1} max={60} value={claimLevel} onChange={e => setClaimLevel(Number(e.target.value))} />
              </div>
              <div className="flex items-end">
                <Button
                  variant="outline"
                  className="w-full"
                  onClick={guard('Claim', async () => {
                    const res = await claimDungeon(claimLevel)
                    setNotice(`Claimed: ${JSON.stringify(res)}`)
                    await refresh()
                  })}
                >
                  Claim
                </Button>
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1.5">
                <Label>Batch level</Label>
                <Input type="number" min={1} max={60} value={batchLevel} onChange={e => setBatchLevel(Number(e.target.value))} />
              </div>
              <div className="space-y-1.5">
                <Label>Count</Label>
                <Input type="number" min={1} max={500} value={batchCount} onChange={e => setBatchCount(Number(e.target.value))} />
              </div>
              <div className="flex items-end">
                <Button
                  variant="outline"
                  className="w-full"
                  onClick={guard('Generate batch', async () => {
                    const res = await generatePoolBatch(batchLevel, batchCount)
                    setNotice(res.message)
                    await refresh()
                  })}
                >
                  <Rocket className="h-4 w-4 mr-1.5" /> Generate
                </Button>
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1.5 col-span-2">
                <Label>Generation ratio</Label>
                <Input type="number" min={0} max={1} step={0.05} value={ratio} onChange={e => setRatio(Number(e.target.value))} />
              </div>
              <div className="flex items-end">
                <Button
                  variant="outline"
                  className="w-full"
                  onClick={guard('Set ratio', async () => {
                    const res = await setPoolConfig(ratio)
                    setNotice(res.message)
                    await refresh()
                  })}
                >
                  Set Ratio
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Pipeline executions + runtime */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Rocket className="h-4 w-4" /> Runtime & Executions
            </CardTitle>
            <CardDescription>Latest executions and runtime controls</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={guard('Reload runtime', async () => {
                  await reloadPipelineRuntime()
                  setNotice('Runtime reloaded.')
                  await refresh()
                })}
              >
                <RefreshCw className="h-3.5 w-3.5 mr-1.5" /> Reload Runtime
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={guard('Execute', async () => {
                  const res = await executePipeline({})
                  setNotice(`Execution started: ${(res as Record<string, string>)?.executionId ?? 'done'}`)
                  await refresh()
                })}
              >
                <Rocket className="h-3.5 w-3.5 mr-1.5" /> Execute Pipeline
              </Button>
            </div>
            <ScrollArea className="h-64 rounded-md border border-border">
              <div className="divide-y divide-border">
                {executions.length === 0 && <p className="p-3 text-sm text-muted-foreground">No executions yet.</p>}
                {executions.map(e => (
                  <div key={e.executionId} className="p-3 text-xs">
                    <div className="flex items-center gap-2">
                      <Badge variant={e.status?.includes('complete') ? 'default' : e.status?.includes('fail') ? 'destructive' : 'secondary'}>
                        {e.status ?? 'unknown'}
                      </Badge>
                      <span className="font-mono">{e.executionId}</span>
                      <span className="ml-auto text-muted-foreground">{e.sessionId ?? 'no session'}</span>
                    </div>
                    {e.world && (
                      <div className="mt-1 text-muted-foreground font-mono">
                        {e.world.rooms?.length ?? 0} rooms · {e.world.enemies?.length ?? 0} enemies · {e.world.loot?.length ?? 0} loot
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>
      </div>

      {/* Pipeline requests */}
      <Card>
        <CardHeader><CardTitle className="text-base">Pipeline Requests</CardTitle></CardHeader>
        <CardContent>
          <ScrollArea className="h-72 rounded-md border border-border">
            <div className="divide-y divide-border">
              {requests.length === 0 && <p className="p-3 text-sm text-muted-foreground">No pipeline requests.</p>}
              {requests.map(r => (
                <div key={r.requestId} className="p-3">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium">{r.pipelineName ?? r.requestId}</span>
                    <Badge variant={r.status === 'Pending' ? 'secondary' : r.status?.includes('Approved') ? 'default' : 'outline'}>
                      {r.status ?? 'unknown'}
                    </Badge>
                    <span className="ml-auto text-muted-foreground font-mono text-[11px]">{r.requestId}</span>
                  </div>
                  {r.submittedBy && (
                    <div className="mt-1 text-xs text-muted-foreground">
                      submitted by {r.submittedBy}
                      {r.submittedAtUtc ? ` · ${new Date(r.submittedAtUtc).toLocaleString()}` : ''}
                    </div>
                  )}
                  {r.status === 'Pending' && (
                    <div className="mt-2 flex gap-2">
                      <Button
                        size="sm"
                        onClick={guard('Approve', async () => {
                          await approvePipelineRequest(r.requestId, { approvedBy: 'admin-ui' })
                          setNotice(`Approved ${r.requestId}`)
                          await refresh()
                        })}
                      >
                        <CheckCircle2 className="h-3.5 w-3.5 mr-1.5" /> Approve
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={guard('Reject', async () => {
                          await rejectPipelineRequest(r.requestId, 'Rejected from admin panel')
                          setNotice(`Rejected ${r.requestId}`)
                          await refresh()
                        })}
                      >
                        <XCircle className="h-3.5 w-3.5 mr-1.5" /> Reject
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>
    </div>
  )
}
