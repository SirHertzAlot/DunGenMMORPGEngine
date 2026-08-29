/**
 * Core Graph Traversal Engine (UI-agnostic)
 *
 * Fully deterministic graph-based procedural generation engine.
 * All IDs, timestamps, and event ordering are derived from seed and counters.
 * No wall-clock time dependencies in deterministic mode.
 */

import { generateDeterministicId, mulberry32 } from "../utils/seededRng";

// ============================================================================
// Types
// ============================================================================

export interface GraphNode {
  id: string;
  type: "sequence" | "choice" | "emit";
  weight?: number;
  emit?: Record<string, any>;
  next?: string | string[];
}

export interface GraphDefinition {
  entry: string;
  nodes: Record<string, GraphNode>;
}

export interface ComponentEvent {
  timestamp: number; // Deterministic tick-based timestamp
  stepIndex: number; // Deterministic step counter
  nodeId: string;
  componentName: string;
  data: Record<string, any>;
}

export interface ResolvedEntity {
  id: string;
  generatedAt: number; // Deterministic tick-based timestamp
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

export interface TraversalConfig {
  maxDepth?: number; // Max nodes to visit
  maxEmits?: number; // Max events to emit
  randomnessFactor?: number; // Probability of uniform override (0-1)
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
  private stepIndex = 0;
  private eventIndex = 0;
  private config: Required<TraversalConfig>;

  constructor(
    graph: GraphDefinition,
    seed: number,
    config: TraversalConfig = {},
  ) {
    this.graph = graph;
    this.seed = seed;
    this.rng = mulberry32(seed);
    this.randomnessFactor = Math.max(
      0,
      Math.min(1, config.randomnessFactor ?? 0.5),
    );
    this.config = {
      maxDepth: config.maxDepth ?? 1000,
      maxEmits: config.maxEmits ?? 500,
      randomnessFactor: this.randomnessFactor,
    };

    this.log(
      "info",
      `Initialized GraphTraversalEngine with seed: ${seed}, randomness: ${(this.randomnessFactor * 100).toFixed(0)}%, maxDepth: ${this.config.maxDepth}, maxEmits: ${this.config.maxEmits}`,
    );
  }

  /**
   * Execute graph traversal from entry point
   * @returns TraversalResult containing events, resolved entity, and logs
   */
  public traverse(): TraversalResult {
    this.log("info", `Starting traversal from entry node: ${this.graph.entry}`);

    let currentNodeId: string | null = this.graph.entry;
    let iterationCount = 0;

    while (currentNodeId && iterationCount < this.config.maxDepth) {
      iterationCount++;
      this.stepIndex++;

      const node = this.graph.nodes[currentNodeId];
      if (!node) {
        this.log("error", `Node not found: ${currentNodeId}`);
        break;
      }

      this.log(
        "info",
        `[${iterationCount}] Entering node: ${currentNodeId} (type: ${node.type})`,
      );
      this.traversalPath.push(currentNodeId);

      // Check emit budget
      if (this.events.length >= this.config.maxEmits) {
        this.log(
          "warn",
          `Max emits budget (${this.config.maxEmits}) exceeded. Stopping traversal.`,
        );
        break;
      }

      // Process node based on type
      switch (node.type) {
        case "emit":
          this.processEmitNode(currentNodeId, node);
          currentNodeId = this.getNextNode(node);
          break;

        case "sequence":
          currentNodeId = this.getNextNode(node);
          break;

        case "choice":
          currentNodeId = this.processChoiceNode(currentNodeId, node);
          break;

        default:
          this.log("warn", `Unknown node type: ${node.type}`);
          currentNodeId = null;
      }
    }

    if (iterationCount >= this.config.maxDepth) {
      this.log(
        "error",
        `Max depth budget (${this.config.maxDepth}) exceeded. Possible infinite loop or complex graph.`,
      );
    }

    this.log(
      "success",
      `Traversal complete. Visited ${this.traversalPath.length} nodes, emitted ${this.events.length} events.`,
    );

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
      this.log("warn", `Emit node ${nodeId} has no emit data`);
      return;
    }

    for (const [componentName, data] of Object.entries(node.emit)) {
      this.eventIndex++;

      const event: ComponentEvent = {
        timestamp: this.stepIndex, // Deterministic tick-based timestamp
        stepIndex: this.stepIndex,
        nodeId,
        componentName,
        data,
      };

      this.events.push(event);
      this.log(
        "info",
        `  → Emitted component: ${componentName} at step ${this.stepIndex}`,
      );
    }
  }

