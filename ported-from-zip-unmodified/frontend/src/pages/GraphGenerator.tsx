import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { 
  Network, 
  Play, 
  Square, 
  RotateCcw, 
  Copy, 
  Check, 
  AlertCircle,
  Info,
  ChevronDown,
  ChevronUp
} from 'lucide-react';
import { debugLogger } from '../lib/debugLogger';

interface GraphNode {
  id: string;
  label: string;
  value: number;
}

interface GraphEdge {
  from: string;
  to: string;
  weight: number;
}

interface GraphOutput {
  nodes: GraphNode[];
  edges: GraphEdge[];
  traversalPath: string[];
  metadata: {
    generatedAt: string;
    totalNodes: number;
    totalEdges: number;
  };
}

export default function GraphGenerator() {
  const [isGenerating, setIsGenerating] = useState(false);
  const [generationProgress, setGenerationProgress] = useState(0);
  const [traversalLog, setTraversalLog] = useState<string[]>([]);
  const [graphOutput, setGraphOutput] = useState<GraphOutput | null>(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isOutputExpanded, setIsOutputExpanded] = useState(true);

  const generateGraph = (): GraphOutput => {
    debugLogger.info('GraphGenerator', 'Generating graph structure', undefined, 'offline');
    
    const nodeCount = 5 + Math.floor(Math.random() * 6); // 5-10 nodes
    const nodes: GraphNode[] = [];
    const edges: GraphEdge[] = [];
    const traversalPath: string[] = [];

    // Generate nodes
    for (let i = 0; i < nodeCount; i++) {
      nodes.push({
        id: `node-${i}`,
        label: `Node ${i + 1}`,
        value: Math.floor(Math.random() * 100),
      });
    }

    debugLogger.info('GraphGenerator', `Generated ${nodeCount} nodes`, { nodeCount }, 'offline');

    // Generate edges (ensure connectivity)
    for (let i = 0; i < nodeCount - 1; i++) {
      edges.push({
        from: `node-${i}`,
        to: `node-${i + 1}`,
        weight: Math.random(),
      });
    }

    // Add some random edges
    const extraEdges = Math.floor(Math.random() * nodeCount);
    for (let i = 0; i < extraEdges; i++) {
      const from = Math.floor(Math.random() * nodeCount);
      const to = Math.floor(Math.random() * nodeCount);
      if (from !== to) {
        edges.push({
          from: `node-${from}`,
          to: `node-${to}`,
          weight: Math.random(),
        });
      }
    }

    debugLogger.info('GraphGenerator', `Generated ${edges.length} edges`, { edgeCount: edges.length }, 'offline');

    // Simulate traversal
    const visited = new Set<string>();
    let current = 'node-0';
    visited.add(current);
    traversalPath.push(current);

    while (visited.size < nodeCount) {
      const possibleEdges = edges.filter(e => e.from === current && !visited.has(e.to));
      if (possibleEdges.length === 0) {
        // Find any unvisited node
        const unvisited = nodes.find(n => !visited.has(n.id));
        if (unvisited) {
          current = unvisited.id;
          visited.add(current);
          traversalPath.push(current);
        } else {
          break;
        }
      } else {
        const nextEdge = possibleEdges[Math.floor(Math.random() * possibleEdges.length)];
        current = nextEdge.to;
        visited.add(current);
        traversalPath.push(current);
      }
    }

    debugLogger.info('GraphGenerator', `Traversal complete: ${traversalPath.length} nodes visited`, { 
      pathLength: traversalPath.length,
      path: traversalPath.join(' → ')
    }, 'offline');

    const output: GraphOutput = {
      nodes,
      edges,
      traversalPath,
      metadata: {
        generatedAt: new Date().toISOString(),
        totalNodes: nodes.length,
        totalEdges: edges.length,
      },
    };

    debugLogger.success('GraphGenerator', 'Graph output created successfully', {
      nodes: output.nodes.length,
      edges: output.edges.length,
      traversalPathLength: output.traversalPath.length
    }, 'offline');

    return output;
  };

  const handleStartGeneration = async () => {
    setIsGenerating(true);
    setGenerationProgress(0);
    setTraversalLog([]);
    setError(null);
    setGraphOutput(null); // Clear previous output
    
    debugLogger.info('GraphGenerator', 'Starting graph generation', undefined, 'offline');

    try {
      setTraversalLog(prev => [...prev, 'Initializing graph generation...']);
      await new Promise(resolve => setTimeout(resolve, 300));
      setGenerationProgress(20);

      setTraversalLog(prev => [...prev, 'Creating graph nodes...']);
      debugLogger.info('GraphGenerator', 'Creating graph nodes', undefined, 'offline');
      await new Promise(resolve => setTimeout(resolve, 400));
      setGenerationProgress(40);

      setTraversalLog(prev => [...prev, 'Generating edges and connections...']);
      debugLogger.info('GraphGenerator', 'Generating edges', undefined, 'offline');
      await new Promise(resolve => setTimeout(resolve, 400));
      setGenerationProgress(60);

      setTraversalLog(prev => [...prev, 'Performing graph traversal...']);
      debugLogger.info('GraphGenerator', 'Performing traversal', undefined, 'offline');
      await new Promise(resolve => setTimeout(resolve, 500));
      setGenerationProgress(80);

      const output = generateGraph();

      setTraversalLog(prev => [...prev, `Generated ${output.nodes.length} nodes and ${output.edges.length} edges`]);
      setTraversalLog(prev => [...prev, `Traversal path: ${output.traversalPath.join(' → ')}`]);
      
      await new Promise(resolve => setTimeout(resolve, 300));
      setGenerationProgress(100);

      // Store output in React state with explicit logging
      debugLogger.info('GraphGenerator', 'Storing graph output in React state', {
        outputSize: JSON.stringify(output).length,
        hasNodes: output.nodes.length > 0,
        hasEdges: output.edges.length > 0,
        hasTraversalPath: output.traversalPath.length > 0
      }, 'offline');
      
      setGraphOutput(output);
      
      // Verify state update
      debugLogger.success('GraphGenerator', 'Graph output stored successfully - rendering JSON display', {
        stateUpdated: true,
        outputId: output.metadata.generatedAt
      }, 'offline');
      
      setTraversalLog(prev => [...prev, 'Generation complete! JSON output ready.']);
      setIsOutputExpanded(true); // Ensure output section is expanded
      
    } catch (err: any) {
      const errorMsg = err.message || 'Unknown error during generation';
      setError(errorMsg);
      setTraversalLog(prev => [...prev, `ERROR: ${errorMsg}`]);
      debugLogger.error('GraphGenerator', `Generation failed: ${errorMsg}`, { error: err }, 'offline');
    } finally {
      setIsGenerating(false);
      setGenerationProgress(0);
    }
  };

  const handleStopGeneration = () => {
    setIsGenerating(false);
    setGenerationProgress(0);
    setTraversalLog(prev => [...prev, 'Generation stopped by user']);
    debugLogger.warn('GraphGenerator', 'Generation stopped by user', undefined, 'offline');
  };

  const handleReset = () => {
    setGraphOutput(null);
    setError(null);
    setGenerationProgress(0);
    setTraversalLog([]);
    setIsOutputExpanded(true);
    debugLogger.info('GraphGenerator', 'Reset to initial state', undefined, 'offline');
  };

  const handleCopyOutput = () => {
    if (graphOutput) {
      const jsonString = JSON.stringify(graphOutput, null, 2);
      navigator.clipboard.writeText(jsonString);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
      debugLogger.success('GraphGenerator', 'Output copied to clipboard', { 
        size: jsonString.length 
      }, 'offline');
    }
  };

  // Log when output is rendered
  if (graphOutput) {
    debugLogger.info('GraphGenerator', 'Rendering JSON output display', {
      hasOutput: true,
      nodeCount: graphOutput.nodes.length,
      edgeCount: graphOutput.edges.length
    }, 'offline');
  }

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border bg-card px-4 py-4 sm:px-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
              <Network className="h-5 w-5 text-primary-foreground" />
            </div>
            <div>
              <h1 className="text-xl font-bold tracking-tight sm:text-2xl">Graph Generator</h1>
              <p className="text-xs text-muted-foreground sm:text-sm">
                Generate and visualize graph structures with traversal
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
          Graph Generator is running in offline mode with client-side processing.
        </AlertDescription>
      </Alert>

      {/* Main Content */}
      <div className="flex flex-1 flex-col gap-4 overflow-hidden p-4 sm:p-6 lg:flex-row">
        {/* Left Panel - Controls */}
        <div className="flex w-full flex-col gap-4 lg:w-96">
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

              {/* Progress */}
              {isGenerating && (
                <div className="space-y-2">
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">Processing...</span>
                    <span className="font-medium">{generationProgress}%</span>
                  </div>
                  <div className="h-2 w-full overflow-hidden rounded-full bg-secondary">
                    <div
                      className="h-full bg-primary transition-all duration-300"
                      style={{ width: `${generationProgress}%` }}
                    />
                  </div>
                </div>
              )}

              <Separator />

              {/* Generation Info */}
              <div className="space-y-2">
                <h3 className="text-sm font-semibold">Generation Settings</h3>
                <div className="space-y-1 text-xs text-muted-foreground">
                  <div className="flex justify-between">
                    <span>Node Range:</span>
                    <span className="font-medium">5-10 nodes</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Edge Generation:</span>
                    <span className="font-medium">Random</span>
                  </div>
                  {graphOutput && (
                    <>
                      <div className="flex justify-between">
                        <span>Nodes Generated:</span>
                        <span className="font-medium text-primary">{graphOutput.nodes.length}</span>
                      </div>
                      <div className="flex justify-between">
                        <span>Edges Generated:</span>
                        <span className="font-medium text-primary">{graphOutput.edges.length}</span>
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
          {traversalLog.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Generation Log</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <ScrollArea className="h-48">
                  <div className="space-y-1 px-4 pb-4">
                    {traversalLog.map((log, idx) => (
                      <div key={idx} className="text-xs font-mono text-muted-foreground">
                        <span className="text-primary">[{idx + 1}]</span> {log}
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
                <CardTitle className="text-lg">Graph Output</CardTitle>
                <CardDescription>Generated graph structure and traversal</CardDescription>
              </div>
              {graphOutput && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={handleCopyOutput}
                  className="gap-2"
                >
                  {copied ? (
                    <>
                      <Check className="h-4 w-4" />
                      Copied
                    </>
                  ) : (
                    <>
                      <Copy className="h-4 w-4" />
                      Copy JSON
                    </>
                  )}
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent className="flex-1 overflow-hidden p-0">
            {graphOutput ? (
              <Tabs defaultValue="formatted" className="flex h-full flex-col">
                <TabsList className="mx-4 mt-2 w-auto">
                  <TabsTrigger value="formatted">Formatted</TabsTrigger>
                  <TabsTrigger value="raw">Raw JSON</TabsTrigger>
                </TabsList>
                <TabsContent value="formatted" className="flex-1 overflow-hidden px-4 pb-4">
                  <ScrollArea className="h-full rounded-lg border border-border bg-muted/30 p-4">
                    <div className="space-y-4">
                      {/* Metadata */}
                      <Collapsible open={isOutputExpanded} onOpenChange={setIsOutputExpanded}>
                        <CollapsibleTrigger className="flex w-full items-center justify-between rounded-lg border border-border bg-card p-3 hover:bg-accent/50 transition-colors">
                          <h3 className="text-sm font-semibold">Metadata</h3>
                          {isOutputExpanded ? (
                            <ChevronUp className="h-4 w-4" />
                          ) : (
                            <ChevronDown className="h-4 w-4" />
                          )}
                        </CollapsibleTrigger>
                        <CollapsibleContent className="mt-2">
                          <div className="space-y-1 text-xs rounded-lg border border-border bg-card p-3">
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Generated At:</span>
                              <span className="font-mono">{new Date(graphOutput.metadata.generatedAt).toLocaleString()}</span>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Total Nodes:</span>
                              <Badge variant="outline">{graphOutput.metadata.totalNodes}</Badge>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Total Edges:</span>
                              <Badge variant="outline">{graphOutput.metadata.totalEdges}</Badge>
                            </div>
                          </div>
                        </CollapsibleContent>
                      </Collapsible>

                      <Separator />

                      {/* Traversal Path */}
                      <div className="space-y-2">
                        <h3 className="text-sm font-semibold">Traversal Path</h3>
                        <div className="rounded-lg border border-border bg-card p-3">
                          <p className="text-xs font-mono break-all">
                            {graphOutput.traversalPath.join(' → ')}
                          </p>
                        </div>
                      </div>

                      <Separator />

                      {/* Nodes */}
                      <div className="space-y-2">
                        <h3 className="text-sm font-semibold">Nodes ({graphOutput.nodes.length})</h3>
                        <div className="space-y-2">
                          {graphOutput.nodes.map((node) => (
                            <div key={node.id} className="rounded-lg border border-border bg-card p-3">
                              <div className="flex items-center justify-between">
                                <span className="text-xs font-semibold">{node.label}</span>
                                <Badge variant="secondary" className="text-xs">
                                  Value: {node.value}
                                </Badge>
                              </div>
                              <p className="mt-1 text-[10px] text-muted-foreground font-mono">
                                ID: {node.id}
                              </p>
                            </div>
                          ))}
                        </div>
                      </div>

                      <Separator />

                      {/* Edges */}
                      <div className="space-y-2">
                        <h3 className="text-sm font-semibold">Edges ({graphOutput.edges.length})</h3>
                        <div className="space-y-2">
                          {graphOutput.edges.map((edge, idx) => (
                            <div key={idx} className="rounded-lg border border-border bg-card p-3">
                              <div className="flex items-center justify-between">
                                <span className="text-xs font-mono">
                                  {edge.from} → {edge.to}
                                </span>
                                <Badge variant="outline" className="text-xs">
                                  Weight: {edge.weight.toFixed(3)}
                                </Badge>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  </ScrollArea>
                </TabsContent>
                <TabsContent value="raw" className="flex-1 overflow-hidden px-4 pb-4">
                  <ScrollArea className="h-full rounded-lg border border-border bg-muted/30 p-4">
                    <pre className="text-xs font-mono whitespace-pre-wrap break-all">
                      {JSON.stringify(graphOutput, null, 2)}
                    </pre>
                  </ScrollArea>
                </TabsContent>
              </Tabs>
            ) : (
              <div className="flex h-full items-center justify-center p-8 text-center">
                <div className="space-y-2">
                  <Network className="mx-auto h-12 w-12 text-muted-foreground/50" />
                  <p className="text-sm text-muted-foreground">
                    No output yet. Click "Start Generation" to create a graph.
                  </p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
