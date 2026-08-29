import { useState, useRef, useEffect } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Slider } from '@/components/ui/slider'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { generateTerrainMeshViaJob, type GeneratedTerrainMesh } from '@/lib/api'
import { Loader2, Download, ChevronDown, ChevronUp, Droplets, Trees, Mountain } from 'lucide-react'

function drawTerrainPreview(canvas: HTMLCanvasElement, mesh: GeneratedTerrainMesh) {
  const { width, height, vertices, waterLevel, heightScale } = mesh
  canvas.width  = width
  canvas.height = height
  const ctx = canvas.getContext('2d')!
  const img = ctx.createImageData(width, height)
  const waterThreshold = waterLevel * heightScale
  const foothillThreshold = 0.6 * heightScale
  const peakThreshold = 0.8 * heightScale

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const v   = vertices[y * width + x].y
      const idx = (y * width + x) * 4
      let r: number, g: number, b: number

      if (v < waterThreshold) {
        // Deep → shallow water (dark blue → light blue)
        const t = v / Math.max(waterThreshold, 0.001)
        r = Math.floor(10  + t * 50)
        g = Math.floor(50  + t * 90)
        b = Math.floor(160 + t * 60)
      } else if (v < foothillThreshold) {
        // Land: dark grass → light grass
        const t = (v - waterThreshold) / Math.max(foothillThreshold - waterThreshold, 0.001)
        r = Math.floor(30  + t * 60)
        g = Math.floor(110 + t * 70)
        b = Math.floor(30  + t * 20)
      } else if (v < peakThreshold) {
        // Foothills: tan → brown
        const t = (v - foothillThreshold) / Math.max(peakThreshold - foothillThreshold, 0.001)
        r = Math.floor(130 + t * 60)
        g = Math.floor(105 + t * 20)
        b = Math.floor(75  - t * 30)
      } else {
        // Peaks: light gray → white
        const t = (v - peakThreshold) / Math.max((mesh.maxHeight - peakThreshold), 0.001)
        const c = Math.floor(190 + t * 65)
        r = g = b = c
      }

      img.data[idx]     = r
      img.data[idx + 1] = g
      img.data[idx + 2] = b
      img.data[idx + 3] = 255
    }
  }
  ctx.putImageData(img, 0, 0)
}

function buildObj(mesh: GeneratedTerrainMesh): string {
  const lines: string[] = ['# TOR terrain mesh export']

  for (const vertex of mesh.vertices)
    lines.push(`v ${vertex.x} ${vertex.y} ${vertex.z}`)

  for (const vertex of mesh.vertices)
    lines.push(`vt ${vertex.u} ${1 - vertex.v}`)

  for (const vertex of mesh.vertices)
    lines.push(`vn ${vertex.normalX} ${vertex.normalY} ${vertex.normalZ}`)

  for (let i = 0; i < mesh.triangles.length; i += 3) {
    const a = mesh.triangles[i] + 1
    const b = mesh.triangles[i + 1] + 1
    const c = mesh.triangles[i + 2] + 1
    lines.push(`f ${a}/${a}/${a} ${b}/${b}/${b} ${c}/${c}/${c}`)
  }

  return `${lines.join('\n')}\n`
}

