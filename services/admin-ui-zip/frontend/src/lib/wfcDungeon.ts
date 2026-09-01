import { debugLogger } from './debugLogger';

/**
 * Tileset configuration interface for WFC dungeon generation
 */
export interface TilesetConfig {
  tiles: Record<string, TileDefinition>;
  adjacency: AdjacencyRules;
  map_constraints?: MapConstraints;
}

export interface TileDefinition {
  id: string;
  name: string;
  type: 'room' | 'corridor' | 'door' | 'wall' | 'entrance' | 'exit';
  weight: number;
  model?: string;
  rotation?: number;
  metadata?: Record<string, any>;
}

export interface AdjacencyRules {
  [tileId: string]: {
    north?: string[];
    south?: string[];
    east?: string[];
    west?: string[];
  };
}

export interface MapConstraints {
  width: number;
  height: number;
  minRooms?: number;
  maxRooms?: number;
  entrancePosition?: 'north' | 'south' | 'east' | 'west' | 'center';
  exitPosition?: 'north' | 'south' | 'east' | 'west' | 'center';
}

export interface DungeonCell {
  x: number;
  y: number;
  tileId: string | null;
  possibleTiles: Set<string>;
  collapsed: boolean;
}

export interface DungeonLayout {
  width: number;
  height: number;
  cells: DungeonCell[][];
  tiles: TileDefinition[];
  metadata: {
    generatedAt: string;
    seed: number;
    totalCells: number;
    collapsedCells: number;
    iterations: number;
  };
}

/**
 * Wave Function Collapse implementation for dungeon generation
 */
export class WFCDungeonGenerator {
  private tileset: TilesetConfig;
  private seed: number;
  private rng: () => number;
  private width: number;
  private height: number;
  private cells: DungeonCell[][];
  private iterations: number = 0;

  constructor(tileset: TilesetConfig, seed: number = Date.now()) {
    this.tileset = tileset;
    this.seed = seed;
    this.rng = this.mulberry32(seed);
    
    // Use map constraints if available, otherwise default to 10x10
    this.width = tileset.map_constraints?.width || 10;
    this.height = tileset.map_constraints?.height || 10;
    
    this.cells = this.initializeCells();
    
    debugLogger.info('WFC', `Initialized WFC generator with ${this.width}x${this.height} grid`, {
      tileCount: Object.keys(tileset.tiles).length,
      seed,
    });
  }

