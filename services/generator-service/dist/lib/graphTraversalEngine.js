import { generateDeterministicId, mulberry32 } from "./seededRng.js";
export class GraphTraversalEngine {
    graph;
    seed;
    maxDepth;
    rng;
    traversalPath = [];
    events = [];
    stepIndex = 0;
    constructor(graph, seed, maxDepth = 256) {
        this.graph = graph;
        this.seed = seed;
        this.maxDepth = maxDepth;
        this.rng = mulberry32(seed);
    }
    traverse() {
        let currentNodeId = this.graph.entry;
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
                        data: data ?? {},
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
    getNextNode(node) {
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
