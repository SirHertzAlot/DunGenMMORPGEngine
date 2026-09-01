/**
 * Deterministic Graph Traversal Engine
 * 
 * A reusable module for deterministic graph-based procedural generation.
 * Implements deterministic RNG via mulberry32 for consistent reproduction.
 * Supports weighted random selection (~50% randomness) for branching decisions.
 * Generates ECS-compatible component events and resolved entity objects.
 */

import { debugLogger } from './debugLogger';

// ============================================================================
// Types
// ============================================================================

export interface GraphNode {
  id: string;
  type: 'sequence' | 'choice' | 'emit';
  weight?: number;
  emit?: Record<string, any>;
  next?: string | string[];
}

export interface GraphDefinition {
  entry: string;
  nodes: Record<string, GraphNode>;
}

export interface ComponentEvent {
  timestamp: number;
  nodeId: string;
  componentName: string;
  data: Record<string, any>;
}

export interface ResolvedEntity {
  id: string;
  generatedAt: string;
  seed: number;
  components: Record<string, any>;
  metadata: {
    traversalPath: string[];
    totalNodes: number;
    emittedEvents: number;
  };
}

export interface TraversalResult {
  events: ComponentEvent[];
  entity: ResolvedEntity;
  logs: string[];
}

// ============================================================================
// Deterministic RNG - mulberry32
// ============================================================================

/**
 * mulberry32 - A simple, fast, and high-quality 32-bit PRNG
 * Returns a deterministic pseudo-random number generator function
 * @param seed - Initial seed value for deterministic generation
 * @returns Function that returns pseudo-random numbers in range [0, 1)
 */
