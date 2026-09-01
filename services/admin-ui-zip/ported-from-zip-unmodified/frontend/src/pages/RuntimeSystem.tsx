import { useState, useEffect } from 'react';
import { runtimeManager } from '../lib/runtimeManager';
import { queueManager } from '../lib/queueManager';
import type { EventMessage } from '../lib/queueManager';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Activity, Database, Cpu, Zap, AlertCircle, Pause, RotateCcw, Play, Square } from 'lucide-react';
import type { Entity, PerformanceMetrics } from '../types/runtime';

export default function RuntimeSystem() {
  const [entities, setEntities] = useState<Entity[]>([]);
  const [metrics, setMetrics] = useState<PerformanceMetrics>(runtimeManager.getMetrics());
  const [selectedEntity, setSelectedEntity] = useState<Entity | null>(null);
  const [queueStats, setQueueStats] = useState(queueManager.getStats());
  const [recentEvents, setRecentEvents] = useState<string[]>([]);

  // Subscribe to queue events
  useEffect(() => {
    const unsubscribeEvents = queueManager.subscribe<EventMessage>('runtime:events', (message) => {
      const eventStr = `[${new Date(message.timestamp).toLocaleTimeString()}] ${message.data.eventType}`;
      setRecentEvents(prev => [eventStr, ...prev].slice(0, 10));
    });

    return () => {
      unsubscribeEvents();
    };
  }, []);

  // Update frontend runtime state
  useEffect(() => {
    const interval = setInterval(() => {
      setEntities(runtimeManager.getAllEntities());
      setMetrics(runtimeManager.getMetrics());
      setQueueStats(queueManager.getStats());
    }, 100);

    return () => clearInterval(interval);
  }, []);

  const handleStart = () => {
    runtimeManager.start();
  };

  const handleStop = () => {
    runtimeManager.stop();
  };

  const handlePause = () => {
    runtimeManager.pause();
  };

  const handleResume = () => {
    runtimeManager.resume();
  };

  const handleReset = () => {
    runtimeManager.reset();
  };

  const handleCreateTestEntity = () => {
    const entity = runtimeManager.createEntity();
    runtimeManager.addComponent(entity.id, 'Transform', {
      position: { x: 0, y: 0, z: 0 },
      rotation: { x: 0, y: 0, z: 0 },
      scale: { x: 1, y: 1, z: 1 },
    });
    runtimeManager.addComponent(entity.id, 'Health', {
      max: 100,
      current: 100,
      regenerationRate: 1,
    });

    // Publish entity creation event to queue
    queueManager.publish<EventMessage>('runtime:events', {
      eventType: 'entity:created',
      entityId: entity.id,
    });
  };

  const runtimeState = runtimeManager.getState();
  const isFrontendRunning = runtimeState.isRunning;
  const isFrontendPaused = runtimeState.isPaused;

  return (
    <div className="flex h-full flex-col overflow-hidden p-4 sm:p-6">
      <div className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold sm:text-3xl">Runtime System</h1>
          <p className="text-sm text-muted-foreground">
            Frontend ECS/DOTS Diagnostics & Monitoring
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            onClick={handleStart}
            disabled={isFrontendRunning}
            variant="default"
            size="sm"
          >
            <Play className="mr-2 h-4 w-4" />
            Start
          </Button>
          <Button
            onClick={handleStop}
            disabled={!isFrontendRunning}
            variant="destructive"
            size="sm"
          >
            <Square className="mr-2 h-4 w-4" />
            Stop
          </Button>
          <Button
            onClick={isFrontendPaused ? handleResume : handlePause}
            disabled={!isFrontendRunning}
            variant="outline"
            size="sm"
          >
            <Pause className="mr-2 h-4 w-4" />
            {isFrontendPaused ? 'Resume' : 'Pause'}
          </Button>
          <Button
            onClick={handleReset}
            variant="outline"
            size="sm"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            Reset
          </Button>
          <Button
            onClick={handleCreateTestEntity}
            variant="outline"
            size="sm"
          >
            <Zap className="mr-2 h-4 w-4" />
            Create Entity
          </Button>
        </div>
      </div>

      {/* Info Alert */}
      <Alert className="mb-4">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          This is a frontend-only diagnostics page. All runtime operations are local and do not communicate with the backend.
        </AlertDescription>
      </Alert>

      {/* Frontend Runtime Status */}
      <Card className="mb-4">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Frontend Runtime Status</CardTitle>
              <CardDescription>Local ECS runtime diagnostics</CardDescription>
            </div>
            <Badge variant={isFrontendRunning ? (isFrontendPaused ? 'outline' : 'default') : 'secondary'}>
              {isFrontendPaused ? 'Paused' : isFrontendRunning ? 'Running' : 'Stopped'}
            </Badge>
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Database className="h-4 w-4" />
                <span className="text-xs">Entities</span>
              </div>
              <p className="text-lg font-semibold">{metrics.entityCount}</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Activity className="h-4 w-4" />
                <span className="text-xs">FPS</span>
              </div>
              <p className="text-lg font-semibold">{metrics.fps.toFixed(1)}</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Cpu className="h-4 w-4" />
                <span className="text-xs">Tick Time</span>
              </div>
              <p className="text-lg font-semibold">{metrics.tickTime.toFixed(2)}ms</p>
            </div>

            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2 text-muted-foreground">
                <Zap className="h-4 w-4" />
                <span className="text-xs">Systems</span>
              </div>
              <p className="text-lg font-semibold">{metrics.activeSystemCount}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Queue System Status */}
      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Queue System Status</CardTitle>
          <CardDescription>Local pub/sub message queue</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Queue Size</span>
              <p className="text-lg font-semibold">{queueStats.queueSize}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Command Subscribers</span>
              <p className="text-lg font-semibold">{queueStats.subscriberCounts['runtime:command'] || 0}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Event Subscribers</span>
              <p className="text-lg font-semibold">{queueStats.subscriberCounts['runtime:events'] || 0}</p>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">Processing</span>
              <Badge variant={queueStats.isProcessing ? 'default' : 'secondary'}>
                {queueStats.isProcessing ? 'Active' : 'Idle'}
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Main Content */}
      <Tabs defaultValue="entities" className="flex-1 overflow-hidden">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="entities">Entities</TabsTrigger>
          <TabsTrigger value="systems">Systems</TabsTrigger>
          <TabsTrigger value="metrics">Metrics</TabsTrigger>
          <TabsTrigger value="events">Events</TabsTrigger>
        </TabsList>

        <TabsContent value="entities" className="h-[calc(100%-3rem)] overflow-hidden">
          <div className="grid h-full gap-4 lg:grid-cols-2">
            <Card className="flex flex-col overflow-hidden">
              <CardHeader>
                <CardTitle>Entity List</CardTitle>
                <CardDescription>
                  {entities.length} active entities
                </CardDescription>
              </CardHeader>
              <CardContent className="flex-1 overflow-hidden p-0">
                <ScrollArea className="h-full px-6 pb-6">
                  <div className="space-y-2">
                    {entities.length === 0 ? (
                      <p className="py-8 text-center text-sm text-muted-foreground">
                        No entities in runtime. Create a test entity to get started.
                      </p>
                    ) : (
                      entities.map((entity) => (
                        <div
                          key={entity.id}
                          className={`cursor-pointer rounded-lg border p-3 transition-colors hover:bg-accent ${
                            selectedEntity?.id === entity.id ? 'border-primary bg-accent' : ''
                          }`}
                          onClick={() => setSelectedEntity(entity)}
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-2">
                              <Badge variant={entity.active ? 'default' : 'secondary'}>
                                {entity.active ? 'Active' : 'Inactive'}
                              </Badge>
                              <span className="text-sm font-mono">{entity.id.slice(0, 12)}...</span>
                            </div>
                            <span className="text-xs text-muted-foreground">
                              {entity.components.size} components
                            </span>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </ScrollArea>
              </CardContent>
            </Card>

            <Card className="flex flex-col overflow-hidden">
              <CardHeader>
                <CardTitle>Entity Inspector</CardTitle>
                <CardDescription>
                  {selectedEntity ? `Inspecting ${selectedEntity.id}` : 'Select an entity'}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex-1 overflow-hidden p-0">
                <ScrollArea className="h-full px-6 pb-6">
                  {selectedEntity ? (
                    <div className="space-y-4">
                      <div>
                        <h3 className="mb-2 text-sm font-semibold">Entity Info</h3>
                        <div className="space-y-1 text-sm">
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">ID:</span>
                            <span className="font-mono">{selectedEntity.id}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Active:</span>
                            <span>{selectedEntity.active ? 'Yes' : 'No'}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Created:</span>
                            <span>{new Date(selectedEntity.createdAt).toLocaleTimeString()}</span>
                          </div>
                        </div>
                      </div>

                      <Separator />

                      <div>
                        <h3 className="mb-2 text-sm font-semibold">Components</h3>
                        <div className="space-y-3">
                          {Array.from(selectedEntity.components.entries()).map(([type, data]) => (
                            <div key={type} className="rounded-lg border p-3">
                              <h4 className="mb-2 text-sm font-medium">{type}</h4>
                              <pre className="overflow-x-auto text-xs">
                                {JSON.stringify(data, null, 2)}
                              </pre>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      Select an entity to inspect its components
                    </p>
                  )}
                </ScrollArea>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="systems" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>System Profiler</CardTitle>
              <CardDescription>
                {metrics.activeSystemCount} active systems
              </CardDescription>
            </CardHeader>
            <CardContent className="overflow-hidden p-0">
              <ScrollArea className="h-full px-6 pb-6">
                <div className="space-y-2">
                  {Array.from(metrics.systemExecutionTimes.entries()).map(([systemId, time]) => (
                    <div key={systemId} className="rounded-lg border p-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium">{systemId}</span>
                        <Badge variant="outline">{time.toFixed(2)}ms</Badge>
                      </div>
                    </div>
                  ))}
                  {metrics.systemExecutionTimes.size === 0 && (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No systems registered yet
                    </p>
                  )}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="metrics" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>Performance Metrics</CardTitle>
              <CardDescription>
                Real-time runtime performance data
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Frame Rate</p>
                    <p className="text-2xl font-bold">{metrics.fps.toFixed(1)} FPS</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Tick Time</p>
                    <p className="text-2xl font-bold">{metrics.tickTime.toFixed(2)}ms</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Entity Count</p>
                    <p className="text-2xl font-bold">{metrics.entityCount}</p>
                  </div>
                  <div className="rounded-lg border p-4">
                    <p className="text-sm text-muted-foreground">Active Systems</p>
                    <p className="text-2xl font-bold">{metrics.activeSystemCount}</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="events" className="h-[calc(100%-3rem)]">
          <Card className="h-full overflow-hidden">
            <CardHeader>
              <CardTitle>Queue Events</CardTitle>
              <CardDescription>
                Recent local queue messages
              </CardDescription>
            </CardHeader>
            <CardContent className="overflow-hidden p-0">
              <ScrollArea className="h-full px-6 pb-6">
                <div className="space-y-1">
                  {recentEvents.length === 0 ? (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No events yet
                    </p>
                  ) : (
                    recentEvents.map((event, index) => (
                      <div key={index} className="rounded border-l-2 border-primary bg-muted/50 p-2 text-xs font-mono">
                        {event}
                      </div>
                    ))
                  )}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
