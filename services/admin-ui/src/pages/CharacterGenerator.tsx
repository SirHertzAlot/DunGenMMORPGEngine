import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Slider } from '@/components/ui/slider'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { generateCharactersViaJob, type GeneratedCharacter } from '@/lib/api'
import { Loader2, Download, Shield, Sword, Sparkles, ChevronDown, ChevronUp } from 'lucide-react'

const CLASSES = ['Any', 'Warrior', 'Mage', 'Rogue', 'Priest', 'Ranger', 'Paladin', 'Warlock', 'Druid']
const RACES   = ['Any', 'Human', 'Elf', 'Dwarf', 'Orc', 'Halfling', 'Gnome', 'Tiefling', 'Dragonborn']

const CLASS_COLORS: Record<string, string> = {
  Warrior: 'text-orange-400', Mage: 'text-violet-400', Rogue: 'text-yellow-400',
  Priest:  'text-yellow-100', Ranger: 'text-green-400', Paladin: 'text-amber-300',
  Warlock: 'text-purple-400', Druid: 'text-emerald-400',
}

const TIER_COLORS: Record<string, string> = {
  common: 'secondary', uncommon: 'default', rare: 'default', epic: 'default', legendary: 'default',
}
const TIER_TEXT: Record<string, string> = {
  common: 'text-slate-400', uncommon: 'text-green-400', rare: 'text-blue-400',
  epic: 'text-purple-400', legendary: 'text-orange-400',
}

function StatBar({ label, value }: { label: string; value: number }) {
  const pct = Math.min(100, Math.round((value / 30) * 100))
  return (
    <div className="flex items-center gap-2">
      <span className="text-xs text-muted-foreground w-7">{label}</span>
      <div className="flex-1 h-1.5 rounded-full bg-secondary overflow-hidden">
        <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs font-mono w-5 text-right">{value}</span>
    </div>
  )
}