export function mulberry32(seed: number): () => number {
  return function() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

// ============================================================================
// Graph Traversal Engine
// ============================================================================

export class GraphTraversalEngine {
  private graph: GraphDefinition;
  private seed: number;
  private rng: () => number;
  private logs: string[] = [];
  private events: ComponentEvent[] = [];
  private traversalPath: string[] = [];
  private randomnessFactor: number;

  constructor(graph: GraphDefinition, seed: number, randomnessFactor: number = 0.5) {
    this.graph = graph;
    this.seed = seed;
    this.rng = mulberry32(seed);
    this.randomnessFactor = Math.max(0, Math.min(1, randomnessFactor)); // Clamp to [0, 1]
    
    this.log('info', `Initialized GraphTraversalEngine with seed: ${seed}, randomness: ${(randomnessFactor * 100).toFixed(0)}%`);
  }

  /**
   * Execute graph traversal from entry point
   * @returns TraversalResult containing events, resolved entity, and logs
   */
  public traverse(): TraversalResult {
    this.log('info', `Starting traversal from entry node: ${this.graph.entry}`);
    
    const startTime = Date.now();
    let currentNodeId: string | null = this.graph.entry;
    let iterationCount = 0;
    const maxIterations = 1000; // Safety limit

    while (currentNodeId && iterationCount < maxIterations) {
      iterationCount++;
      
      const node = this.graph.nodes[currentNodeId];
      if (!node) {
        this.log('error', `Node not found: ${currentNodeId}`);
        break;
      }

      this.log('info', `[${iterationCount}] Entering node: ${currentNodeId} (type: ${node.type})`);
      this.traversalPath.push(currentNodeId);

      // Process node based on type
      switch (node.type) {
        case 'emit':
          this.processEmitNode(currentNodeId, node);
          currentNodeId = this.getNextNode(node);
          break;
        
        case 'sequence':
          currentNodeId = this.getNextNode(node);
          break;
        
        case 'choice':
          currentNodeId = this.processChoiceNode(currentNodeId, node);
          break;
        
        default:
          this.log('warn', `Unknown node type: ${node.type}`);
          currentNodeId = null;
      }
    }

    if (iterationCount >= maxIterations) {
      this.log('error', `Max iterations (${maxIterations}) reached. Possible infinite loop.`);
    }

    const duration = Date.now() - startTime;
    this.log('success', `Traversal complete in ${duration}ms. Visited ${this.traversalPath.length} nodes, emitted ${this.events.length} events.`);

    // Build resolved entity
    const entity = this.buildResolvedEntity();

    return {
      events: this.events,
      entity,
      logs: this.logs,
    };
  }

  /**
   * Process an emit node - accumulate component data
   */
  private processEmitNode(nodeId: string, node: GraphNode): void {
    if (!node.emit) {
      this.log('warn', `Emit node ${nodeId} has no emit data`);
      return;
    }

    for (const [componentName, data] of Object.entries(node.emit)) {
      const event: ComponentEvent = {
        timestamp: Date.now(),
        nodeId,
        componentName,
        data,
      };
      
      this.events.push(event);
      this.log('info', `  → Emitted component: ${componentName}`, { data });
      
      debugLogger.info(
        'GraphTraversal',
        `Emitted ${componentName} from node ${nodeId}`,
        { componentName, data, nodeId },
        'offline'
      );
    }
  }

  /**
   * Process a choice node - weighted random selection
   */
  private processChoiceNode(nodeId: string, node: GraphNode): string | null {
    if (!node.next || !Array.isArray(node.next)) {
      this.log('error', `Choice node ${nodeId} has invalid next array`);
      return null;
    }

    const choices = node.next;
    if (choices.length === 0) {
      this.log('warn', `Choice node ${nodeId} has no choices`);
      return null;
    }

    // Get RNG value
    const rngValue = this.rng();
    this.log('info', `  → RNG value: ${rngValue.toFixed(4)}`);

    // Determine if we use weighted or pure random selection
    const useWeighted = rngValue >= this.randomnessFactor;
    
    let selectedNode: string;

    if (useWeighted && node.weight !== undefined) {
      // Weighted selection based on node weights
      selectedNode = this.weightedSelection(choices, rngValue);
      this.log('info', `  → Weighted selection: ${selectedNode}`);
    } else {
      // Pure random selection
      const randomIndex = Math.floor(this.rng() * choices.length);
      selectedNode = choices[randomIndex];
      this.log('info', `  → Random selection: ${selectedNode} (index: ${randomIndex})`);
    }

    debugLogger.info(
      'GraphTraversal',
      `Choice node ${nodeId} selected: ${selectedNode}`,
      { 
        nodeId, 
        rngValue, 
        useWeighted, 
        selectedNode,
        totalChoices: choices.length 
      },
      'offline'
    );

    return selectedNode;
  }

  /**
   * Weighted selection from array of node IDs
   * Assumes nodes have weight property
   */
  private weightedSelection(nodeIds: string[], rngValue: number): string {
    // Calculate total weight
    let totalWeight = 0;
    const weights: number[] = [];
    
    for (const id of nodeIds) {
      const node = this.graph.nodes[id];
      const weight = node?.weight ?? 1.0;
      weights.push(weight);
      totalWeight += weight;
    }

    this.log('info', `  → Total weight: ${totalWeight.toFixed(2)}, weights: [${weights.map(w => w.toFixed(2)).join(', ')}]`);

    // Select based on cumulative weight
    let cumulativeWeight = 0;
    const threshold = rngValue * totalWeight;

    for (let i = 0; i < nodeIds.length; i++) {
      cumulativeWeight += weights[i];
      if (threshold <= cumulativeWeight) {
        this.log('info', `  → Selected ${nodeIds[i]} (cumulative: ${cumulativeWeight.toFixed(2)}, threshold: ${threshold.toFixed(2)})`);
        return nodeIds[i];
      }
    }

    // Fallback to last node
    return nodeIds[nodeIds.length - 1];
  }

  /**
   * Get next node from current node
   */
  private getNextNode(node: GraphNode): string | null {
    if (!node.next) {
      return null;
    }

    if (typeof node.next === 'string') {
      return node.next;
    }

    if (Array.isArray(node.next) && node.next.length > 0) {
      return node.next[0];
    }

    return null;
  }

  /**
   * Build resolved entity from accumulated events
   */
  private buildResolvedEntity(): ResolvedEntity {
    const components: Record<string, any> = {};

    // Merge all emitted component data
    for (const event of this.events) {
      if (!components[event.componentName]) {
        components[event.componentName] = {};
      }
      
      // Merge data (later events override earlier ones)
      Object.assign(components[event.componentName], event.data);
    }

    const entity: ResolvedEntity = {
      id: `entity_${this.seed}_${Date.now()}`,
      generatedAt: new Date().toISOString(),
      seed: this.seed,
      components,
      metadata: {
        traversalPath: this.traversalPath,
        totalNodes: this.traversalPath.length,
        emittedEvents: this.events.length,
      },
    };

    this.log('success', `Built resolved entity: ${entity.id} with ${Object.keys(components).length} components`);

    return entity;
  }

  /**
   * Internal logging
   */
  private log(level: 'info' | 'warn' | 'error' | 'success', message: string, details?: Record<string, any>): void {
    const timestamp = new Date().toISOString();
    const logMessage = `[${timestamp}] ${message}`;
    this.logs.push(logMessage);

    // Also log to debug logger
    debugLogger[level](
      'GraphTraversal',
      message,
      details,
      'offline'
    );
  }

  /**
   * Get current logs
   */
  public getLogs(): string[] {
    return [...this.logs];
  }

  /**
   * Get current events
   */
  public getEvents(): ComponentEvent[] {
    return [...this.events];
  }
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Validate graph definition
 */
export function validateGraph(graph: GraphDefinition): { valid: boolean; errors: string[] } {
  const errors: string[] = [];

  // Check entry node exists
  if (!graph.nodes[graph.entry]) {
    errors.push(`Entry node '${graph.entry}' not found in graph`);
  }

  // Check all nodes
  for (const [nodeId, node] of Object.entries(graph.nodes)) {
    // Validate node type
    if (!['sequence', 'choice', 'emit'].includes(node.type)) {
      errors.push(`Node '${nodeId}' has invalid type: ${node.type}`);
    }

    // Validate next references
    if (node.next) {
      const nextNodes = Array.isArray(node.next) ? node.next : [node.next];
      for (const nextId of nextNodes) {
        if (!graph.nodes[nextId]) {
          errors.push(`Node '${nodeId}' references non-existent node: ${nextId}`);
        }
      }
    }

    // Validate choice nodes have multiple options
    if (node.type === 'choice' && (!node.next || !Array.isArray(node.next) || node.next.length < 2)) {
      errors.push(`Choice node '${nodeId}' must have at least 2 options in next array`);
    }

    // Validate emit nodes have emit data
    if (node.type === 'emit' && !node.emit) {
      errors.push(`Emit node '${nodeId}' must have emit data`);
    }
  }

  return {
    valid: errors.length === 0,
    errors,
  };
}

/**
 * Create a simple example graph for testing
 */
export function createExampleGraph(): GraphDefinition {
  return {
    entry: 'start',
    nodes: {
      start: {
        id: 'start',
        type: 'sequence',
        next: 'choose_class',
      },
      choose_class: {
        id: 'choose_class',
        type: 'choice',
        weight: 1.0,
        next: ['warrior', 'mage', 'rogue'],
      },
      warrior: {
        id: 'warrior',
        type: 'emit',
        emit: {
          Health: { current: 100, max: 100 },
          Attack: { damage: 15, range: 2 },
          Class: { name: 'Warrior', archetype: 'melee' },
        },
        next: 'add_loot',
      },
      mage: {
        id: 'mage',
        type: 'emit',
        emit: {
          Health: { current: 60, max: 60 },
          Attack: { damage: 25, range: 10 },
          Class: { name: 'Mage', archetype: 'ranged' },
        },
        next: 'add_loot',
      },
      rogue: {
        id: 'rogue',
        type: 'emit',
        emit: {
          Health: { current: 80, max: 80 },
          Attack: { damage: 20, range: 3 },
          Class: { name: 'Rogue', archetype: 'stealth' },
        },
        next: 'add_loot',
      },
      add_loot: {
        id: 'add_loot',
        type: 'emit',
        emit: {
          LootTable: {
            items: [
              { id: 'gold', dropChance: 0.8 },
              { id: 'potion', dropChance: 0.5 },
            ],
          },
        },
      },
    },
  };
}