  /**
   * Mulberry32 PRNG for deterministic randomness
   */
  private mulberry32(seed: number): () => number {
    return function() {
      let t = seed += 0x6D2B79F5;
      t = Math.imul(t ^ t >>> 15, t | 1);
      t ^= t + Math.imul(t ^ t >>> 7, t | 61);
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }

  /**
   * Initialize grid cells with all possible tiles
   */
  private initializeCells(): DungeonCell[][] {
    const cells: DungeonCell[][] = [];
    const allTileIds = Object.keys(this.tileset.tiles);

    for (let y = 0; y < this.height; y++) {
      const row: DungeonCell[] = [];
      for (let x = 0; x < this.width; x++) {
        row.push({
          x,
          y,
          tileId: null,
          possibleTiles: new Set(allTileIds),
          collapsed: false,
        });
      }
      cells.push(row);
    }

    return cells;
  }

  /**
   * Find cell with minimum entropy (fewest possible tiles)
   */
  private findMinEntropyCell(): DungeonCell | null {
    let minEntropy = Infinity;
    let candidates: DungeonCell[] = [];

    for (let y = 0; y < this.height; y++) {
      for (let x = 0; x < this.width; x++) {
        const cell = this.cells[y][x];
        if (!cell.collapsed) {
          const entropy = cell.possibleTiles.size;
          if (entropy < minEntropy) {
            minEntropy = entropy;
            candidates = [cell];
          } else if (entropy === minEntropy) {
            candidates.push(cell);
          }
        }
      }
    }

    if (candidates.length === 0) return null;

    // Randomly select from candidates with same entropy
    const index = Math.floor(this.rng() * candidates.length);
    return candidates[index];
  }

  /**
   * Collapse a cell by selecting one tile from possible tiles
   */
  private collapseCell(cell: DungeonCell): void {
    if (cell.possibleTiles.size === 0) {
      debugLogger.error('WFC', `Cell at (${cell.x}, ${cell.y}) has no possible tiles - contradiction detected`);
      throw new Error(`WFC contradiction at (${cell.x}, ${cell.y})`);
    }

    // Weight-based selection
    const tiles = Array.from(cell.possibleTiles);
    const weights = tiles.map(id => this.tileset.tiles[id].weight);
    const totalWeight = weights.reduce((sum, w) => sum + w, 0);
    
    let random = this.rng() * totalWeight;
    let selectedTile = tiles[0];
    
    for (let i = 0; i < tiles.length; i++) {
      random -= weights[i];
      if (random <= 0) {
        selectedTile = tiles[i];
        break;
      }
    }

    cell.tileId = selectedTile;
    cell.collapsed = true;
    cell.possibleTiles = new Set([selectedTile]);

    debugLogger.info('WFC', `Collapsed cell (${cell.x}, ${cell.y}) to tile: ${selectedTile}`);
  }

  /**
   * Propagate constraints to neighboring cells
   */
  private propagateConstraints(cell: DungeonCell): void {
    const queue: DungeonCell[] = [cell];
    const visited = new Set<string>();

    while (queue.length > 0) {
      const current = queue.shift()!;
      const key = `${current.x},${current.y}`;
      
      if (visited.has(key)) continue;
      visited.add(key);

      // Check all neighbors
      const neighbors = [
        { cell: this.getCell(current.x, current.y - 1), direction: 'north' as const },
        { cell: this.getCell(current.x, current.y + 1), direction: 'south' as const },
        { cell: this.getCell(current.x + 1, current.y), direction: 'east' as const },
        { cell: this.getCell(current.x - 1, current.y), direction: 'west' as const },
      ];

      for (const { cell: neighbor, direction } of neighbors) {
        if (!neighbor || neighbor.collapsed) continue;

        const sizeBefore = neighbor.possibleTiles.size;
        this.constrainNeighbor(current, neighbor, direction);
        
        if (neighbor.possibleTiles.size < sizeBefore && neighbor.possibleTiles.size > 0) {
          queue.push(neighbor);
        }
      }
    }
  }

  /**
   * Constrain neighbor based on adjacency rules
   */
  private constrainNeighbor(
    source: DungeonCell,
    neighbor: DungeonCell,
    direction: 'north' | 'south' | 'east' | 'west'
  ): void {
    const validTiles = new Set<string>();

    for (const sourceTileId of source.possibleTiles) {
      const adjacency = this.tileset.adjacency[sourceTileId];
      if (!adjacency) continue;

      const allowedNeighbors = adjacency[direction] || [];
      for (const neighborTileId of allowedNeighbors) {
        if (neighbor.possibleTiles.has(neighborTileId)) {
          validTiles.add(neighborTileId);
        }
      }
    }

    // Intersect with current possible tiles
    const newPossibleTiles = new Set<string>();
    for (const tileId of neighbor.possibleTiles) {
      if (validTiles.has(tileId)) {
        newPossibleTiles.add(tileId);
      }
    }

    neighbor.possibleTiles = newPossibleTiles;
  }

  /**
   * Get cell at position (returns null if out of bounds)
   */
  private getCell(x: number, y: number): DungeonCell | null {
    if (x < 0 || x >= this.width || y < 0 || y >= this.height) {
      return null;
    }
    return this.cells[y][x];
  }

  /**
   * Check if all cells are collapsed
   */
  private isComplete(): boolean {
    for (let y = 0; y < this.height; y++) {
      for (let x = 0; x < this.width; x++) {
        if (!this.cells[y][x].collapsed) {
          return false;
        }
      }
    }
    return true;
  }

  /**
   * Generate dungeon layout using WFC algorithm
   */
  public generate(): DungeonLayout {
    debugLogger.info('WFC', 'Starting WFC dungeon generation');
    this.iterations = 0;
    const maxIterations = this.width * this.height * 10;

    try {
      while (!this.isComplete() && this.iterations < maxIterations) {
        this.iterations++;

        // Find cell with minimum entropy
        const cell = this.findMinEntropyCell();
        if (!cell) break;

        // Collapse the cell
        this.collapseCell(cell);

        // Propagate constraints
        this.propagateConstraints(cell);

        if (this.iterations % 10 === 0) {
          const collapsedCount = this.cells.flat().filter(c => c.collapsed).length;
          debugLogger.info('WFC', `Progress: ${collapsedCount}/${this.width * this.height} cells collapsed`);
        }
      }

      if (!this.isComplete()) {
        debugLogger.warn('WFC', `Generation incomplete after ${this.iterations} iterations`);
      } else {
        debugLogger.success('WFC', `Generation complete in ${this.iterations} iterations`);
      }

      return this.buildLayout();
    } catch (error: any) {
      debugLogger.error('WFC', `Generation failed: ${error.message}`);
      throw error;
    }
  }

  /**
   * Build final dungeon layout from collapsed cells
   */
  private buildLayout(): DungeonLayout {
    const collapsedCells = this.cells.flat().filter(c => c.collapsed).length;
    const tiles = Object.values(this.tileset.tiles);

    return {
      width: this.width,
      height: this.height,
      cells: this.cells,
      tiles,
      metadata: {
        generatedAt: new Date().toISOString(),
        seed: this.seed,
        totalCells: this.width * this.height,
        collapsedCells,
        iterations: this.iterations,
      },
    };
  }
}

/**
 * Parse YAML tileset configuration
 */
export function parseTilesetYAML(yamlContent: string): TilesetConfig {
  debugLogger.info('WFC', 'Parsing tileset YAML configuration');

  try {
    // Simple YAML parser for tileset structure
    const lines = yamlContent.split('\n').filter(line => !line.trim().startsWith('#'));
    const result: any = { tiles: {}, adjacency: {} };
    
    let currentSection: 'tiles' | 'adjacency' | 'map_constraints' | null = null;
    let currentTileId: string | null = null;
    let currentAdjacencyId: string | null = null;

    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;

      // Section headers
      if (trimmed === 'tiles:') {
        currentSection = 'tiles';
        continue;
      } else if (trimmed === 'adjacency:') {
        currentSection = 'adjacency';
        continue;
      } else if (trimmed === 'map_constraints:') {
        currentSection = 'map_constraints';
        result.map_constraints = {};
        continue;
      }

      if (currentSection === 'tiles') {
        if (line.startsWith('  ') && !line.startsWith('    ')) {
          // Tile ID
          const match = trimmed.match(/^(\w+):/);
          if (match) {
            currentTileId = match[1];
            result.tiles[currentTileId] = { id: currentTileId };
          }
        } else if (line.startsWith('    ') && currentTileId) {
          // Tile property
          const [key, ...valueParts] = trimmed.split(':');
          const value = valueParts.join(':').trim();
          
          if (key === 'weight') {
            result.tiles[currentTileId].weight = parseFloat(value) || 1.0;
          } else if (key === 'type') {
            result.tiles[currentTileId].type = value.replace(/['"]/g, '');
          } else if (key === 'name') {
            result.tiles[currentTileId].name = value.replace(/['"]/g, '');
          } else if (key === 'model') {
            result.tiles[currentTileId].model = value.replace(/['"]/g, '');
          }
        }
      } else if (currentSection === 'adjacency') {
        if (line.startsWith('  ') && !line.startsWith('    ')) {
          // Adjacency tile ID
          const match = trimmed.match(/^(\w+):/);
          if (match) {
            currentAdjacencyId = match[1];
            result.adjacency[currentAdjacencyId] = {};
          }
        } else if (line.startsWith('    ') && currentAdjacencyId) {
          // Direction
          const [direction, ...valueParts] = trimmed.split(':');
          const value = valueParts.join(':').trim();
          
          if (value.startsWith('[') && value.endsWith(']')) {
            const tiles = value.slice(1, -1).split(',').map(t => t.trim().replace(/['"]/g, ''));
            result.adjacency[currentAdjacencyId][direction] = tiles;
          }
        }
      } else if (currentSection === 'map_constraints') {
        const [key, ...valueParts] = trimmed.split(':');
        const value = valueParts.join(':').trim();
        
        if (key === 'width' || key === 'height') {
          result.map_constraints[key] = parseInt(value) || 10;
        } else if (key === 'minRooms' || key === 'maxRooms') {
          result.map_constraints[key] = parseInt(value);
        }
      }
    }

    // Validate required fields
    if (Object.keys(result.tiles).length === 0) {
      throw new Error('No tiles defined in tileset configuration');
    }

    if (Object.keys(result.adjacency).length === 0) {
      throw new Error('No adjacency rules defined in tileset configuration');
    }

    debugLogger.success('WFC', `Tileset parsed: ${Object.keys(result.tiles).length} tiles, ${Object.keys(result.adjacency).length} adjacency rules`);
    return result as TilesetConfig;
  } catch (error: any) {
    debugLogger.error('WFC', `Tileset parsing failed: ${error.message}`);
    throw new Error(`Tileset parsing error: ${error.message}`);
  }
}
