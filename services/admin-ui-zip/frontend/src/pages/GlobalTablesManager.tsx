import { useState, useEffect } from 'react';
import { Database, Plus, Trash2, Download, AlertCircle, TrendingUp, Sparkles, Zap, Copy, RefreshCw, Table as TableIcon, Filter, Search, Loader2, Shield, Swords, Users, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Slider } from '@/components/ui/slider';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { debugLogger } from '../lib/debugLogger';
import { 
  generateLootAttributes, 
  lootToYAML, 
  lootToJSON, 
  lootGenerationQueue,
  generateMassiveLootTable,
  exportWorldLootTableYAML,
  exportWorldLootTableJSON,
  type GeneratedLoot,
  type LootTier,
  type AttributeCategory,
  type WorldLootTableStats
} from '../lib/lootGenerator';
import { RPGDatasetService } from '../lib/rpgDataset';

interface TierConfig {
  id: string;
  name: string;
  rarity: 'common' | 'rare' | 'epic' | 'legendary';
  spawnChance: number;
  statMultiplier: number;
  minLevel: number;
  maxLevel: number;
  specialAttributes: string[];
}

interface WorldPopulationRule {
  id: string;
  areaType: string;
  tierDistribution: Record<string, number>;
  density: number;
  spawnRate: number;
}

