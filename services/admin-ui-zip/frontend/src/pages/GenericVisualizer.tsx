import { useState, useRef } from 'react';
import { Eye, Upload, FileText, AlertCircle, RotateCcw, Maximize2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import VisualizerScene from '../components/VisualizerScene';
import { parseYAMLConfig, type EntityConfig } from '../lib/yamlParser';
import { debugLogger } from '../lib/debugLogger';

type EntityType = 'character' | 'npc' | 'item' | 'terrain' | 'dungeon';

export default function GenericVisualizer() {
  const [entityType, setEntityType] = useState<EntityType>('character');
  const [yamlConfig, setYamlConfig] = useState<EntityConfig | null>(null);
  const [modelFiles, setModelFiles] = useState<File[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [yamlText, setYamlText] = useState<string>('');
  const [isLoading, setIsLoading] = useState(false);
  const yamlInputRef = useRef<HTMLInputElement>(null);
  const modelInputRef = useRef<HTMLInputElement>(null);

  const handleEntityTypeChange = (value: string) => {
    setEntityType(value as EntityType);
    // Reset state when changing entity type
    setYamlConfig(null);
    setModelFiles([]);
    setError(null);
    setYamlText('');
    debugLogger.info('Visualizer', `Entity type changed to: ${value}`);
  };

  const handleYAMLUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    setIsLoading(true);
    setError(null);
    debugLogger.info('Visualizer', `Loading YAML configuration: ${file.name}`);

    try {
      const text = await file.text();
      setYamlText(text);
      
      const config = parseYAMLConfig(text, entityType);
      setYamlConfig(config);
      
      debugLogger.success('Visualizer', `YAML configuration parsed successfully for ${entityType}`);
      debugLogger.info('Visualizer', `Configuration contains ${config.models.length} model reference(s)`);
    } catch (err: any) {
      const errorMsg = `Failed to parse YAML: ${err.message}`;
      setError(errorMsg);
      debugLogger.error('Visualizer', errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleModelUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files || []);
    if (files.length === 0) return;

    debugLogger.info('Visualizer', `Loading ${files.length} model file(s)`);
    
    // Validate file types
    const validExtensions = ['.glb', '.gltf', '.obj', '.fbx'];
    const invalidFiles = files.filter(file => {
      const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));
      return !validExtensions.includes(ext);
    });

    if (invalidFiles.length > 0) {
      const errorMsg = `Invalid file types: ${invalidFiles.map(f => f.name).join(', ')}. Supported formats: GLB, GLTF, OBJ, FBX`;
      setError(errorMsg);
      debugLogger.error('Visualizer', errorMsg);
      return;
    }

    setModelFiles(files);
    setError(null);
    debugLogger.success('Visualizer', `Loaded ${files.length} model file(s): ${files.map(f => f.name).join(', ')}`);
  };

  const handleReset = () => {
    setYamlConfig(null);
    setModelFiles([]);
    setError(null);
    setYamlText('');
    if (yamlInputRef.current) yamlInputRef.current.value = '';
    if (modelInputRef.current) modelInputRef.current.value = '';
    debugLogger.info('Visualizer', 'Visualizer reset');
  };

  const canVisualize = yamlConfig !== null && modelFiles.length > 0;

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border bg-card px-4 py-3 sm:px-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
              <Eye className="h-5 w-5 text-primary-foreground" />
            </div>
            <div>
              <h1 className="text-xl font-bold tracking-tight sm:text-2xl">Generic Visualizer</h1>
              <p className="text-xs text-muted-foreground sm:text-sm">
                YAML-driven 3D entity visualization system
              </p>
            </div>
          </div>
          {canVisualize && (
            <Button variant="outline" size="sm" onClick={handleReset}>
              <RotateCcw className="mr-2 h-4 w-4" />
              Reset
            </Button>
          )}
        </div>
      </div>

      {/* Offline Mode Alert */}
      <div className="px-4 pt-4 sm:px-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Offline Mode Active</AlertTitle>
          <AlertDescription>
            Visualizer operates in local-only mode with client-side YAML parsing and model loading.
          </AlertDescription>
        </Alert>
      </div>

      {/* Main Content */}
      <div className="flex flex-1 flex-col gap-4 overflow-hidden p-4 sm:p-6">
        {!canVisualize ? (
          <div className="grid gap-4 lg:grid-cols-2">
            {/* Configuration Panel */}
            <Card>
              <CardHeader>
                <CardTitle>Configuration</CardTitle>
                <CardDescription>
                  Select entity type and upload YAML configuration
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Entity Type Selector */}
                <div className="space-y-2">
                  <Label htmlFor="entity-type">Entity Type</Label>
                  <Select value={entityType} onValueChange={handleEntityTypeChange}>
                    <SelectTrigger id="entity-type">
                      <SelectValue placeholder="Select entity type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="character">Character</SelectItem>
                      <SelectItem value="npc">NPC</SelectItem>
                      <SelectItem value="item">Item</SelectItem>
                      <SelectItem value="terrain">Terrain</SelectItem>
                      <SelectItem value="dungeon">Dungeon</SelectItem>
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    Choose the type of entity to visualize
                  </p>
                </div>

                <Separator />

                {/* YAML Upload */}
                <div className="space-y-2">
                  <Label htmlFor="yaml-upload">YAML Configuration</Label>
                  <div className="flex gap-2">
                    <input
                      ref={yamlInputRef}
                      id="yaml-upload"
                      type="file"
                      accept=".yaml,.yml"
                      onChange={handleYAMLUpload}
                      className="hidden"
                    />
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => yamlInputRef.current?.click()}
                      disabled={isLoading}
                    >
                      <FileText className="mr-2 h-4 w-4" />
                      {isLoading ? 'Loading...' : yamlConfig ? 'Change Configuration' : 'Upload YAML'}
                    </Button>
                  </div>
                  {yamlConfig && (
                    <div className="rounded-md border border-green-500/20 bg-green-500/10 p-2">
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ Configuration loaded: {yamlConfig.name || 'Unnamed entity'}
                      </p>
                    </div>
                  )}
                  <p className="text-xs text-muted-foreground">
                    Upload a YAML file containing entity configuration
                  </p>
                </div>

                {/* Model Upload */}
                <div className="space-y-2">
                  <Label htmlFor="model-upload">3D Models</Label>
                  <div className="flex gap-2">
                    <input
                      ref={modelInputRef}
                      id="model-upload"
                      type="file"
                      accept=".glb,.gltf,.obj,.fbx"
                      multiple
                      onChange={handleModelUpload}
                      className="hidden"
                    />
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => modelInputRef.current?.click()}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      {modelFiles.length > 0 ? `${modelFiles.length} file(s) selected` : 'Upload Models'}
                    </Button>
                  </div>
                  {modelFiles.length > 0 && (
                    <div className="rounded-md border border-green-500/20 bg-green-500/10 p-2">
                      <p className="text-xs text-green-600 dark:text-green-400">
                        ✓ {modelFiles.length} model file(s) loaded
                      </p>
                    </div>
                  )}
                  <p className="text-xs text-muted-foreground">
                    Upload 3D model files (GLB, GLTF, OBJ, FBX)
                  </p>
                </div>

                {/* Error Display */}
                {error && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Error</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                  </Alert>
                )}

                {/* Visualize Button */}
                {yamlConfig && modelFiles.length > 0 && (
                  <Button className="w-full" size="lg">
                    <Maximize2 className="mr-2 h-5 w-5" />
                    Ready to Visualize
                  </Button>
                )}
              </CardContent>
            </Card>

            {/* Configuration Preview */}
            {yamlConfig && (
              <Card>
                <CardHeader>
                  <CardTitle>Configuration Preview</CardTitle>
                  <CardDescription>
                    Parsed YAML structure for {entityType}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <Tabs defaultValue="parsed">
                    <TabsList className="grid w-full grid-cols-2">
                      <TabsTrigger value="parsed">Parsed</TabsTrigger>
                      <TabsTrigger value="raw">Raw YAML</TabsTrigger>
                    </TabsList>
                    <TabsContent value="parsed" className="mt-4">
                      <ScrollArea className="h-[400px] rounded-md border p-4">
                        <div className="space-y-3 text-sm">
                          <div>
                            <span className="font-semibold text-primary">Name:</span>{' '}
                            <span className="text-muted-foreground">{yamlConfig.name || 'N/A'}</span>
                          </div>
                          <div>
                            <span className="font-semibold text-primary">Type:</span>{' '}
                            <span className="text-muted-foreground">{yamlConfig.type}</span>
                          </div>
                          <Separator />
                          <div>
                            <span className="font-semibold text-primary">Models ({yamlConfig.models.length}):</span>
                            <ul className="ml-4 mt-2 space-y-1">
                              {yamlConfig.models.map((model, idx) => (
                                <li key={idx} className="text-muted-foreground">
                                  • {model.path}
                                </li>
                              ))}
                            </ul>
                          </div>
                          {yamlConfig.transform && (
                            <>
                              <Separator />
                              <div>
                                <span className="font-semibold text-primary">Transform:</span>
                                <div className="ml-4 mt-2 space-y-1 text-muted-foreground">
                                  <div>Position: [{yamlConfig.transform.position.join(', ')}]</div>
                                  <div>Rotation: [{yamlConfig.transform.rotation.join(', ')}]</div>
                                  <div>Scale: [{yamlConfig.transform.scale.join(', ')}]</div>
                                </div>
                              </div>
                            </>
                          )}
                          {yamlConfig.metadata && Object.keys(yamlConfig.metadata).length > 0 && (
                            <>
                              <Separator />
                              <div>
                                <span className="font-semibold text-primary">Metadata:</span>
                                <pre className="ml-4 mt-2 text-xs text-muted-foreground">
                                  {JSON.stringify(yamlConfig.metadata, null, 2)}
                                </pre>
                              </div>
                            </>
                          )}
                        </div>
                      </ScrollArea>
                    </TabsContent>
                    <TabsContent value="raw" className="mt-4">
                      <ScrollArea className="h-[400px] rounded-md border p-4">
                        <pre className="text-xs text-muted-foreground">{yamlText}</pre>
                      </ScrollArea>
                    </TabsContent>
                  </Tabs>
                </CardContent>
              </Card>
            )}
          </div>
        ) : (
          <VisualizerScene
            config={yamlConfig}
            modelFiles={modelFiles}
            entityType={entityType}
          />
        )}
      </div>
    </div>
  );
}
