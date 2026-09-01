import { useState } from 'react'
import { Swords, Globe, Users, Mountain, Activity, Map, Radio, Layers, Terminal, Gauge } from 'lucide-react'
import { cn } from '@/lib/utils'
import WorldGenerator from '@/pages/WorldGenerator'
import CharacterGenerator from '@/pages/CharacterGenerator'
import HeightmapGenerator from '@/pages/HeightmapGenerator'
import Observability from '@/pages/Observability'
import WorldExplorer from '@/pages/WorldExplorer'
import LiveWorldMonitor from '@/pages/LiveWorldMonitor'
import LoggingConsole from '@/pages/LoggingConsole'
import PoolRuntime from '@/pages/PoolRuntime'

type Page = 'world' | 'characters' | 'heightmap' | 'observability'
  | 'explorer' | 'live' | 'logging' | 'pool'

interface NavSection {
  label: string
  items: { id: Page; label: string; icon: React.ElementType }[]
}

const NAV: NavSection[] = [
  {
    label: 'Generate',
    items: [
      { id: 'world',        label: 'World Generator',  icon: Globe },
      { id: 'characters',   label: 'Characters',       icon: Users },
      { id: 'heightmap',    label: 'Terrain Mesh',     icon: Mountain },
    ],
  },
  {
    label: 'World',
    items: [
      { id: 'explorer',     label: 'World Explorer',   icon: Map },
      { id: 'live',         label: 'Live Monitor',     icon: Radio },
      { id: 'logging',      label: 'Logging Console',  icon: Terminal },
    ],
  },
  {
    label: 'Oversight',
    items: [
      { id: 'observability', label: 'Observability',   icon: Activity },
      { id: 'pool',         label: 'Pool & Runtime',  icon: Gauge },
    ],
  },
]

export default function App() {
  const [page, setPage] = useState<Page>('world')

  return (
    <div className="flex h-screen bg-background text-foreground overflow-hidden">
      {/* Sidebar */}
      <aside className="w-56 shrink-0 flex flex-col border-r border-border bg-card">
        <div className="p-4 border-b border-border">
          <div className="flex items-center gap-2">
            <Swords className="h-5 w-5 text-primary" />
            <span className="font-semibold text-sm tracking-wide">TOR Admin</span>
          </div>
          <p className="text-xs text-muted-foreground mt-0.5">MMO Engine Console</p>
        </div>

        <nav className="flex-1 p-2 space-y-3 overflow-auto">
          {NAV.map((section) => (
            <div key={section.label}>
              <p className="px-3 pt-2 pb-1 text-[11px] uppercase tracking-wider text-muted-foreground">
                {section.label}
              </p>
              <div className="space-y-0.5">
                {section.items.map(({ id, label, icon: Icon }) => (
                  <button
                    key={id}
                    onClick={() => setPage(id)}
                    className={cn(
                      'w-full flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors text-left',
                      page === id
                        ? 'bg-primary text-primary-foreground font-medium'
                        : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                    )}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    {label}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </nav>

        <div className="p-3 border-t border-border space-y-0.5">
          <p className="text-xs text-muted-foreground">Backend: <span className="text-foreground/70">:8081</span></p>
          <p className="text-xs text-muted-foreground">Generator: <span className="text-foreground/70">:8090</span></p>
        </div>
      </aside>

      {/* Content */}
      <main className="flex-1 overflow-auto">
        {page === 'world'          && <WorldGenerator />}
        {page === 'characters'     && <CharacterGenerator />}
        {page === 'heightmap'      && <HeightmapGenerator />}
        {page === 'observability'  && <Observability />}
        {page === 'explorer'       && <WorldExplorer />}
        {page === 'live'           && <LiveWorldMonitor />}
        {page === 'logging'        && <LoggingConsole />}
        {page === 'pool'           && <PoolRuntime />}
      </main>
    </div>
  )
}
