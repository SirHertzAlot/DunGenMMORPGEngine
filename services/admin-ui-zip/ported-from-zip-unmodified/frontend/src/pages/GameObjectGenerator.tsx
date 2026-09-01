import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Swords, 
  Play, 
  Square, 
  RotateCcw, 
  Copy, 
  Check, 
  AlertCircle, 
  Loader2,
  Users,
  Package,
  Castle,
  ScrollText,
  Skull,
  Info,
  Upload,
  FileText
} from 'lucide-react';
import { debugLogger } from '../lib/debugLogger';
import { 
  GraphTraversalEngine, 
  GraphDefinition, 
  TraversalResult,
  validateGraph,
} from '../lib/graphTraversal';
import { 
  WFCDungeonGenerator, 
  parseTilesetYAML, 
  type TilesetConfig, 
  type DungeonLayout 
} from '../lib/wfcDungeon';

type GenerationType = 'npc' | 'item' | 'dungeon' | 'quest' | 'boss';

const generationTypeConfig = {
  npc: {
    label: 'Generate NPC',
    icon: Users,
    description: 'Create non-player characters with AI behaviors and stats',
    color: 'text-blue-500',
  },
  item: {
    label: 'Generate Item',
    icon: Package,
    description: 'Create weapons, armor, consumables, and other game items',
    color: 'text-green-500',
  },
  dungeon: {
    label: 'Generate Dungeon',
    icon: Castle,
    description: 'Create dungeon layouts with rooms, connections, and encounters using WFC',
    color: 'text-purple-500',
  },
  quest: {
    label: 'Generate Quest',
    icon: ScrollText,
    description: 'Create quest objectives, rewards, and trigger conditions',
    color: 'text-amber-500',
  },
  boss: {
    label: 'Generate Boss Battle',
    icon: Skull,
    description: 'Create boss encounters with special abilities and mechanics',
    color: 'text-red-500',
  },
};