  /**
   * Process a choice node - weighted random selection with corrected logic
   */
  private processChoiceNode(nodeId: string, node: GraphNode): string | null {
    if (!node.next || !Array.isArray(node.next)) {
      this.log("error", `Choice node ${nodeId} has invalid next array`);
      return null;
    }

    const choices = node.next;
    if (choices.length === 0) {
      this.log("warn", `Choice node ${nodeId} has no choices`);
      return null;
    }

    // Check if any child has weights
    const hasWeights = choices.some((id) => {
      const childNode = this.graph.nodes[id];
      return (
        childNode && childNode.weight !== undefined && childNode.weight !== 1.0
      );
    });

    // Draw 1: Decide if we override with uniform random
    const overrideRoll = this.rng();
    const shouldOverride = overrideRoll < this.randomnessFactor;

    let selectedNode: string;

    if (shouldOverride || !hasWeights) {
      // Uniform random selection
      const randomRoll = this.rng(); // Draw 2: Selection
      const randomIndex = Math.floor(randomRoll * choices.length);
      selectedNode = choices[randomIndex];
      this.log(
        "info",
        `  → Uniform selection: ${selectedNode} (index: ${randomIndex}, roll: ${randomRoll.toFixed(4)})`,
      );
    } else {
      // Weighted selection based on child node weights
      const selectionRoll = this.rng(); // Draw 2: Selection threshold
      selectedNode = this.weightedSelection(choices, selectionRoll);
      this.log(
        "info",
        `  → Weighted selection: ${selectedNode} (roll: ${selectionRoll.toFixed(4)})`,
      );
    }

    return selectedNode;
  }

  /**
   * Weighted selection from array of node IDs using child node weights
   */
  private weightedSelection(nodeIds: string[], rngValue: number): string {
    // Calculate total weight from child nodes
    let totalWeight = 0;
    const weights: number[] = [];

    for (const id of nodeIds) {
      const childNode = this.graph.nodes[id];
      const weight = childNode?.weight ?? 1.0;
      weights.push(weight);
      totalWeight += weight;
    }

    this.log(
      "info",
      `  → Total weight: ${totalWeight.toFixed(2)}, weights: [${weights.map((w) => w.toFixed(2)).join(", ")}]`,
    );

    // Select based on cumulative weight
    let cumulativeWeight = 0;
    const threshold = rngValue * totalWeight;

    for (let i = 0; i < nodeIds.length; i++) {
      cumulativeWeight += weights[i];
      if (threshold <= cumulativeWeight) {
        this.log(
          "info",
          `  → Selected ${nodeIds[i]} (cumulative: ${cumulativeWeight.toFixed(2)}, threshold: ${threshold.toFixed(2)})`,
        );
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

    if (typeof node.next === "string") {
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
      id: generateDeterministicId(this.seed, this.stepIndex, "entity"),
      generatedAt: this.stepIndex, // Deterministic tick-based timestamp
      seed: this.seed,
      components,
      metadata: {
        traversalPath: this.traversalPath,
        totalNodes: this.traversalPath.length,
        emittedEvents: this.events.length,
      },
    };

    this.log(
      "success",
      `Built resolved entity: ${entity.id} with ${Object.keys(components).length} components`,
    );

    return entity;
  }

  /**
   * Internal logging with deterministic ordering
   */
  private log(
    _level: "info" | "warn" | "error" | "success",
    message: string,
  ): void {
    const logMessage = `[step:${this.stepIndex}] ${message}`;
    this.logs.push(logMessage);
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
export function validateGraph(graph: GraphDefinition): {
  valid: boolean;
  errors: string[];
} {
  const errors: string[] = [];

  // Check entry node exists
  if (!graph.nodes[graph.entry]) {
    errors.push(`Entry node '${graph.entry}' not found in graph`);
  }

  // Check all nodes
  for (const [nodeId, node] of Object.entries(graph.nodes)) {
    // Validate node type
    if (!["sequence", "choice", "emit"].includes(node.type)) {
      errors.push(`Node '${nodeId}' has invalid type: ${node.type}`);
    }

    // Validate next references
    if (node.next) {
      const nextNodes = Array.isArray(node.next) ? node.next : [node.next];
      for (const nextId of nextNodes) {
        if (!graph.nodes[nextId]) {
          errors.push(
            `Node '${nodeId}' references non-existent node: ${nextId}`,
          );
        }
      }
    }

    // Validate choice nodes have multiple options
    if (
      node.type === "choice" &&
      (!node.next || !Array.isArray(node.next) || node.next.length < 2)
    ) {
      errors.push(
        `Choice node '${nodeId}' must have at least 2 options in next array`,
      );
    }

    // Validate emit nodes have emit data
    if (node.type === "emit" && !node.emit) {
      errors.push(`Emit node '${nodeId}' must have emit data`);
    }
  }

  return {
    valid: errors.length === 0,
    errors,
  };
}
