import { useState, useEffect } from 'react';
import { FileCode, Upload, Play, Trash2, Copy, Check, AlertCircle, Clock, CheckCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Progress } from '@/components/ui/progress';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { parseYAML, mergeYAML } from '../lib/yamlParser';
import { debugLogger } from '../lib/debugLogger';

interface YAMLRequest {
  id: string;
  timestamp: number;
  status: 'pending' | 'processing' | 'completed' | 'error';
  priority: number;
  partialYAML: string;
  templateType: string;
  progress: number;
  result?: string;
  error?: string;
}

export default function YAMLBackendService() {
  const [partialYAML, setPartialYAML] = useState<string>('');
  const [parsedYAML, setParsedYAML] = useState<any>(null);
  const [parseError, setParseError] = useState<string>('');
  const [selectedTemplate, setSelectedTemplate] = useState<string>('npc');
  const [requests, setRequests] = useState<YAMLRequest[]>([]);
  const [copiedId, setCopiedId] = useState<string>('');

  useEffect(() => {
    debugLogger.info('yaml-service', 'YAML Backend Service initialized in offline mode');
  }, []);

  const templates = {
    npc: `# NPC Template
name: ""
type: npc
stats:
  health: 100
  attack: 10
  defense: 5
behavior:
  aggression: 0.5
  patrol: true
loot:
  - item: "gold"
    chance: 0.8`,
    item: `# Item Template
name: ""
type: item
category: weapon
stats:
  damage: 0
  durability: 100
rarity: common
effects: []`,
    dungeon: `# Dungeon Template
name: ""
type: dungeon
rooms: []
connections: []
difficulty: 1
encounters: []`,
    quest: `# Quest Template
name: ""
type: quest
objectives: []
rewards: []
requirements: []
difficulty: 1`,
    boss: `# Boss Template
name: ""
type: boss
stats:
  health: 1000
  attack: 50
  defense: 20
phases: []
abilities: []
loot: []`
  };

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    debugLogger.info('yaml-service', `Loading YAML file: ${file.name}`);
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      setPartialYAML(content);
      handleParseYAML(content);
    };
    reader.onerror = () => {
      debugLogger.error('yaml-service', 'Failed to read YAML file');
      setParseError('Failed to read file');
    };
    reader.readAsText(file);
  };

  const handleParseYAML = (yamlContent: string) => {
    try {
      const parsed = parseYAML(yamlContent);
      setParsedYAML(parsed);
      setParseError('');
      debugLogger.success('yaml-service', 'YAML parsed successfully');
    } catch (error: any) {
      setParseError(error.message);
      setParsedYAML(null);
      debugLogger.error('yaml-service', `YAML parsing failed: ${error.message}`);
    }
  };

  const handleProcessRequest = () => {
    if (!partialYAML.trim()) {
      setParseError('Please provide YAML content');
      return;
    }

    const newRequest: YAMLRequest = {
      id: `req-${Date.now()}`,
      timestamp: Date.now(),
      status: 'pending',
      priority: Math.floor(Math.random() * 3) + 1,
      partialYAML,
      templateType: selectedTemplate,
      progress: 0,
    };

    setRequests(prev => [newRequest, ...prev]);
    debugLogger.info('yaml-service', `Created request ${newRequest.id} with priority ${newRequest.priority}`);

    // Simulate processing
    simulateProcessing(newRequest.id);
  };

  const simulateProcessing = (requestId: string) => {
    let progress = 0;
    const interval = setInterval(() => {
      progress += 10;
      
      setRequests(prev => prev.map(req => {
        if (req.id !== requestId) return req;
        
        if (progress <= 30) {
          return { ...req, status: 'processing' as const, progress };
        } else if (progress <= 100) {
          return { ...req, status: 'processing' as const, progress };
        } else {
          clearInterval(interval);
          
          try {
            const template = templates[selectedTemplate as keyof typeof templates];
            const merged = mergeYAML(template, partialYAML);
            debugLogger.success('yaml-service', `Request ${requestId} completed successfully`);
            return {
              ...req,
              status: 'completed' as const,
              progress: 100,
              result: merged,
            };
          } catch (error: any) {
            debugLogger.error('yaml-service', `Request ${requestId} failed: ${error.message}`);
            return {
              ...req,
              status: 'error' as const,
              progress: 100,
              error: error.message,
            };
          }
        }
      }));
    }, 200);
  };

  const handleCopyResult = (result: string, id: string) => {
    navigator.clipboard.writeText(result);
    setCopiedId(id);
    setTimeout(() => setCopiedId(''), 2000);
    debugLogger.info('yaml-service', 'Result copied to clipboard');
  };

  const handleClearQueue = () => {
    setRequests([]);
    debugLogger.info('yaml-service', 'Queue cleared');
  };

  const getStatusIcon = (status: YAMLRequest['status']) => {
    switch (status) {
      case 'pending':
        return <Clock className="h-4 w-4 text-yellow-500" />;
      case 'processing':
        return <div className="h-4 w-4 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />;
      case 'completed':
        return <CheckCircle className="h-4 w-4 text-green-500" />;
      case 'error':
        return <XCircle className="h-4 w-4 text-red-500" />;
    }
  };

  const getStatusBadge = (status: YAMLRequest['status']) => {
    const variants = {
      pending: 'secondary',
      processing: 'default',
      completed: 'outline',
      error: 'destructive',
    };
    return <Badge variant={variants[status] as any}>{status}</Badge>;
  };

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      <div className="flex-1 overflow-y-auto">
        <div className="container mx-auto space-y-6 p-4 sm:p-6">
          {/* Header */}
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
                <FileCode className="h-6 w-6 text-primary-foreground" />
              </div>
              <div>
                <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">YAML Backend Service</h1>
                <p className="text-sm text-muted-foreground">
                  Generic YAML-based backend responder with queue simulation
                </p>
              </div>
            </div>
          </div>

          {/* Offline Mode Alert */}
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Offline Mode</AlertTitle>
            <AlertDescription>
              Running in simulation mode. All processing happens locally in the browser.
            </AlertDescription>
          </Alert>

          <div className="grid gap-6 lg:grid-cols-2">
            {/* Input Section */}
            <Card>
              <CardHeader>
                <CardTitle>YAML Configuration Input</CardTitle>
                <CardDescription>
                  Upload or paste partial YAML configuration
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => document.getElementById('yaml-upload')?.click()}
                  >
                    <Upload className="mr-2 h-4 w-4" />
                    Upload YAML File
                  </Button>
                  <input
                    id="yaml-upload"
                    type="file"
                    accept=".yaml,.yml"
                    className="hidden"
                    onChange={handleFileUpload}
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">Template Type</label>
                  <Select value={selectedTemplate} onValueChange={setSelectedTemplate}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="npc">NPC</SelectItem>
                      <SelectItem value="item">Item</SelectItem>
                      <SelectItem value="dungeon">Dungeon</SelectItem>
                      <SelectItem value="quest">Quest</SelectItem>
                      <SelectItem value="boss">Boss</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">Partial YAML</label>
                  <textarea
                    className="min-h-[200px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
                    placeholder="Paste partial YAML configuration here..."
                    value={partialYAML}
                    onChange={(e) => {
                      setPartialYAML(e.target.value);
                      handleParseYAML(e.target.value);
                    }}
                  />
                </div>

                {parseError && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Parse Error</AlertTitle>
                    <AlertDescription>{parseError}</AlertDescription>
                  </Alert>
                )}

                <Button
                  className="w-full"
                  onClick={handleProcessRequest}
                  disabled={!partialYAML.trim() || !!parseError}
                >
                  <Play className="mr-2 h-4 w-4" />
                  Process Request
                </Button>
              </CardContent>
            </Card>

            {/* Preview Section */}
            <Card>
              <CardHeader>
                <CardTitle>Configuration Preview</CardTitle>
                <CardDescription>
                  Parsed YAML structure and template
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Tabs defaultValue="parsed">
                  <TabsList className="grid w-full grid-cols-2">
                    <TabsTrigger value="parsed">Parsed</TabsTrigger>
                    <TabsTrigger value="template">Template</TabsTrigger>
                  </TabsList>
                  <TabsContent value="parsed" className="mt-4">
                    <ScrollArea className="h-[300px] w-full rounded-md border p-4">
                      <pre className="text-xs font-mono">
                        {parsedYAML ? JSON.stringify(parsedYAML, null, 2) : 'No YAML parsed yet'}
                      </pre>
                    </ScrollArea>
                  </TabsContent>
                  <TabsContent value="template" className="mt-4">
                    <ScrollArea className="h-[300px] w-full rounded-md border p-4">
                      <pre className="text-xs font-mono">
                        {templates[selectedTemplate as keyof typeof templates]}
                      </pre>
                    </ScrollArea>
                  </TabsContent>
                </Tabs>
              </CardContent>
            </Card>
          </div>

          {/* Queue Section */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Request Queue</CardTitle>
                  <CardDescription>
                    Simulated message queue with priority-based processing
                  </CardDescription>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleClearQueue}
                  disabled={requests.length === 0}
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Clear Queue
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <ScrollArea className="h-[400px]">
                {requests.length === 0 ? (
                  <div className="flex h-[200px] items-center justify-center text-muted-foreground">
                    No requests in queue
                  </div>
                ) : (
                  <div className="space-y-4">
                    {requests.map((request) => (
                      <Card key={request.id}>
                        <CardHeader className="pb-3">
                          <div className="flex items-start justify-between">
                            <div className="space-y-1">
                              <div className="flex items-center gap-2">
                                {getStatusIcon(request.status)}
                                <span className="font-mono text-sm">{request.id}</span>
                                {getStatusBadge(request.status)}
                                <Badge variant="outline">Priority {request.priority}</Badge>
                              </div>
                              <p className="text-xs text-muted-foreground">
                                {new Date(request.timestamp).toLocaleString()}
                              </p>
                            </div>
                          </div>
                        </CardHeader>
                        <CardContent className="space-y-3">
                          {request.status === 'processing' && (
                            <div className="space-y-2">
                              <div className="flex justify-between text-xs">
                                <span>Processing...</span>
                                <span>{request.progress}%</span>
                              </div>
                              <Progress value={request.progress} />
                            </div>
                          )}

                          {request.status === 'completed' && request.result && (
                            <div className="space-y-2">
                              <div className="flex items-center justify-between">
                                <span className="text-sm font-medium">Completed YAML</span>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => handleCopyResult(request.result!, request.id)}
                                >
                                  {copiedId === request.id ? (
                                    <Check className="h-4 w-4 text-green-500" />
                                  ) : (
                                    <Copy className="h-4 w-4" />
                                  )}
                                </Button>
                              </div>
                              <ScrollArea className="h-[150px] w-full rounded-md border bg-muted/50 p-3">
                                <pre className="text-xs font-mono">{request.result}</pre>
                              </ScrollArea>
                            </div>
                          )}

                          {request.status === 'error' && request.error && (
                            <Alert variant="destructive">
                              <AlertCircle className="h-4 w-4" />
                              <AlertTitle>Processing Error</AlertTitle>
                              <AlertDescription>{request.error}</AlertDescription>
                            </Alert>
                          )}
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                )}
              </ScrollArea>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
