import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { ScrollArea } from '@/components/ui/scroll-area'
import { createPipelineRequest, approvePipelineRequest, executeWorldViaJob, getJobIngestion, ingestWorldSession, type GeneratorJobIngestion, type WorldIngestResult } from '@/lib/api'
import { CheckCircle2, Circle, Loader2, ChevronDown, ChevronUp, Download, Database, XCircle, Send } from 'lucide-react'

type Step = 'idle' | 'creating' | 'created' | 'approving' | 'approved' | 'executing' | 'done' | 'error'

interface WorldResult {
  executionId?: string
  sessionId?: string
  roomCount?: number
  enemyCount?: number
  lootCount?: number
  terrainVertexCount?: number
  terrainTriangleCount?: number
  mapWidth?: number
  mapHeight?: number
  dungeonLevel?: number
  status?: string
  raw?: unknown
}

export default function WorldGenerator() {
  const [form, setForm] = useState({
    name: 'World-001', dungeonLevel: 5, width: 80, height: 48,
    enemyCount: 20, lootCount: 10,
  })
  const [step, setStep]       = useState<Step>('idle')
  const [requestId, setReqId] = useState<string | null>(null)
  const [sessionId, setSessId]= useState<string | null>(null)
  const [result, setResult]   = useState<WorldResult | null>(null)
  const [error, setError]     = useState<string | null>(null)
  const [jsonOpen, setJsonOpen] = useState(false)
  const [ingestion, setIngestion] = useState<GeneratorJobIngestion | null>(null)
  const [ingestBusy, setIngestBusy] = useState(false)
  const [ingestResult, setIngestResult] = useState<WorldIngestResult | null>(null)

  const set = (k: keyof typeof form, v: string | number) =>
    setForm(f => ({ ...f, [k]: typeof f[k] === 'number' ? Number(v) : v }))

  const run = async () => {
    setError(null)
    setResult(null)
    setReqId(null)
    setSessId(null)
    setIngestion(null)

    try {
      // 1. Create
      setStep('creating')
      const created = await createPipelineRequest({
        pipelineName: form.name,
        dungeonLevel: form.dungeonLevel,
        width:        form.width,
        height:       form.height,
        enemyCount:   form.enemyCount,
        lootCount:    form.lootCount,
      })
      const rid = created?.requestId ?? (created as Record<string, string>)?.id ?? String(Date.now())
      setReqId(rid)

      // 2. Approve
      setStep('approving')
      const approved = await approvePipelineRequest(rid, { approvedBy: 'admin-ui' })
      const sid = (approved as Record<string, string>)?.sessionId ?? rid
      setSessId(sid)

      // 3. Execute
      setStep('executing')
      const { jobId, execution } = await executeWorldViaJob({
        sessionId: sid,
        notes: `World generator request ${rid}`,
      })

      setResult({
        executionId:  execution.executionId,
        sessionId:    execution.sessionId ?? sid,
        roomCount:    execution.world.rooms.length,
        enemyCount:   execution.world.enemies.length,
        lootCount:    execution.world.loot.length,
        terrainVertexCount: execution.world.terrainMesh?.vertices.length ?? 0,
        terrainTriangleCount: Math.floor((execution.world.terrainMesh?.triangles.length ?? 0) / 3),
        mapWidth:     execution.world.width,
        mapHeight:    execution.world.height,
        dungeonLevel: execution.world.dungeonLevel,
        status:       execution.status,
        raw:          execution,
      })

      // 4. Confirm the world was ingested into ScyllaDB (persistence is async,
      //    so poll briefly until it lands or we run out of patience).
      for (let attempt = 0; attempt < 15; attempt++) {
        const check = await getJobIngestion(jobId)
        setIngestion(check)
        if (check.worldPersisted) break
        if (!check.scyllaAvailable) break
        await new Promise(r => setTimeout(r, 300))
      }

      setStep('done')
    } catch (e) {
      setError(String(e))
      setStep('error')
    }
  }

  const downloadJson = () => {
    const blob = new Blob([JSON.stringify(result?.raw, null, 2)], { type: 'application/json' })
    const url  = URL.createObjectURL(blob)
    const a    = document.createElement('a')
    a.href = url; a.download = `world-${result?.executionId ?? Date.now()}.json`
    a.click(); URL.revokeObjectURL(url)
  }

  const ingestToAuthority = async () => {
    const raw = result?.raw as Record<string, unknown> | undefined
    const world = raw?.world as Record<string, unknown> | undefined
    const sid = result?.sessionId
    if (!sid || !world) { setError('No generated world payload available to ingest.'); return }
    setIngestBusy(true); setError(null); setIngestResult(null)
    try {
      const res = await ingestWorldSession(sid, {
        executionId: result?.executionId,
        pipelineId: 'world-pipeline',
        notes: 'Ingested from World Generator (admin-ui)',
        world: world as never,
      })
      setIngestResult(res)
      setStep('done')
    } catch (e) {
      setError(`Ingest failed: ${String(e)}`)
    } finally {
      setIngestBusy(false)
    }
  }

  const steps: { id: Step; label: string }[] = [
    { id: 'creating',  label: 'Create Request' },
    { id: 'approving', label: 'Approve'         },
    { id: 'executing', label: 'Execute'          },
    { id: 'done',      label: 'Complete'         },
  ]
  const stepOrder = ['idle','creating','created','approving','approved','executing','done','error']
  const stepIdx   = (s: Step) => stepOrder.indexOf(s)

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-semibold">World Generator</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Create, approve, and execute a world generation pipeline.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Form */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Pipeline Config</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-1.5">
              <Label>Pipeline Name</Label>
              <Input value={form.name} onChange={e => set('name', e.target.value)} placeholder="World-001" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Dungeon Level</Label>
                <Input type="number" min={1} max={60} value={form.dungeonLevel} onChange={e => set('dungeonLevel', e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label>Enemies</Label>
                <Input type="number" min={0} max={200} value={form.enemyCount} onChange={e => set('enemyCount', e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label>Map Width</Label>
                <Input type="number" min={20} max={512} value={form.width} onChange={e => set('width', e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label>Map Height</Label>
                <Input type="number" min={20} max={512} value={form.height} onChange={e => set('height', e.target.value)} />
              </div>
              <div className="space-y-1.5 col-span-2">
                <Label>Loot Items</Label>
                <Input type="number" min={0} max={100} value={form.lootCount} onChange={e => set('lootCount', e.target.value)} />
              </div>
            </div>
            <Button onClick={run} disabled={step === 'creating' || step === 'approving' || step === 'executing'} className="w-full">
              {(step === 'creating' || step === 'approving' || step === 'executing')
                ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Running…</>
                : 'Generate World'}
            </Button>
          </CardContent>
        </Card>

        {/* Progress + Results */}
        <div className="space-y-4">
          {/* Steps */}
          <Card>
            <CardHeader><CardTitle className="text-base">Pipeline Steps</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-3">
                {steps.map((s, i) => {
                  const current = stepIdx(step)
                  const target  = stepOrder.indexOf(s.id)
                  const done    = current > target
                  const active  = current === target || (s.id === 'creating' && step === 'created') || (s.id === 'approving' && step === 'approved')
                  const running = (s.id === 'creating' && step === 'creating') ||
                                  (s.id === 'approving' && (step === 'approving')) ||
                                  (s.id === 'executing' && step === 'executing')
                  return (
                    <div key={s.id} className="flex items-center gap-3">
                      {done
                        ? <CheckCircle2 className="h-5 w-5 text-primary shrink-0" />
                        : running
                          ? <Loader2 className="h-5 w-5 animate-spin text-primary shrink-0" />
                          : <Circle className={`h-5 w-5 shrink-0 ${active ? 'text-primary' : 'text-muted-foreground'}`} />}
                      <span className={done || active ? 'text-sm text-foreground' : 'text-sm text-muted-foreground'}>
                        {s.label}
                        {s.id === 'creating' && requestId && <span className="ml-2 text-xs text-muted-foreground font-mono">{requestId.slice(0, 8)}…</span>}
                      </span>
                      {i < steps.length - 1 && (
                        <div className={`ml-auto h-px flex-1 ${done ? 'bg-primary' : 'bg-border'}`} />
                      )}
                    </div>
                  )
                })}
              </div>
              {error && (
                <div className="mt-4 rounded-md bg-destructive/10 border border-destructive/30 p-3 text-xs text-destructive-foreground font-mono">
                  {error}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Result */}
          {result && step === 'done' && (
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">World Generated</CardTitle>
                  <Badge variant="default">completed</Badge>
                </div>
                {result.executionId && (
                  <CardDescription className="font-mono text-xs">{result.executionId}</CardDescription>
                )}
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid grid-cols-3 gap-2 text-center">
                  {[
                    { label: 'Rooms',   value: result.roomCount   ?? '—' },
                    { label: 'Enemies', value: result.enemyCount  ?? '—' },
                    { label: 'Loot',    value: result.lootCount   ?? '—' },
                  ].map(({ label, value }) => (
                    <div key={label} className="rounded-md bg-secondary p-3">
                      <div className="text-xl font-bold">{value}</div>
                      <div className="text-xs text-muted-foreground">{label}</div>
                    </div>
                  ))}
                </div>
                <div className="grid grid-cols-2 gap-2 text-center">
                  {[
                    { label: 'Terrain Vertices', value: result.terrainVertexCount ?? '—' },
                    { label: 'Terrain Triangles', value: result.terrainTriangleCount ?? '—' },
                  ].map(({ label, value }) => (
                    <div key={label} className="rounded-md bg-card/50 border border-border p-3">
                      <div className="text-base font-semibold">{value}</div>
                      <div className="text-xs text-muted-foreground">{label}</div>
                    </div>
                  ))}
                </div>

                {/* Ingestion confirmation */}
                {ingestion && (
                  <div className={`rounded-md border p-3 flex items-start gap-3 ${
                    ingestion.worldPersisted
                      ? 'bg-emerald-500/10 border-emerald-500/30'
                      : ingestion.scyllaAvailable
                        ? 'bg-amber-500/10 border-amber-500/30'
                        : 'bg-destructive/10 border-destructive/30'
                  }`}>
                    {ingestion.worldPersisted
                      ? <Database className="h-4 w-4 mt-0.5 shrink-0 text-emerald-400" />
                      : <XCircle className="h-4 w-4 mt-0.5 shrink-0 text-destructive" />}
                    <div className="text-xs">
                      {ingestion.worldPersisted ? (
                        <p className="font-medium text-emerald-400">
                          Ingested to ScyllaDB ✓ — session {ingestion.sessionId}
                        </p>
                      ) : ingestion.scyllaAvailable ? (
                        <p className="font-medium text-amber-400">
                          Not visible in ScyllaDB yet — still queued or pending. Check back shortly.
                        </p>
                      ) : (
                        <p className="font-medium text-destructive">
                          ScyllaDB unavailable — Scylla is not connected/schema not ready. World was saved to the artifact only.
                        </p>
                      )}
                      <p className="text-muted-foreground mt-1 font-mono">
                        exec {ingestion.executionId} · rooms {ingestion.rooms ?? '—'} · enemies {ingestion.enemies ?? '—'} · loot {ingestion.loot ?? '—'}
                      </p>
                    </div>
                  </div>
                )}

                <Separator />

                {ingestResult && (
                  <div className="rounded-md border border-emerald-500/30 bg-emerald-500/10 p-3 flex items-start gap-3">
                    <Database className="h-4 w-4 mt-0.5 shrink-0 text-emerald-400" />
                    <div className="text-xs">
                      <p className="font-medium text-emerald-400">Ingested to authoritative world ✓</p>
                      <p className="text-muted-foreground mt-1 font-mono">
                        session {ingestResult.sessionId} · exec {ingestResult.executionId} · rooms {ingestResult.rooms} · enemies {ingestResult.enemies} · loot {ingestResult.loot}
                      </p>
                    </div>
                  </div>
                )}

                <div className="flex items-center justify-between">
                  <button
                    onClick={() => setJsonOpen(v => !v)}
                    className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                  >
                    {jsonOpen ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
                    Raw JSON
                  </button>
                  <div className="flex gap-2">
                    <Button size="sm" onClick={ingestToAuthority} disabled={ingestBusy}>
                      {ingestBusy ? <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" /> : <Send className="h-3.5 w-3.5 mr-1.5" />}
                      Ingest to Authority
                    </Button>
                    <Button size="sm" variant="outline" onClick={downloadJson}>
                      <Download className="h-3.5 w-3.5 mr-1.5" /> Export
                    </Button>
                  </div>
                </div>
                {jsonOpen && (
                  <ScrollArea className="h-64 rounded-md border border-border">
                    <pre className="p-3 text-xs font-mono text-muted-foreground whitespace-pre-wrap">
                      {JSON.stringify(result.raw, null, 2)}
                    </pre>
                  </ScrollArea>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}
