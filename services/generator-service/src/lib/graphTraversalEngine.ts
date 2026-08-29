import { generateDeterministicId, mulberry32 } from "./seededRng.js";

export interface GraphNode {
  id: string;
  type: "sequence" | "choice" | "emit";
  weight?: number;
  emit?: Record<string, unknown>;
  next?: string | string[];
}

export interface GraphDefinition {
  entry: string;
  nodes: Record<string, GraphNode>;
}

export interface TraversalEvent {
  stepIndex: number;
  nodeId: string;
  componentName: string;
  data: Record<string, unknown>;
}

export interface TraversalResult {
  events: TraversalEvent[];
  entityId: string;
  traversalPath: string[];
}

export class GraphTraversalEngine {
  private readonly rng: () => number;
  private readonly traversalPath: string[] = [];
  private readonly events: TraversalEvent[] = [];
  private stepIndex = 0;

  constructor(
    private readonly graph: GraphDefinition,
    private readonly seed: number,
    private readonly maxDepth = 256,
  ) {
    this.rng = mulberry32(seed);
  }

  traverse(): TraversalResult {
    let currentNodeId: string | null = this.graph.entry;
    let iterations = 0;

    while (currentNodeId && iterations < this.maxDepth) {
      iterations++;
      this.stepIndex++;
      const node = this.graph.nodes[currentNodeId];
      if (!node) {
        break;
      }

      this.traversalPath.push(currentNodeId);
      if (node.type === "emit" && node.emit) {
        for (const [componentName, data] of Object.entries(node.emit)) {
          this.events.push({
            stepIndex: this.stepIndex,
            nodeId: currentNodeId,
            componentName,
            data: (data as Record<string, unknown>) ?? {},
          });
        }
      }

      currentNodeId = this.getNextNode(node);
    }

    return {
      entityId: generateDeterministicId("entity", this.seed, this.events.length),
      events: this.events,
      traversalPath: this.traversalPath,
    };
  }

  private getNextNode(node: GraphNode): string | null {
    if (!node.next) {
      return null;
    }

    if (typeof node.next === "string") {
      return node.next;
    }

    if (node.next.length === 0) {
      return null;
    }

    const weighted = node.next
      .map((nodeId) => ({ nodeId, weight: this.graph.nodes[nodeId]?.weight ?? 1 }))
      .filter((entry) => entry.weight > 0);

    const totalWeight = weighted.reduce((sum, entry) => sum + entry.weight, 0);
    let roll = this.rng() * totalWeight;
    for (const entry of weighted) {
      roll -= entry.weight;
      if (roll <= 0) {
        return entry.nodeId;
      }
    }

    return weighted[weighted.length - 1]?.nodeId ?? null;
  }
}