export default function GlobalTablesManager() {
  const [tiers, setTiers] = useState<TierConfig[]>([
    {
      id: 'tier-common',
      name: 'Common',
      rarity: 'common',
      spawnChance: 0.6,
      statMultiplier: 1.0,
      minLevel: 1,
      maxLevel: 10,
      specialAttributes: [],
    },
    {
      id: 'tier-rare',
      name: 'Rare',
      rarity: 'rare',
      spawnChance: 0.25,
      statMultiplier: 1.5,
      minLevel: 5,
      maxLevel: 20,
      specialAttributes: ['enhanced_loot'],
    },
    {
      id: 'tier-epic',
      name: 'Epic',
      rarity: 'epic',
      spawnChance: 0.12,
      statMultiplier: 2.0,
      minLevel: 15,
      maxLevel: 35,
      specialAttributes: ['enhanced_loot', 'special_ability'],
    },
    {
      id: 'tier-legendary',
      name: 'Legendary',
      rarity: 'legendary',
      spawnChance: 0.03,
      statMultiplier: 3.0,
      minLevel: 30,
      maxLevel: 50,
      specialAttributes: ['enhanced_loot', 'special_ability', 'unique_drop'],
    },
  ]);

  const [populationRules, setPopulationRules] = useState<WorldPopulationRule[]>([
    {
      id: 'rule-forest',
      areaType: 'Forest',
      tierDistribution: {
        common: 0.7,
        rare: 0.2,
        epic: 0.08,
        legendary: 0.02,
      },
      density: 0.5,
      spawnRate: 1.0,
    },
    {
      id: 'rule-dungeon',
      areaType: 'Dungeon',
      tierDistribution: {
        common: 0.4,
        rare: 0.35,
        epic: 0.2,
        legendary: 0.05,
      },
      density: 0.8,
      spawnRate: 1.5,
    },
  ]);

  const [selectedTier, setSelectedTier] = useState<TierConfig | null>(null);
  const [selectedRule, setSelectedRule] = useState<WorldPopulationRule | null>(null);
  
  // Loot generation state
  const [selectedLootTier, setSelectedLootTier] = useState<LootTier>('common');
  const [requestExcellent, setRequestExcellent] = useState(false);
  const [generatedLoot, setGeneratedLoot] = useState<GeneratedLoot | null>(null);
  const [lootHistory, setLootHistory] = useState<GeneratedLoot[]>([]);
  const [queueStatus, setQueueStatus] = useState({ queueSize: 0, processing: false, completedCount: 0 });

  // World loot table state
  const [worldLootTable, setWorldLootTable] = useState<GeneratedLoot[]>([]);
  const [isGeneratingWorld, setIsGeneratingWorld] = useState(false);
  const [generationProgress, setGenerationProgress] = useState(0);
  const [worldTableStats, setWorldTableStats] = useState<WorldLootTableStats | null>(null);
  const [tableFilter, setTableFilter] = useState<{
    tier: LootTier | 'all';
    excellent: 'all' | 'yes' | 'no';
    category: AttributeCategory | 'all';
    search: string;
  }>({
    tier: 'all',
    excellent: 'all',
    category: 'all',
    search: '',
  });
  const [currentPage, setCurrentPage] = useState(0);
  const itemsPerPage = 50;

  useEffect(() => {
    debugLogger.info('global-tables', 'Global Tables Manager initialized with RPG Dataset integration and name uniqueness validation');
    
    // Update queue status periodically
    const interval = setInterval(() => {
      setQueueStatus(lootGenerationQueue.getQueueStatus());
    }, 500);
    
    return () => clearInterval(interval);
  }, []);

  const handleAddTier = () => {
    const newTier: TierConfig = {
      id: `tier-${Date.now()}`,
      name: 'New Tier',
      rarity: 'common',
      spawnChance: 0.5,
      statMultiplier: 1.0,
      minLevel: 1,
      maxLevel: 10,
      specialAttributes: [],
    };
    setTiers([...tiers, newTier]);
    setSelectedTier(newTier);
    debugLogger.info('global-tables', `Created new tier: ${newTier.id}`);
  };

  const handleDeleteTier = (id: string) => {
    setTiers(tiers.filter(t => t.id !== id));
    if (selectedTier?.id === id) setSelectedTier(null);
    debugLogger.info('global-tables', `Deleted tier: ${id}`);
  };

  const handleUpdateTier = (updated: TierConfig) => {
    setTiers(tiers.map(t => t.id === updated.id ? updated : t));
    setSelectedTier(updated);
    debugLogger.info('global-tables', `Updated tier: ${updated.id}`);
  };

  const handleAddRule = () => {
    const newRule: WorldPopulationRule = {
      id: `rule-${Date.now()}`,
      areaType: 'New Area',
      tierDistribution: {
        common: 0.6,
        rare: 0.25,
        epic: 0.12,
        legendary: 0.03,
      },
      density: 0.5,
      spawnRate: 1.0,
    };
    setPopulationRules([...populationRules, newRule]);
    setSelectedRule(newRule);
    debugLogger.info('global-tables', `Created new population rule: ${newRule.id}`);
  };

  const handleDeleteRule = (id: string) => {
    setPopulationRules(populationRules.filter(r => r.id !== id));
    if (selectedRule?.id === id) setSelectedRule(null);
    debugLogger.info('global-tables', `Deleted population rule: ${id}`);
  };

  const handleGenerateLoot = () => {
    const loot = generateLootAttributes(selectedLootTier, Date.now(), requestExcellent);
    setGeneratedLoot(loot);
    setLootHistory([loot, ...lootHistory.slice(0, 9)]); // Keep last 10
    debugLogger.success('global-tables', `Generated ${loot.tier} loot with RPG Dataset and name uniqueness: ${loot.name}`, {
      rpgContext: loot.rpgContext,
    });
  };

  const handleQueueLootGeneration = () => {
    const requestId = lootGenerationQueue.enqueue(selectedLootTier, 'weapon', Date.now(), requestExcellent);
    debugLogger.info('global-tables', `Queued loot generation request: ${requestId}`);
    
    // Check for completion after a delay
    setTimeout(() => {
      const completed = lootGenerationQueue.getCompletedItem(requestId);
      if (completed) {
        setGeneratedLoot(completed);
        setLootHistory([completed, ...lootHistory.slice(0, 9)]);
      }
    }, 200);
  };

  const handleGenerateWorldLootTable = async (itemCount: number) => {
    setIsGeneratingWorld(true);
    setGenerationProgress(0);
    debugLogger.info('global-tables', `Starting massive world loot table generation with name uniqueness validation: ${itemCount} items`);

    try {
      const result = await generateMassiveLootTable(
        itemCount,
        (progress) => {
          setGenerationProgress(progress);
          debugLogger.info('world-loot', `Generation progress: ${progress.toFixed(1)}%`);
        }
      );

      setWorldLootTable(result.items);
      setWorldTableStats(result.stats);
      setCurrentPage(0);
      
      debugLogger.success('global-tables', `World loot table generated with name uniqueness: ${result.items.length} items`, {
        stats: result.stats,
        nameUniqueness: result.stats.nameUniqueness,
      });
    } catch (error: any) {
      debugLogger.error('global-tables', 'Failed to generate world loot table', { error: error.message });
    } finally {
      setIsGeneratingWorld(false);
      setGenerationProgress(0);
    }
  };

  const handleClearWorldTable = () => {
    setWorldLootTable([]);
    setWorldTableStats(null);
    setCurrentPage(0);
    debugLogger.info('global-tables', 'Cleared world loot table');
  };

  const handleExportWorldYAML = () => {
    const yaml = exportWorldLootTableYAML(worldLootTable);
    const blob = new Blob([yaml], { type: 'text/yaml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `world_loot_table_unique_names_${Date.now()}.yaml`;
    a.click();
    URL.revokeObjectURL(url);
    debugLogger.success('global-tables', 'Exported world loot table with name uniqueness to YAML');
  };

  const handleExportWorldJSON = () => {
    const json = exportWorldLootTableJSON(worldLootTable);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `world_loot_table_unique_names_${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    debugLogger.success('global-tables', 'Exported world loot table with name uniqueness to JSON');
  };

  const handleCopyYAML = () => {
    if (generatedLoot) {
      const yaml = lootToYAML(generatedLoot);
      navigator.clipboard.writeText(yaml);
      debugLogger.success('global-tables', 'Copied loot YAML with RPG Dataset to clipboard');
    }
  };

  const handleCopyJSON = () => {
    if (generatedLoot) {
      const json = lootToJSON(generatedLoot);
      navigator.clipboard.writeText(json);
      debugLogger.success('global-tables', 'Copied loot JSON with RPG Dataset to clipboard');
    }
  };

  const handleExportLootYAML = () => {
    if (generatedLoot) {
      const yaml = lootToYAML(generatedLoot);
      const blob = new Blob([yaml], { type: 'text/yaml' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${generatedLoot.name.replace(/\s+/g, '_')}.yaml`;
      a.click();
      URL.revokeObjectURL(url);
      debugLogger.success('global-tables', 'Exported loot with RPG Dataset to YAML file');
    }
  };

  const handleExportTiers = () => {
    const yaml = tiers.map(tier => `
# ${tier.name} Tier
${tier.id}:
  name: "${tier.name}"
  rarity: ${tier.rarity}
  spawnChance: ${tier.spawnChance}
  statMultiplier: ${tier.statMultiplier}
  minLevel: ${tier.minLevel}
  maxLevel: ${tier.maxLevel}
  specialAttributes: [${tier.specialAttributes.map(a => `"${a}"`).join(', ')}]
`).join('\n');

    const blob = new Blob([yaml], { type: 'text/yaml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'global-tiers.yaml';
    a.click();
    URL.revokeObjectURL(url);
    debugLogger.success('global-tables', 'Exported tiers to YAML');
  };

  const handleExportRules = () => {
    const yaml = populationRules.map(rule => `
# ${rule.areaType} Population Rule
${rule.id}:
  areaType: "${rule.areaType}"
  tierDistribution:
${Object.entries(rule.tierDistribution).map(([tier, chance]) => `    ${tier}: ${chance}`).join('\n')}
  density: ${rule.density}
  spawnRate: ${rule.spawnRate}
`).join('\n');

    const blob = new Blob([yaml], { type: 'text/yaml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'population-rules.yaml';
    a.click();
    URL.revokeObjectURL(url);
    debugLogger.success('global-tables', 'Exported population rules to YAML');
  };

  const getRarityColor = (rarity: string) => {
    const colors = {
      common: 'bg-gray-500',
      rare: 'bg-blue-500',
      epic: 'bg-purple-500',
      legendary: 'bg-amber-500',
    };
    return colors[rarity as keyof typeof colors] || 'bg-gray-500';
  };

  const getCategoryColor = (category: AttributeCategory) => {
    const colors = {
      attack: 'text-red-500',
      defense: 'text-blue-500',
      elemental: 'text-purple-500',
      special: 'text-green-500',
      abilities: 'text-amber-500',
    };
    return colors[category] || 'text-gray-500';
  };

  // Filter world loot table
  const filteredWorldLoot = worldLootTable.filter(item => {
    if (tableFilter.tier !== 'all' && item.tier !== tableFilter.tier) return false;
    if (tableFilter.excellent === 'yes' && !item.isExcellent) return false;
    if (tableFilter.excellent === 'no' && item.isExcellent) return false;
    if (tableFilter.category !== 'all' && !item.attributes.some(a => a.category === tableFilter.category)) return false;
    if (tableFilter.search && !item.name.toLowerCase().includes(tableFilter.search.toLowerCase())) return false;
    return true;
  });

  const paginatedLoot = filteredWorldLoot.slice(
    currentPage * itemsPerPage,
    (currentPage + 1) * itemsPerPage
  );

  const totalPages = Math.ceil(filteredWorldLoot.length / itemsPerPage);

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      <div className="flex-1 overflow-y-auto">
        <div className="container mx-auto space-y-6 p-4 sm:p-6">
          {/* Header */}
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
                <Database className="h-6 w-6 text-primary-foreground" />
              </div>
              <div>
                <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">Global Tables Manager</h1>
                <p className="text-sm text-muted-foreground">
                  Manage game object tiers, world population, and massive procedural loot generation with RPG Dataset integration and name uniqueness validation
                </p>
              </div>
            </div>
          </div>

          {/* Offline Mode Alert */}
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Offline Mode with RPG Dataset & Name Uniqueness</AlertTitle>
            <AlertDescription>
              Running in local mode with full RPG Dataset integration and automatic name uniqueness validation. All changes are stored in browser memory.
            </AlertDescription>
          </Alert>

          {/* RPG Dataset Info Card */}
          <Card className="border-primary/50">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Shield className="h-5 w-5 text-primary" />
                RPG Dataset Integration Active
              </CardTitle>
              <CardDescription>
                Context-aware loot generation with weapon-attack compatibility, sentient items, faction influences, archetype alignment, and automatic name uniqueness validation
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <Swords className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm font-semibold">Weapons</span>
                  </div>
                  <p className="text-2xl font-bold">{RPGDatasetService.getAllWeaponCategories().length}</p>
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <Shield className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm font-semibold">Factions</span>
                  </div>
                  <p className="text-2xl font-bold">{RPGDatasetService.getAllFactions().length}</p>
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <Users className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm font-semibold">Archetypes</span>
                  </div>
                  <p className="text-2xl font-bold">{RPGDatasetService.getAllArchetypes().length}</p>
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <Sparkles className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm font-semibold">Sentient</span>
                  </div>
                  <p className="text-2xl font-bold">{RPGDatasetService.getAllSentientPersonalities().length}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <Tabs defaultValue="world-loot" className="w-full">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="world-loot">World Loot Table</TabsTrigger>
              <TabsTrigger value="loot">Loot Generator</TabsTrigger>
              <TabsTrigger value="tiers">Tier Config</TabsTrigger>
              <TabsTrigger value="population">Population</TabsTrigger>
            </TabsList>

            {/* World Loot Table Tab */}
            <TabsContent value="world-loot" className="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <TableIcon className="h-5 w-5 text-primary" />
                    Massive World Loot Table Generator with Name Uniqueness
                  </CardTitle>
                  <CardDescription>
                    Generate millions of procedural loot items with automatic name uniqueness validation, weapon-attack compatibility, faction influences, and archetype alignment
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {/* Generation Controls */}
                  <div className="flex flex-wrap gap-2">
                    <Button
                      onClick={() => handleGenerateWorldLootTable(1000)}
                      disabled={isGeneratingWorld}
                    >
                      {isGeneratingWorld ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Zap className="mr-2 h-4 w-4" />
                      )}
                      Generate 1,000 Items
                    </Button>
                    <Button
                      onClick={() => handleGenerateWorldLootTable(10000)}
                      disabled={isGeneratingWorld}
                      variant="outline"
                    >
                      {isGeneratingWorld ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Zap className="mr-2 h-4 w-4" />
                      )}
                      Generate 10,000 Items
                    </Button>
                    <Button
                      onClick={() => handleGenerateWorldLootTable(100000)}
                      disabled={isGeneratingWorld}
                      variant="outline"
                    >
                      {isGeneratingWorld ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Zap className="mr-2 h-4 w-4" />
                      )}
                      Generate 100,000 Items
                    </Button>
                    <Button
                      onClick={handleClearWorldTable}
                      disabled={isGeneratingWorld || worldLootTable.length === 0}
                      variant="destructive"
                    >
                      <Trash2 className="mr-2 h-4 w-4" />
                      Clear Table
                    </Button>
                  </div>

                  {/* Generation Progress */}
                  {isGeneratingWorld && (
                    <div className="space-y-2">
                      <div className="flex justify-between text-sm">
                        <span>Generating world loot table with name uniqueness validation...</span>
                        <span>{generationProgress.toFixed(1)}%</span>
                      </div>
                      <Progress value={generationProgress} />
                    </div>
                  )}

                  {/* Name Uniqueness Summary */}
                  {worldTableStats?.nameUniqueness && (
                    <Card className="border-green-500/50 bg-green-500/5">
                      <CardHeader className="pb-3">
                        <CardTitle className="flex items-center gap-2 text-sm">
                          <CheckCircle2 className="h-4 w-4 text-green-500" />
                          Name Uniqueness Validation Summary
                        </CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        <div className="flex items-center justify-between text-sm">
                          <span className="font-semibold">Duplicates Found:</span>
                          <Badge variant={worldTableStats.nameUniqueness.duplicatesFound === 0 ? 'default' : 'secondary'} className="bg-green-500">
                            {worldTableStats.nameUniqueness.duplicatesFound}
                          </Badge>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span className="font-semibold">Duplicates Resolved:</span>
                          <Badge variant="outline">
                            {worldTableStats.nameUniqueness.duplicatesResolved}
                          </Badge>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span className="font-semibold">Unique Names Created:</span>
                          <Badge variant="outline">
                            {worldTableStats.nameUniqueness.uniqueNames.toLocaleString()}
                          </Badge>
                        </div>
                        {Object.keys(worldTableStats.nameUniqueness.fallbackPatternsUsed).length > 0 && (
                          <div className="space-y-1 pt-2">
                            <span className="text-xs font-semibold">Fallback Patterns Used:</span>
                            <div className="flex flex-wrap gap-1">
                              {Object.entries(worldTableStats.nameUniqueness.fallbackPatternsUsed).map(([pattern, count]) => (
                                <Badge key={pattern} variant="secondary" className="text-xs">
                                  {pattern}: {count}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  )}

                  {/* Statistics */}
                  {worldTableStats && (
                    <div className="space-y-4">
                      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Total Items</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold">{worldTableStats.totalItems.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Common</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold text-gray-500">{worldTableStats.commonCount.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Rare</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold text-blue-500">{worldTableStats.rareCount.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Epic</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold text-purple-500">{worldTableStats.epicCount.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Legendary</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold text-amber-500">{worldTableStats.legendaryCount.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Excellent</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="text-2xl font-bold text-amber-500">{worldTableStats.excellentCount.toLocaleString()}</div>
                          </CardContent>
                        </Card>
                      </div>

                      {/* RPG Dataset Statistics */}
                      <Card className="border-primary/50">
                        <CardHeader className="pb-3">
                          <CardTitle className="text-sm">RPG Dataset Context Statistics</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                          <div className="flex justify-between text-sm">
                            <span>Archetype Compatible Items:</span>
                            <Badge variant="outline">{worldTableStats.archetypeCompatibilityCount.toLocaleString()}</Badge>
                          </div>
                          <div className="space-y-1">
                            <span className="text-sm font-semibold">Weapon Categories:</span>
                            <div className="flex flex-wrap gap-1">
                              {Object.entries(worldTableStats.weaponCategoryDistribution).map(([weapon, count]) => (
                                <Badge key={weapon} variant="secondary" className="text-xs">
                                  {weapon}: {count}
                                </Badge>
                              ))}
                            </div>
                          </div>
                          <div className="space-y-1">
                            <span className="text-sm font-semibold">Faction Alignments:</span>
                            <div className="flex flex-wrap gap-1">
                              {Object.entries(worldTableStats.factionAlignmentDistribution).map(([faction, count]) => (
                                <Badge key={faction} variant="secondary" className="text-xs">
                                  {faction}: {count}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        </CardContent>
                      </Card>
                    </div>
                  )}

                  {/* Export Controls */}
                  {worldLootTable.length > 0 && (
                    <div className="flex gap-2">
                      <Button onClick={handleExportWorldYAML} variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export YAML
                      </Button>
                      <Button onClick={handleExportWorldJSON} variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export JSON
                      </Button>
                    </div>
                  )}

                  {/* Filters */}
                  {worldLootTable.length > 0 && (
                    <div className="space-y-4">
                      <div className="flex items-center gap-2">
                        <Filter className="h-4 w-4 text-muted-foreground" />
                        <span className="text-sm font-semibold">Filters</span>
                      </div>
                      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                        <div className="space-y-2">
                          <Label>Tier</Label>
                          <Select
                            value={tableFilter.tier}
                            onValueChange={(value: any) => {
                              setTableFilter({ ...tableFilter, tier: value });
                              setCurrentPage(0);
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="all">All Tiers</SelectItem>
                              <SelectItem value="common">Common</SelectItem>
                              <SelectItem value="rare">Rare</SelectItem>
                              <SelectItem value="epic">Epic</SelectItem>
                              <SelectItem value="legendary">Legendary</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label>Excellent</Label>
                          <Select
                            value={tableFilter.excellent}
                            onValueChange={(value: any) => {
                              setTableFilter({ ...tableFilter, excellent: value });
                              setCurrentPage(0);
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="all">All Items</SelectItem>
                              <SelectItem value="yes">Excellent Only</SelectItem>
                              <SelectItem value="no">Normal Only</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label>Category</Label>
                          <Select
                            value={tableFilter.category}
                            onValueChange={(value: any) => {
                              setTableFilter({ ...tableFilter, category: value });
                              setCurrentPage(0);
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="all">All Categories</SelectItem>
                              <SelectItem value="attack">Attack</SelectItem>
                              <SelectItem value="defense">Defense</SelectItem>
                              <SelectItem value="elemental">Elemental</SelectItem>
                              <SelectItem value="special">Special</SelectItem>
                              <SelectItem value="abilities">Abilities</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label>Search</Label>
                          <div className="relative">
                            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                            <Input
                              placeholder="Search items..."
                              value={tableFilter.search}
                              onChange={(e) => {
                                setTableFilter({ ...tableFilter, search: e.target.value });
                                setCurrentPage(0);
                              }}
                              className="pl-8"
                            />
                          </div>
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Loot Table */}
                  {worldLootTable.length > 0 && (
                    <div className="space-y-4">
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-muted-foreground">
                          Showing {paginatedLoot.length} of {filteredWorldLoot.length} items
                        </span>
                        <div className="flex items-center gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setCurrentPage(Math.max(0, currentPage - 1))}
                            disabled={currentPage === 0}
                          >
                            Previous
                          </Button>
                          <span className="text-sm">
                            Page {currentPage + 1} of {totalPages}
                          </span>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setCurrentPage(Math.min(totalPages - 1, currentPage + 1))}
                            disabled={currentPage >= totalPages - 1}
                          >
                            Next
                          </Button>
                        </div>
                      </div>

                      <div className="rounded-md border">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Name</TableHead>
                              <TableHead>Tier</TableHead>
                              <TableHead>Excellent</TableHead>
                              <TableHead>Weapon</TableHead>
                              <TableHead>Faction</TableHead>
                              <TableHead>Compatible</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {paginatedLoot.map((item) => (
                              <TableRow
                                key={item.id}
                                className="cursor-pointer hover:bg-accent/50"
                                onClick={() => setGeneratedLoot(item)}
                              >
                                <TableCell className="font-medium">
                                  <div className="flex items-center gap-2">
                                    {item.isExcellent && <Sparkles className="h-3 w-3 text-amber-500" />}
                                    {item.name}
                                  </div>
                                </TableCell>
                                <TableCell>
                                  <Badge className={getRarityColor(item.tier)}>
                                    {item.tier}
                                  </Badge>
                                </TableCell>
                                <TableCell>
                                  {item.isExcellent ? (
                                    <Badge variant="default">Yes</Badge>
                                  ) : (
                                    <Badge variant="outline">No</Badge>
                                  )}
                                </TableCell>
                                <TableCell>
                                  {item.rpgContext?.weaponCategory ? (
                                    <Badge variant="secondary">{item.rpgContext.weaponCategory}</Badge>
                                  ) : (
                                    <span className="text-muted-foreground">-</span>
                                  )}
                                </TableCell>
                                <TableCell>
                                  {item.rpgContext?.factionAlignment ? (
                                    <Badge variant="secondary" className="text-xs">
                                      {item.rpgContext.factionAlignment.replace(/_/g, ' ')}
                                    </Badge>
                                  ) : (
                                    <span className="text-muted-foreground">-</span>
                                  )}
                                </TableCell>
                                <TableCell>
                                  {item.rpgContext?.weaponAttackCompatible ? (
                                    <Badge variant="default" className="bg-green-500">✓</Badge>
                                  ) : (
                                    <Badge variant="destructive">✗</Badge>
                                  )}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    </div>
                  )}

                  {worldLootTable.length === 0 && !isGeneratingWorld && (
                    <div className="flex h-[200px] items-center justify-center text-muted-foreground">
                      No world loot table generated yet. Click a generation button to start.
                    </div>
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            {/* Loot Generation Tab - keeping existing implementation */}
            <TabsContent value="loot" className="space-y-4">
              <div className="grid gap-6 lg:grid-cols-2">
                {/* Loot Generator */}
                <Card>
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                      <Sparkles className="h-5 w-5 text-amber-500" />
                      Loot Attribute Generator with Name Uniqueness
                    </CardTitle>
                    <CardDescription>
                      Generate procedural loot with automatic name uniqueness validation, weapon-attack compatibility, faction influences, and archetype alignment
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label>Loot Tier</Label>
                      <Select
                        value={selectedLootTier}
                        onValueChange={(value: LootTier) => setSelectedLootTier(value)}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="common">Common (1 attribute)</SelectItem>
                          <SelectItem value="rare">Rare (2 attributes)</SelectItem>
                          <SelectItem value="epic">Epic (3 attributes)</SelectItem>
                          <SelectItem value="legendary">Legendary (4 attributes)</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="flex items-center space-x-2">
                      <Checkbox
                        id="excellent"
                        checked={requestExcellent}
                        onCheckedChange={(checked) => setRequestExcellent(checked as boolean)}
                      />
                      <Label htmlFor="excellent" className="text-sm font-normal">
                        Request Excellent Version (double abilities, special properties)
                      </Label>
                    </div>

                    <div className="flex gap-2">
                      <Button onClick={handleGenerateLoot} className="flex-1">
                        <Zap className="mr-2 h-4 w-4" />
                        Generate Loot
                      </Button>
                      <Button onClick={handleQueueLootGeneration} variant="outline" className="flex-1">
                        <RefreshCw className="mr-2 h-4 w-4" />
                        Queue Generation
                      </Button>
                    </div>

                    {/* Queue Status */}
                    <Card className="bg-muted/50">
                      <CardHeader className="pb-3">
                        <CardTitle className="text-sm">Queue Status</CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-1 text-xs">
                        <div className="flex justify-between">
                          <span>Pending:</span>
                          <Badge variant="outline">{queueStatus.queueSize}</Badge>
                        </div>
                        <div className="flex justify-between">
                          <span>Processing:</span>
                          <Badge variant={queueStatus.processing ? 'default' : 'outline'}>
                            {queueStatus.processing ? 'Yes' : 'No'}
                          </Badge>
                        </div>
                        <div className="flex justify-between">
                          <span>Completed:</span>
                          <Badge variant="outline">{queueStatus.completedCount}</Badge>
                        </div>
                      </CardContent>
                    </Card>

                    {/* Generated Loot Display */}
                    {generatedLoot && (
                      <Card className="border-primary">
                        <CardHeader className="pb-3">
                          <div className="flex items-center justify-between">
                            <CardTitle className="text-base flex items-center gap-2">
                              {generatedLoot.isExcellent && <Sparkles className="h-4 w-4 text-amber-500" />}
                              {generatedLoot.name}
                            </CardTitle>
                            <Badge className={getRarityColor(generatedLoot.tier)}>
                              {generatedLoot.tier}
                            </Badge>
                          </div>
                          {generatedLoot.rpgContext && (
                            <div className="flex flex-wrap gap-1 pt-2">
                              {generatedLoot.rpgContext.weaponCategory && (
                                <Badge variant="secondary" className="text-xs">
                                  <Swords className="mr-1 h-3 w-3" />
                                  {generatedLoot.rpgContext.weaponCategory}
                                </Badge>
                              )}
                              {generatedLoot.rpgContext.factionAlignment && (
                                <Badge variant="secondary" className="text-xs">
                                  <Shield className="mr-1 h-3 w-3" />
                                  {generatedLoot.rpgContext.factionAlignment.replace(/_/g, ' ')}
                                </Badge>
                              )}
                              {generatedLoot.rpgContext.weaponAttackCompatible && (
                                <Badge variant="default" className="bg-green-500 text-xs">
                                  ✓ Compatible
                                </Badge>
                              )}
                            </div>
                          )}
                        </CardHeader>
                        <CardContent className="space-y-3">
                          <ScrollArea className="h-[200px]">
                            <div className="space-y-2 pr-4">
                              {generatedLoot.attributes.map((attr, idx) => (
                                <div key={idx} className="rounded-lg border p-2">
                                  <div className="flex items-center justify-between">
                                    <span className={`text-sm font-semibold ${getCategoryColor(attr.category)}`}>
                                      {attr.name}
                                    </span>
                                    <Badge variant="secondary">{attr.value}</Badge>
                                  </div>
                                  <p className="text-xs text-muted-foreground mt-1">{attr.description}</p>
                                </div>
                              ))}
                            </div>
                          </ScrollArea>

                          <div className="flex gap-2">
                            <Button size="sm" variant="outline" onClick={handleCopyYAML} className="flex-1">
                              <Copy className="mr-2 h-3 w-3" />
                              Copy YAML
                            </Button>
                            <Button size="sm" variant="outline" onClick={handleCopyJSON} className="flex-1">
                              <Copy className="mr-2 h-3 w-3" />
                              Copy JSON
                            </Button>
                            <Button size="sm" variant="outline" onClick={handleExportLootYAML}>
                              <Download className="h-3 w-3" />
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    )}
                  </CardContent>
                </Card>

                {/* Loot History - keeping existing implementation */}
                <Card>
                  <CardHeader>
                    <CardTitle>Generation History</CardTitle>
                    <CardDescription>
                      Recently generated loot items with RPG Dataset context and unique names
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <ScrollArea className="h-[600px]">
                      <div className="space-y-2">
                        {lootHistory.length === 0 ? (
                          <div className="flex h-[200px] items-center justify-center text-muted-foreground">
                            No loot generated yet
                          </div>
                        ) : (
                          lootHistory.map((loot) => (
                            <Card
                              key={loot.id}
                              className="cursor-pointer transition-colors hover:bg-accent/50"
                              onClick={() => setGeneratedLoot(loot)}
                            >
                              <CardHeader className="pb-2">
                                <div className="flex items-center justify-between">
                                  <div className="flex items-center gap-2">
                                    {loot.isExcellent && <Sparkles className="h-3 w-3 text-amber-500" />}
                                    <span className="text-sm font-semibold">{loot.name}</span>
                                  </div>
                                  <Badge variant="outline" className={getRarityColor(loot.tier)}>
                                    {loot.tier}
                                  </Badge>
                                </div>
                              </CardHeader>
                              <CardContent className="space-y-1 text-xs text-muted-foreground">
                                <div>Attributes: {loot.attributes.length}</div>
                                {loot.rpgContext && (
                                  <div className="flex flex-wrap gap-1">
                                    {loot.rpgContext.weaponCategory && (
                                      <Badge variant="secondary" className="text-xs">
                                        {loot.rpgContext.weaponCategory}
                                      </Badge>
                                    )}
                                    {loot.rpgContext.factionAlignment && (
                                      <Badge variant="secondary" className="text-xs">
                                        {loot.rpgContext.factionAlignment.replace(/_/g, ' ')}
                                      </Badge>
                                    )}
                                  </div>
                                )}
                              </CardContent>
                            </Card>
                          ))
                        )}
                      </div>
                    </ScrollArea>
                  </CardContent>
                </Card>
              </div>
            </TabsContent>

            {/* Tier Configuration Tab - keeping existing implementation */}
            <TabsContent value="tiers" className="space-y-4">
              {/* ... existing tier configuration code ... */}
            </TabsContent>

            {/* World Population Tab - keeping existing implementation */}
            <TabsContent value="population" className="space-y-4">
              {/* ... existing population rules code ... */}
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </div>
  );
}