export default function HeightmapGeneratorPage() {
  const [form, setForm] = useState({
    width: 64, height: 64, seed: '', waterLevel: 0.35,
    algorithm: 'diamond-square', roughness: 0.55, octaves: 4,
  })
  const [loading, setLoading]   = useState(false)
  const [error, setError]       = useState<string | null>(null)
  const [mesh, setMesh]         = useState<GeneratedTerrainMesh | null>(null)
  const [jsonOpen, setJsonOpen] = useState(false)
  const canvasRef               = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    if (mesh && canvasRef.current) drawTerrainPreview(canvasRef.current, mesh)
  }, [mesh])

  const set = (k: keyof typeof form, v: string | number) =>
    setForm(f => ({ ...f, [k]: v }))

  const generate = async () => {
    setLoading(true); setError(null)
    try {
      const result = await generateTerrainMeshViaJob({
        width:      form.width,
        height:     form.height,
        waterLevel: form.waterLevel,
        algorithm:  form.algorithm,
        roughness:  form.roughness,
        octaves:    form.octaves,
        seed:       form.seed.trim() ? parseInt(form.seed.trim(), 10) : undefined,
      })
      setMesh(result)
    } catch (e) {
      setError(String(e))
    } finally {
      setLoading(false)
    }
  }

  const exportObj = () => {
    if (!mesh) return
    const blob = new Blob([buildObj(mesh)], { type: 'text/plain' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `terrain-mesh-${mesh.seed}.obj`
    a.click()
  }

  const exportJson = () => {
    const blob = new Blob([JSON.stringify(mesh, null, 2)], { type: 'application/json' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `terrain-mesh-${mesh?.seed ?? Date.now()}.json`
    a.click()
  }

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-semibold">Terrain Mesh Generator</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Generate a final terrain mesh artifact with vertex, triangle, UV, and normal data.
        </p>
      </div>

      <div className="flex flex-col lg:flex-row gap-6">
        {/* Controls */}
        <div className="w-full lg:w-72 shrink-0">
          <Card>
            <CardHeader><CardTitle className="text-base">Parameters</CardTitle></CardHeader>
            <CardContent className="space-y-5">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Width</Label>
                  <Input type="number" min={8} max={512} value={form.width} onChange={e => set('width', Number(e.target.value))} />
                </div>
                <div className="space-y-1.5">
                  <Label>Height</Label>
                  <Input type="number" min={8} max={512} value={form.height} onChange={e => set('height', Number(e.target.value))} />
                </div>
              </div>

              <div className="space-y-1.5">
                <Label>Algorithm</Label>
                <Select value={form.algorithm} onValueChange={v => set('algorithm', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="diamond-square">Diamond-Square</SelectItem>
                    <SelectItem value="perlin">Perlin (Value Noise)</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <div className="flex justify-between">
                  <Label>Water Level</Label>
                  <span className="text-sm font-mono">{form.waterLevel.toFixed(2)}</span>
                </div>
                <Slider min={0} max={0.9} step={0.01} value={[form.waterLevel]}
                  onValueChange={([v]) => set('waterLevel', v)} />
              </div>

              <div className="space-y-2">
                <div className="flex justify-between">
                  <Label>Roughness</Label>
                  <span className="text-sm font-mono">{form.roughness.toFixed(2)}</span>
                </div>
                <Slider min={0.1} max={1} step={0.05} value={[form.roughness]}
                  onValueChange={([v]) => set('roughness', v)} />
              </div>

              {form.algorithm === 'perlin' && (
                <div className="space-y-2">
                  <div className="flex justify-between">
                    <Label>Octaves</Label>
                    <span className="text-sm font-mono">{form.octaves}</span>
                  </div>
                  <Slider min={1} max={8} step={1} value={[form.octaves]}
                    onValueChange={([v]) => set('octaves', v)} />
                </div>
              )}

              <div className="space-y-1.5">
                <Label>Seed (optional)</Label>
                <Input placeholder="Random" value={form.seed} onChange={e => set('seed', e.target.value)} />
              </div>

              <Button onClick={generate} disabled={loading} className="w-full">
                {loading ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Generating…</> : 'Generate'}
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* Canvas + Stats */}
        <div className="flex-1 space-y-4">
          {error && (
            <div className="rounded-md bg-destructive/10 border border-destructive/30 p-3 text-sm text-destructive-foreground">
              {error}
            </div>
          )}

          <Card>
            <CardContent className="p-4">
              {loading && (
                <div className="flex items-center justify-center h-64">
                  <Loader2 className="h-8 w-8 animate-spin text-primary" />
                </div>
              )}
              {!mesh && !loading && (
                <div className="flex items-center justify-center h-64 rounded-md border border-dashed border-border text-muted-foreground text-sm">
                  Terrain preview will appear here.
                </div>
              )}
              {mesh && !loading && (
                <div className="space-y-3">
                  {/* Canvas */}
                  <div className="rounded-md overflow-hidden border border-border bg-black flex justify-center">
                    <canvas
                      ref={canvasRef}
                      style={{
                        imageRendering: 'pixelated',
                        width: '100%',
                        maxWidth: '512px',
                        aspectRatio: `${mesh.width} / ${mesh.height}`,
                        display: 'block',
                      }}
                    />
                  </div>

                  {/* Biome Stats */}
                  <div className="grid grid-cols-3 gap-2">
                    <div className="rounded-md bg-secondary p-3 text-center">
                      <Droplets className="h-4 w-4 mx-auto mb-1 text-blue-400" />
                      <div className="text-lg font-bold">{mesh.biomes.waterPercent}%</div>
                      <div className="text-xs text-muted-foreground">Water</div>
                      <div className="text-xs text-muted-foreground">{mesh.biomes.waterTiles} tiles</div>
                    </div>
                    <div className="rounded-md bg-secondary p-3 text-center">
                      <Trees className="h-4 w-4 mx-auto mb-1 text-green-400" />
                      <div className="text-lg font-bold">{mesh.biomes.landPercent}%</div>
                      <div className="text-xs text-muted-foreground">Land</div>
                      <div className="text-xs text-muted-foreground">{mesh.biomes.landTiles} tiles</div>
                    </div>
                    <div className="rounded-md bg-secondary p-3 text-center">
                      <Mountain className="h-4 w-4 mx-auto mb-1 text-slate-300" />
                      <div className="text-lg font-bold">{mesh.biomes.mountainPercent}%</div>
                      <div className="text-xs text-muted-foreground">Mountain</div>
                      <div className="text-xs text-muted-foreground">{mesh.biomes.mountainTiles} tiles</div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 text-xs text-muted-foreground">
                    <span>{mesh.width}×{mesh.height}</span>
                    <span>·</span>
                    <span className="font-mono">seed: {mesh.seed}</span>
                    <span>·</span>
                    <span>{mesh.algorithm}</span>
                    <span>·</span>
                    <span>{mesh.vertices.length} verts</span>
                    <span>·</span>
                    <span>{mesh.triangles.length / 3} tris</span>
                  </div>

                  <Separator />

                  <div className="flex items-center justify-between">
                    <button
                      onClick={() => setJsonOpen(v => !v)}
                      className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                    >
                      {jsonOpen ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
                      Raw JSON
                    </button>
                    <div className="flex gap-2">
                      <Button size="sm" variant="outline" onClick={exportObj}>
                        <Download className="h-3.5 w-3.5 mr-1.5" /> OBJ
                      </Button>
                      <Button size="sm" variant="outline" onClick={exportJson}>
                        <Download className="h-3.5 w-3.5 mr-1.5" /> JSON
                      </Button>
                    </div>
                  </div>

                  {jsonOpen && (
                    <ScrollArea className="h-48 rounded-md border border-border">
                      <pre className="p-3 text-xs font-mono text-muted-foreground">
                        {JSON.stringify({ ...mesh, vertices: `[${mesh.vertices.length} vertices — omitted]`, triangles: `[${mesh.triangles.length} indices — omitted]` }, null, 2)}
                      </pre>
                    </ScrollArea>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Color Legend */}
          <Card>
            <CardContent className="p-4">
              <p className="text-xs text-muted-foreground mb-2 font-medium">Color Legend</p>
              <div className="flex gap-4 text-xs">
                {[
                  { color: 'bg-blue-600',   label: 'Water'    },
                  { color: 'bg-green-600',  label: 'Land'     },
                  { color: 'bg-amber-700',  label: 'Foothills'},
                  { color: 'bg-slate-300',  label: 'Peaks'    },
                ].map(({ color, label }) => (
                  <div key={label} className="flex items-center gap-1.5">
                    <div className={`h-3 w-3 rounded-sm ${color}`} />
                    <span className="text-muted-foreground">{label}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