function CharacterCard({ char }: { char: GeneratedCharacter }) {
  const [jsonOpen, setJsonOpen] = useState(false)

  const downloadJson = () => {
    const blob = new Blob([JSON.stringify(char, null, 2)], { type: 'application/json' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `character-${char.name.replace(/\s+/g, '-')}.json`
    a.click()
  }

  const eq = char.equipment
  const eqSlots = [
    { label: 'Main Hand', slot: eq.mainHand },
    { label: 'Off Hand',  slot: eq.offHand  },
    { label: 'Armor',     slot: eq.armor    },
    { label: 'Accessory', slot: eq.accessory },
  ].filter(s => s.slot)

  return (
    <Card className="bg-card/50">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-2">
          <div>
            <CardTitle className="text-base">{char.name}</CardTitle>
            <CardDescription className="text-xs mt-0.5">
              <span className={CLASS_COLORS[char.class] ?? 'text-muted-foreground'}>{char.class}</span>
              {' · '}{char.race}{' · '}Lv {char.level}
            </CardDescription>
          </div>
          <div className="flex items-center gap-1.5 shrink-0 text-xs text-muted-foreground">
            <Shield className="h-3.5 w-3.5" />
            <span>{char.armorClass}</span>
            <span className="mx-0.5 text-border">|</span>
            <span className="text-red-400">{char.hitPoints} HP</span>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4 pt-0">
        {/* Stats */}
        <div className="space-y-1">
          <StatBar label="STR" value={char.stats.strength} />
          <StatBar label="DEX" value={char.stats.dexterity} />
          <StatBar label="INT" value={char.stats.intelligence} />
          <StatBar label="CON" value={char.stats.constitution} />
          <StatBar label="WIS" value={char.stats.wisdom} />
          <StatBar label="CHA" value={char.stats.charisma} />
        </div>

        <Separator />

        {/* Skills */}
        {char.skills.length > 0 && (
          <div>
            <p className="text-xs text-muted-foreground mb-1.5 flex items-center gap-1">
              <Sparkles className="h-3 w-3" /> Skills
            </p>
            <div className="flex flex-wrap gap-1">
              {char.skills.map(s => (
                <Badge key={s} variant="secondary" className="text-xs">{s}</Badge>
              ))}
            </div>
          </div>
        )}

        {/* Equipment */}
        {eqSlots.length > 0 && (
          <div>
            <p className="text-xs text-muted-foreground mb-1.5 flex items-center gap-1">
              <Sword className="h-3 w-3" /> Equipment
            </p>
            <div className="space-y-1">
              {eqSlots.map(({ label, slot }) => slot && (
                <div key={label} className="flex items-center justify-between text-xs">
                  <span className="text-muted-foreground">{label}</span>
                  <span className={TIER_TEXT[slot.tier] ?? 'text-foreground'}>{slot.name}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        <Separator />

        <div className="flex items-center justify-between">
          <div className="text-xs text-muted-foreground space-x-3">
            <span>{char.alignment}</span>
            <span>{char.background}</span>
            <span className="text-yellow-400">{char.gold}g</span>
          </div>
          <div className="flex gap-1.5">
            <button
              onClick={() => setJsonOpen(v => !v)}
              className="text-xs text-muted-foreground hover:text-foreground transition-colors flex items-center gap-1"
            >
              {jsonOpen ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />} JSON
            </button>
            <button
              onClick={downloadJson}
              className="text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              <Download className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
        {jsonOpen && (
          <ScrollArea className="h-48 rounded-md border border-border">
            <pre className="p-2 text-xs font-mono text-muted-foreground whitespace-pre-wrap">
              {JSON.stringify(char, null, 2)}
            </pre>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  )
}

export default function CharacterGeneratorPage() {
  const [level, setLevel]       = useState(5)
  const [charClass, setClass]   = useState('Any')
  const [race, setRace]         = useState('Any')
  const [count, setCount]       = useState(3)
  const [seed, setSeed]         = useState('')
  const [loading, setLoading]   = useState(false)
  const [error, setError]       = useState<string | null>(null)
  const [chars, setChars]       = useState<GeneratedCharacter[]>([])

  const generate = async () => {
    setLoading(true); setError(null)
    try {
      const result = await generateCharactersViaJob({
        level,
        class:  charClass === 'Any' ? undefined : charClass,
        race:   race     === 'Any' ? undefined : race,
        count,
        seed:   seed.trim() ? parseInt(seed.trim(), 10) : undefined,
      })
      setChars(result)
    } catch (e) {
      setError(String(e))
    } finally {
      setLoading(false)
    }
  }

  const exportAll = () => {
    const blob = new Blob([JSON.stringify(chars, null, 2)], { type: 'application/json' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `characters-${Date.now()}.json`
    a.click()
  }

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Character Generator</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Generate procedural character JSON dumps for game content testing.
        </p>
      </div>

      <div className="flex flex-col lg:flex-row gap-6">
        {/* Controls */}
        <div className="w-full lg:w-72 shrink-0">
          <Card>
            <CardHeader><CardTitle className="text-base">Parameters</CardTitle></CardHeader>
            <CardContent className="space-y-5">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label>Level</Label>
                  <span className="text-sm font-mono tabular-nums">{level}</span>
                </div>
                <Slider min={1} max={60} step={1} value={[level]} onValueChange={([v]) => setLevel(v)} />
                <div className="flex justify-between text-xs text-muted-foreground"><span>1</span><span>60</span></div>
              </div>

              <div className="space-y-1.5">
                <Label>Class</Label>
                <Select value={charClass} onValueChange={setClass}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {CLASSES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label>Race</Label>
                <Select value={race} onValueChange={setRace}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {RACES.map(r => <SelectItem key={r} value={r}>{r}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label>Count (1–20)</Label>
                <Input type="number" min={1} max={20} value={count} onChange={e => setCount(Number(e.target.value))} />
              </div>

              <div className="space-y-1.5">
                <Label>Seed (optional)</Label>
                <Input placeholder="Random" value={seed} onChange={e => setSeed(e.target.value)} />
              </div>

              <Button onClick={generate} disabled={loading} className="w-full">
                {loading ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Generating…</> : 'Generate'}
              </Button>
              {chars.length > 0 && (
                <Button variant="outline" onClick={exportAll} className="w-full">
                  <Download className="mr-2 h-4 w-4" /> Export All JSON
                </Button>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Results */}
        <div className="flex-1 min-w-0">
          {error && (
            <div className="rounded-md bg-destructive/10 border border-destructive/30 p-3 text-sm text-destructive-foreground mb-4">
              {error}
            </div>
          )}
          {chars.length === 0 && !loading && !error && (
            <div className="flex items-center justify-center h-64 rounded-lg border border-dashed border-border text-muted-foreground text-sm">
              Configure parameters and click Generate.
            </div>
          )}
          {loading && (
            <div className="flex items-center justify-center h-64">
              <Loader2 className="h-8 w-8 animate-spin text-primary" />
            </div>
          )}
          {chars.length > 0 && (
            <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
              {chars.map(c => <CharacterCard key={c.characterId} char={c} />)}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