// Graph definitions for each generation type (excluding dungeon)
const graphDefinitions: Record<Exclude<GenerationType, 'dungeon'>, GraphDefinition> = {
  npc: {
    entry: 'start',
    nodes: {
      start: { id: 'start', type: 'sequence', next: 'choose_class' },
      choose_class: { id: 'choose_class', type: 'choice', weight: 1.0, next: ['warrior', 'mage', 'rogue', 'merchant'] },
      warrior: {
        id: 'warrior',
        type: 'emit',
        weight: 0.3,
        emit: {
          Health: { current: 100, max: 100, regeneration: 1 },
          Attack: { damage: 15, range: 3, speed: 1.0, critChance: 0.1 },
          Class: { name: 'Warrior', archetype: 'melee' },
        },
        next: 'add_ai',
      },
      mage: {
        id: 'mage',
        type: 'emit',
        weight: 0.25,
        emit: {
          Health: { current: 60, max: 60, regeneration: 0 },
          Attack: { damage: 25, range: 15, speed: 0.8, critChance: 0.15 },
          Class: { name: 'Mage', archetype: 'ranged' },
        },
        next: 'add_ai',
      },
      rogue: {
        id: 'rogue',
        type: 'emit',
        weight: 0.25,
        emit: {
          Health: { current: 80, max: 80, regeneration: 2 },
          Attack: { damage: 20, range: 3, speed: 1.5, critChance: 0.25 },
          Class: { name: 'Rogue', archetype: 'stealth' },
        },
        next: 'add_ai',
      },
      merchant: {
        id: 'merchant',
        type: 'emit',
        weight: 0.2,
        emit: {
          Health: { current: 50, max: 50, regeneration: 0 },
          Attack: { damage: 5, range: 1, speed: 0.5, critChance: 0 },
          Class: { name: 'Merchant', archetype: 'passive' },
        },
        next: 'add_ai',
      },
      add_ai: {
        id: 'add_ai',
        type: 'emit',
        emit: {
          AIBehavior: { type: 'patrol', aggroRange: 10, patrolRadius: 15, tactics: ['engage', 'retreat'] },
        },
        next: 'add_loot',
      },
      add_loot: {
        id: 'add_loot',
        type: 'emit',
        emit: {
          LootTable: {
            items: [
              { id: 'gold', rarity: 'common', dropChance: 0.8 },
              { id: 'potion', rarity: 'common', dropChance: 0.5 },
              { id: 'equipment', rarity: 'uncommon', dropChance: 0.2 },
            ],
          },
        },
      },
    },
  },
  item: {
    entry: 'start',
    nodes: {
      start: { id: 'start', type: 'sequence', next: 'choose_type' },
      choose_type: { id: 'choose_type', type: 'choice', weight: 1.0, next: ['weapon', 'armor', 'potion', 'artifact'] },
      weapon: {
        id: 'weapon',
        type: 'emit',
        weight: 0.35,
        emit: {
          ItemData: { name: 'Sword', type: 'Weapon', rarity: 'common', stackSize: 1 },
          Attack: { damage: 10, range: 2, speed: 1.0 },
          Durability: { current: 100, max: 100 },
        },
      },
      armor: {
        id: 'armor',
        type: 'emit',
        weight: 0.3,
        emit: {
          ItemData: { name: 'Chainmail', type: 'Armor', rarity: 'common', stackSize: 1 },
          Defense: { value: 8 },
          Durability: { current: 150, max: 150 },
        },
      },
      potion: {
        id: 'potion',
        type: 'emit',
        weight: 0.2,
        emit: {
          ItemData: { name: 'Health Potion', type: 'Potion', rarity: 'common', stackSize: 10 },
          Consumable: { effect: 'heal', value: 50 },
        },
      },
      artifact: {
        id: 'artifact',
        type: 'emit',
        weight: 0.15,
        emit: {
          ItemData: { name: 'Ancient Relic', type: 'Artifact', rarity: 'legendary', stackSize: 1 },
          MagicPower: { value: 20, school: 'arcane' },
        },
      },
    },
  },
  quest: {
    entry: 'start',
    nodes: {
      start: { id: 'start', type: 'sequence', next: 'choose_quest' },
      choose_quest: { id: 'choose_quest', type: 'choice', weight: 1.0, next: ['fetch', 'kill', 'escort'] },
      fetch: {
        id: 'fetch',
        type: 'emit',
        weight: 0.4,
        emit: {
          QuestData: { type: 'FetchQuest', difficulty: 'easy', timeLimit: 600, repeatable: true },
          QuestTriggers: {
            objectives: ['Collect 5 items'],
            rewards: { experience: 100, gold: 50, items: ['common_item'] },
            prerequisites: [],
          },
        },
      },
      kill: {
        id: 'kill',
        type: 'emit',
        weight: 0.35,
        emit: {
          QuestData: { type: 'KillQuest', difficulty: 'medium', timeLimit: null, repeatable: true },
          QuestTriggers: {
            objectives: ['Defeat 10 enemies'],
            rewards: { experience: 200, gold: 100, items: ['uncommon_item'] },
            prerequisites: [],
          },
        },
      },
      escort: {
        id: 'escort',
        type: 'emit',
        weight: 0.25,
        emit: {
          QuestData: { type: 'EscortQuest', difficulty: 'hard', timeLimit: 900, repeatable: false },
          QuestTriggers: {
            objectives: ['Escort NPC 500m'],
            rewards: { experience: 500, gold: 200, items: ['rare_item'] },
            prerequisites: ['previous_quest_complete'],
          },
        },
      },
    },
  },
  boss: {
    entry: 'start',
    nodes: {
      start: { id: 'start', type: 'sequence', next: 'choose_boss' },
      choose_boss: { id: 'choose_boss', type: 'choice', weight: 1.0, next: ['dragon', 'demon', 'lich'] },
      dragon: {
        id: 'dragon',
        type: 'emit',
        weight: 0.35,
        emit: {
          Health: { current: 5000, max: 5000, regeneration: 25 },
          Attack: { damage: 250, range: 20, speed: 0.8, critChance: 0.15 },
          BossAbilities: {
            phases: 3,
            specialAbilities: ['fire_breath', 'wing_buffet', 'tail_swipe'],
            enrageThreshold: 0.3,
          },
        },
        next: 'add_loot',
      },
      demon: {
        id: 'demon',
        type: 'emit',
        weight: 0.35,
        emit: {
          Health: { current: 4000, max: 4000, regeneration: 20 },
          Attack: { damage: 200, range: 5, speed: 1.0, critChance: 0.2 },
          BossAbilities: {
            phases: 2,
            specialAbilities: ['hellfire', 'summon_minions', 'dark_pact'],
            enrageThreshold: 0.25,
          },
        },
        next: 'add_loot',
      },
      lich: {
        id: 'lich',
        type: 'emit',
        weight: 0.3,
        emit: {
          Health: { current: 3500, max: 3500, regeneration: 15 },
          Attack: { damage: 175, range: 15, speed: 0.7, critChance: 0.25 },
          BossAbilities: {
            phases: 4,
            specialAbilities: ['summon_undead', 'life_drain', 'death_bolt', 'phylactery'],
            enrageThreshold: 0.2,
          },
        },
        next: 'add_loot',
      },
      add_loot: {
        id: 'add_loot',
        type: 'emit',
        emit: {
          LootTable: {
            items: [
              { id: 'legendary_weapon', rarity: 'legendary', dropChance: 0.1 },
              { id: 'epic_armor', rarity: 'epic', dropChance: 0.3 },
              { id: 'rare_material', rarity: 'rare', dropChance: 0.6 },
              { id: 'gold_pile', rarity: 'common', dropChance: 1.0 },
            ],
          },
        },
      },
    },
  },
};

export default function GameObjectGenerator() {
  const [generationType, setGenerationType] = useState<GenerationType>('npc');
  const [seed, setSeed] = useState<number>(Date.now());
  const [randomnessFactor, setRandomnessFactor] = useState<number>(0.5);
  const [isGenerating, setIsGenerating] = useState(false);
  const [traversalResult, setTraversalResult] = useState<TraversalResult | null>(null);
  const [dungeonLayout, setDungeonLayout] = useState<DungeonLayout | null>(null);
  const [tilesetConfig, setTilesetConfig] = useState<TilesetConfig | null>(null);
  const [tilesetYAML, setTilesetYAML] = useState<string>('');
  const [copiedSection, setCopiedSection] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [generationPhase, setGenerationPhase] = useState<string>('');

  const handleTilesetUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    debugLogger.info('GameObjectGenerator', `Loading tileset YAML: ${file.name}`);
    setError(null);

    try {
      const text = await file.text();
      setTilesetYAML(text);
      
      const config = parseTilesetYAML(text);
      setTilesetConfig(config);
      
      debugLogger.success('GameObjectGenerator', `Tileset loaded: ${Object.keys(config.tiles).length} tiles, ${Object.keys(config.adjacency).length} adjacency rules`);
    } catch (err: any) {
      const errorMsg = `Failed to parse tileset YAML: ${err.message}`;
      setError(errorMsg);
      debugLogger.error('GameObjectGenerator', errorMsg);
    }
  };

  const handleStartGeneration = async () => {
    setIsGenerating(true);
    setError(null);
    setGenerationPhase('Initializing...');
    
    debugLogger.info('GameObjectGenerator', `Starting ${generationType} generation with seed ${seed}`, { generationType, seed, randomnessFactor }, 'offline');

    try {
      if (generationType === 'dungeon') {
        // WFC-based dungeon generation
        if (!tilesetConfig) {
          throw new Error('Please upload a tileset YAML configuration first');
        }

        setGenerationPhase('Parsing tileset configuration...');
        await new Promise(resolve => setTimeout(resolve, 300));

        debugLogger.info('GameObjectGenerator', 'Starting WFC dungeon generation');
        setGenerationPhase('Initializing WFC algorithm...');
        await new Promise(resolve => setTimeout(resolve, 300));

        const generator = new WFCDungeonGenerator(tilesetConfig, seed);
        
        setGenerationPhase('Running Wave Function Collapse...');
        await new Promise(resolve => setTimeout(resolve, 500));
        
        const layout = generator.generate();
        setDungeonLayout(layout);
        setTraversalResult(null);

        debugLogger.success('GameObjectGenerator', `Dungeon generated: ${layout.width}x${layout.height}, ${layout.metadata.collapsedCells} cells collapsed in ${layout.metadata.iterations} iterations`);
        setGenerationPhase('Complete!');
      } else {
        // Graph-based generation for other types
        const graph = graphDefinitions[generationType];
        
        setGenerationPhase('Validating graph structure...');
        const validation = validateGraph(graph);
        if (!validation.valid) {
          throw new Error(`Invalid graph: ${validation.errors.join(', ')}`);
        }

        debugLogger.success('GameObjectGenerator', 'Graph validation passed', { nodeCount: Object.keys(graph.nodes).length }, 'offline');

        setGenerationPhase('Executing graph traversal...');
        await new Promise(resolve => setTimeout(resolve, 500));

        const engine = new GraphTraversalEngine(graph, seed, randomnessFactor);
        const result = engine.traverse();

        debugLogger.success('GameObjectGenerator', 'Graph traversal completed', { 
          entityId: result.entity.id,
          componentCount: Object.keys(result.entity.components).length,
          eventCount: result.events.length,
        }, 'offline');

        setTraversalResult(result);
        setDungeonLayout(null);
        setGenerationPhase('Complete!');
        
        debugLogger.success('GameObjectGenerator', 'Output state updated successfully', { 
          hasResult: !!result,
          entityId: result.entity.id,
        }, 'offline');
      }
    } catch (err: any) {
      const errorMsg = err.message || 'Unknown error during generation';
      setError(errorMsg);
      setGenerationPhase('');
      debugLogger.error('GameObjectGenerator', `Generation failed: ${errorMsg}`, { error: err }, 'offline');
    } finally {
      setIsGenerating(false);
    }
  };

  const handleStopGeneration = () => {
    setIsGenerating(false);
    setGenerationPhase('');
    debugLogger.warn('GameObjectGenerator', 'Generation stopped by user', undefined, 'offline');
  };

  const handleReset = () => {
    setTraversalResult(null);
    setDungeonLayout(null);
    setError(null);
    setGenerationPhase('');
    setSeed(Date.now());
    debugLogger.info('GameObjectGenerator', 'Reset to initial state', undefined, 'offline');
  };

  const handleCopy = (content: string, section: string) => {
    navigator.clipboard.writeText(content);
    setCopiedSection(section);
    setTimeout(() => setCopiedSection(null), 2000);
    debugLogger.success('GameObjectGenerator', `${section} copied to clipboard`, undefined, 'offline');
  };

  const handleRandomizeSeed = () => {
    setSeed(Date.now());
  };

  const TypeIcon = generationTypeConfig[generationType].icon;
  const isDungeonType = generationType === 'dungeon';
  const hasOutput = traversalResult !== null || dungeonLayout !== null;

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border bg-card px-4 py-4 sm:px-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
              <Swords className="h-5 w-5 text-primary-foreground" />
            </div>
            <div>
              <h1 className="text-xl font-bold tracking-tight sm:text-2xl">Game Object Generator</h1>
              <p className="text-xs text-muted-foreground sm:text-sm">
                Deterministic graph traversal with ECS components and WFC dungeon generation
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant="secondary" className="bg-amber-500/20 text-amber-700 dark:text-amber-300 border-amber-500/30">
              Offline Mode
            </Badge>
          </div>
        </div>
      </div>

      {/* Offline Mode Info */}
      <Alert className="m-4 mb-0">
        <Info className="h-4 w-4" />
        <AlertDescription>
          Generator is running in offline mode with client-side processing. All generation happens locally.
        </AlertDescription>
      </Alert>

      {/* Main Content */}
      <div className="flex flex-1 flex-col gap-4 overflow-hidden p-4 sm:p-6 lg:flex-row">
        {/* Left Panel - Controls */}
        <div className="flex w-full flex-col gap-4 lg:w-96">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Generation Type</CardTitle>
              <CardDescription>Select what type of game object to generate</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Select value={generationType} onValueChange={(value) => setGenerationType(value as GenerationType)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(generationTypeConfig).map(([key, config]) => {
                    const Icon = config.icon;
                    return (
                      <SelectItem key={key} value={key}>
                        <div className="flex items-center gap-2">
                          <Icon className={`h-4 w-4 ${config.color}`} />
                          <span>{config.label}</span>
                        </div>
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>

              <div className="rounded-lg border border-border bg-muted/30 p-3">
                <div className="flex items-start gap-2">
                  <TypeIcon className={`h-5 w-5 mt-0.5 ${generationTypeConfig[generationType].color}`} />
                  <p className="text-xs text-muted-foreground">
                    {generationTypeConfig[generationType].description}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Dungeon-specific tileset upload */}
          {isDungeonType && (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Tileset Configuration</CardTitle>
                <CardDescription>Upload YAML tileset for WFC generation</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="tileset-upload">YAML Tileset File</Label>
                  <div className="flex gap-2">
                    <input
                      id="tileset-upload"
                      type="file"
                      accept=".yaml,.yml"
                      onChange={handleTilesetUpload}
                      className="hidden"
                    />
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => document.getElementById('tileset-upload')?.click()}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      {tilesetConfig ? 'Change Tileset' : 'Upload Tileset'}
                    </Button>
                  </div>
                  {tilesetConfig && (
                    <div className="rounded-md border border-green-500/20 bg-green-500/10 p-2">
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ Tileset loaded: {Object.keys(tilesetConfig.tiles).length} tiles, {Object.keys(tilesetConfig.adjacency).length} adjacency rules
                      </p>
                    </div>
                  )}
                  <p className="text-xs text-muted-foreground">
                    Required for dungeon generation with tiles, adjacency, and map_constraints sections
                  </p>
                </div>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Parameters</CardTitle>
              <CardDescription>Configure generation settings</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="seed">Seed (Deterministic RNG)</Label>
                <div className="flex gap-2">
                  <Input
                    id="seed"
                    type="number"
                    value={seed}
                    onChange={(e) => setSeed(parseInt(e.target.value) || 0)}
                    className="flex-1"
                  />
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={handleRandomizeSeed}
                  >
                    <RotateCcw className="h-4 w-4" />
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">
                  Same seed produces identical results
                </p>
              </div>

              {!isDungeonType && (
                <div className="space-y-2">
                  <Label htmlFor="randomness">Randomness Factor: {(randomnessFactor * 100).toFixed(0)}%</Label>
                  <input
                    id="randomness"
                    type="range"
                    min="0"
                    max="100"
                    value={randomnessFactor * 100}
                    onChange={(e) => setRandomnessFactor(parseInt(e.target.value) / 100)}
                    className="w-full"
                  />
                  <p className="text-xs text-muted-foreground">
                    Balance between weighted and random selection
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Controls</CardTitle>
              <CardDescription>Execute generation and manage output</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Control Buttons */}
              <div className="flex flex-col gap-2">
                {!isGenerating ? (
                  <Button
                    size="lg"
                    onClick={handleStartGeneration}
                    className="w-full"
                    disabled={isDungeonType && !tilesetConfig}
                  >
                    <Play className="mr-2 h-5 w-5" />
                    Start Generation
                  </Button>
                ) : (
                  <Button
                    size="lg"
                    variant="destructive"
                    onClick={handleStopGeneration}
                    className="w-full"
                  >
                    <Square className="mr-2 h-5 w-5" />
                    Stop Generation
                  </Button>
                )}
                <Button
                  size="lg"
                  variant="outline"
                  onClick={handleReset}
                  disabled={isGenerating}
                  className="w-full"
                >
                  <RotateCcw className="mr-2 h-5 w-5" />
                  Reset
                </Button>
              </div>

              {isGenerating && (
                <div className="space-y-2">
                  <div className="flex items-center justify-center py-2">
                    <Loader2 className="h-6 w-6 animate-spin text-primary" />
                  </div>
                  {generationPhase && (
                    <p className="text-center text-xs text-muted-foreground">
                      {generationPhase}
                    </p>
                  )}
                </div>
              )}

              <Separator />

              {/* Generation Info */}
              <div className="space-y-2">
                <h3 className="text-sm font-semibold">Generation Settings</h3>
                <div className="space-y-1 text-xs text-muted-foreground">
                  <div className="flex justify-between">
                    <span>Type:</span>
                    <span className="font-medium capitalize">{generationType}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Seed:</span>
                    <span className="font-mono font-medium">{seed}</span>
                  </div>
                  {!isDungeonType && (
                    <div className="flex justify-between">
                      <span>Randomness:</span>
                      <span className="font-medium">{(randomnessFactor * 100).toFixed(0)}%</span>
                    </div>
                  )}
                  {traversalResult && (
                    <>
                      <div className="flex justify-between">
                        <span>Nodes Traversed:</span>
                        <span className="font-medium text-primary">{traversalResult.entity.metadata.totalNodes}</span>
                      </div>
                      <div className="flex justify-between">
                        <span>Events Emitted:</span>
                        <span className="font-medium text-primary">{traversalResult.entity.metadata.emittedEvents}</span>
                      </div>
                      <div className="flex justify-between">
                        <span>Components:</span>
                        <span className="font-medium text-primary">{Object.keys(traversalResult.entity.components).length}</span>
                      </div>
                    </>
                  )}
                  {dungeonLayout && (
                    <>
                      <div className="flex justify-between">
                        <span>Grid Size:</span>
                        <span className="font-medium text-primary">{dungeonLayout.width}×{dungeonLayout.height}</span>
                      </div>
                      <div className="flex justify-between">
                        <span>Cells Collapsed:</span>
                        <span className="font-medium text-primary">{dungeonLayout.metadata.collapsedCells}/{dungeonLayout.metadata.totalCells}</span>
                      </div>
                      <div className="flex justify-between">
                        <span>WFC Iterations:</span>
                        <span className="font-medium text-primary">{dungeonLayout.metadata.iterations}</span>
                      </div>
                    </>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Error Display */}
          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Error</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {/* Traversal Log */}
          {traversalResult && traversalResult.logs.length > 0 && (
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-sm">Traversal Log</CardTitle>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => handleCopy(traversalResult.logs.join('\n'), 'Traversal Log')}
                  >
                    {copiedSection === 'Traversal Log' ? (
                      <Check className="h-3 w-3" />
                    ) : (
                      <Copy className="h-3 w-3" />
                    )}
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                <ScrollArea className="h-64">
                  <div className="space-y-1 px-4 pb-4">
                    {traversalResult.logs.map((log, idx) => (
                      <div key={idx} className="text-[10px] font-mono text-muted-foreground break-all">
                        {log}
                      </div>
                    ))}
                  </div>
                </ScrollArea>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Right Panel - Output */}
        <Card className="flex flex-1 flex-col overflow-hidden">
          <CardHeader className="flex-shrink-0">
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg">Generated Output</CardTitle>
                <CardDescription>
                  {isDungeonType 
                    ? 'View dungeon layout and WFC generation results'
                    : 'View resolved entity, component events, and raw JSON'}
                </CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex-1 overflow-hidden p-0">
            {hasOutput ? (
              dungeonLayout ? (
                // Dungeon Layout Display
                <div className="flex h-full flex-col gap-4 p-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold">Dungeon Layout ({dungeonLayout.width}×{dungeonLayout.height})</h3>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleCopy(JSON.stringify(dungeonLayout, null, 2), 'Dungeon Layout')}
                    >
                      {copiedSection === 'Dungeon Layout' ? (
                        <>
                          <Check className="mr-2 h-3 w-3" />
                          Copied
                        </>
                      ) : (
                        <>
                          <Copy className="mr-2 h-3 w-3" />
                          Copy JSON
                        </>
                      )}
                    </Button>
                  </div>
                  
                  <ScrollArea className="flex-1">
                    <div className="space-y-4 pr-4">
                      {/* Dungeon Grid Visualization */}
                      <div className="rounded-lg border border-border bg-muted/30 p-4">
                        <h4 className="mb-3 text-xs font-semibold text-muted-foreground uppercase">Grid Visualization</h4>
                        <div className="grid gap-0.5" style={{ 
                          gridTemplateColumns: `repeat(${dungeonLayout.width}, minmax(0, 1fr))`,
                          maxWidth: '100%',
                          aspectRatio: `${dungeonLayout.width}/${dungeonLayout.height}`
                        }}>
                          {dungeonLayout.cells.flat().map((cell, idx) => (
                            <div
                              key={idx}
                              className={`aspect-square rounded-sm border ${
                                cell.collapsed 
                                  ? 'bg-primary/20 border-primary/40' 
                                  : 'bg-muted border-muted-foreground/20'
                              }`}
                              title={cell.tileId || 'Uncollapsed'}
                            />
                          ))}
                        </div>
                      </div>

                      {/* Generation Metadata */}
                      <div className="rounded-lg border border-border bg-card p-4">
                        <h4 className="mb-3 text-sm font-semibold text-primary">Generation Metadata</h4>
                        <div className="space-y-2 text-sm">
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Generated:</span>
                            <span className="text-xs">{new Date(dungeonLayout.metadata.generatedAt).toLocaleString()}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Seed:</span>
                            <span className="font-mono text-xs">{dungeonLayout.metadata.seed}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Total Cells:</span>
                            <span className="font-mono text-xs">{dungeonLayout.metadata.totalCells}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Collapsed Cells:</span>
                            <span className="font-mono text-xs">{dungeonLayout.metadata.collapsedCells}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">WFC Iterations:</span>
                            <span className="font-mono text-xs">{dungeonLayout.metadata.iterations}</span>
                          </div>
                        </div>
                      </div>

                      {/* Tile Statistics */}
                      <div className="rounded-lg border border-border bg-card p-4">
                        <h4 className="mb-3 text-sm font-semibold text-primary">Tile Distribution</h4>
                        <div className="space-y-2">
                          {Object.entries(
                            dungeonLayout.cells.flat()
                              .filter(c => c.tileId)
                              .reduce((acc, c) => {
                                acc[c.tileId!] = (acc[c.tileId!] || 0) + 1;
                                return acc;
                              }, {} as Record<string, number>)
                          ).map(([tileId, count]) => (
                            <div key={tileId} className="flex justify-between text-sm">
                              <span className="text-muted-foreground">{tileId}:</span>
                              <span className="font-mono text-xs">{count}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  </ScrollArea>
                </div>
              ) : (
                // Graph Traversal Result Display
                <Tabs defaultValue="entity" className="flex h-full flex-col">
                  <TabsList className="mx-4 mt-4 grid w-auto grid-cols-2">
                    <TabsTrigger value="entity">Resolved Entity</TabsTrigger>
                    <TabsTrigger value="raw">Raw JSON</TabsTrigger>
                  </TabsList>
                  
                  {/* Resolved Entity Tab */}
                  <TabsContent value="entity" className="flex-1 overflow-hidden m-0 p-4">
                    <div className="flex h-full flex-col gap-4 overflow-hidden">
                      <div className="flex items-center justify-between">
                        <h3 className="text-sm font-semibold">ECS Components</h3>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleCopy(JSON.stringify(traversalResult!.entity, null, 2), 'Resolved Entity')}
                        >
                          {copiedSection === 'Resolved Entity' ? (
                            <>
                              <Check className="mr-2 h-3 w-3" />
                              Copied
                            </>
                          ) : (
                            <>
                              <Copy className="mr-2 h-3 w-3" />
                              Copy JSON
                            </>
                          )}
                        </Button>
                      </div>
                      
                      <ScrollArea className="flex-1">
                        <div className="space-y-4 pr-4">
                          {/* Entity Metadata */}
                          <div className="rounded-lg border border-border bg-muted/30 p-4">
                            <h4 className="mb-2 text-xs font-semibold text-muted-foreground uppercase">Entity Metadata</h4>
                            <div className="space-y-1 text-sm">
                              <div className="flex justify-between">
                                <span className="text-muted-foreground">ID:</span>
                                <span className="font-mono text-xs">{traversalResult!.entity.id}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-muted-foreground">Generated:</span>
                                <span className="text-xs">{new Date(traversalResult!.entity.generatedAt).toLocaleString()}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-muted-foreground">Seed:</span>
                                <span className="font-mono text-xs">{traversalResult!.entity.seed}</span>
                              </div>
                            </div>
                          </div>

                          {/* Components */}
                          {Object.entries(traversalResult!.entity.components).map(([componentName, componentData]) => (
                            <div key={componentName} className="rounded-lg border border-border bg-card p-4">
                              <h4 className="mb-3 text-sm font-semibold text-primary">{componentName}</h4>
                              <div className="space-y-2">
                                {Object.entries(componentData as Record<string, any>).map(([key, value]) => (
                                  <div key={key} className="flex justify-between text-sm">
                                    <span className="text-muted-foreground">{key}:</span>
                                    <span className="font-mono text-xs max-w-[60%] text-right break-all">
                                      {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                                    </span>
                                  </div>
                                ))}
                              </div>
                            </div>
                          ))}
                        </div>
                      </ScrollArea>
                    </div>
                  </TabsContent>

                  {/* Raw JSON Tab */}
                  <TabsContent value="raw" className="flex-1 overflow-hidden m-0 p-4">
                    <div className="flex h-full flex-col gap-4 overflow-hidden">
                      <div className="flex items-center justify-between">
                        <h3 className="text-sm font-semibold">Complete JSON Output</h3>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleCopy(JSON.stringify(traversalResult, null, 2), 'Raw JSON')}
                        >
                          {copiedSection === 'Raw JSON' ? (
                            <>
                              <Check className="mr-2 h-3 w-3" />
                              Copied
                            </>
                          ) : (
                            <>
                              <Copy className="mr-2 h-3 w-3" />
                              Copy JSON
                            </>
                          )}
                        </Button>
                      </div>
                      
                      <ScrollArea className="flex-1">
                        <pre className="rounded-lg bg-muted p-4 text-xs font-mono overflow-x-auto">
                          {JSON.stringify(traversalResult, null, 2)}
                        </pre>
                      </ScrollArea>
                    </div>
                  </TabsContent>
                </Tabs>
              )
            ) : (
              <div className="flex h-full items-center justify-center p-8 text-center">
                <div className="space-y-3">
                  <Swords className="mx-auto h-16 w-16 text-muted-foreground/30" />
                  <div className="space-y-1">
                    <p className="text-sm font-medium text-muted-foreground">
                      No output yet
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {isDungeonType 
                        ? 'Upload a tileset YAML and click "Start Generation" to create dungeons'
                        : 'Configure parameters and click "Start Generation" to create game objects'}
                    </p>
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
