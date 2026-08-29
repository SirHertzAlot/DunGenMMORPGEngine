import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Slider } from '@/components/ui/slider';
import { RotateCcw, Maximize2 } from 'lucide-react';
import { debugLogger } from '../lib/debugLogger';
import type { EntityConfig } from '../lib/yamlParser';

interface VisualizerSceneProps {
  config: EntityConfig;
  modelFiles: File[];
  entityType: string;
}

export default function VisualizerScene({ config, modelFiles, entityType }: VisualizerSceneProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const loadedModelsRef = useRef<THREE.Object3D[]>([]);
  const animationFrameRef = useRef<number | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [loadProgress, setLoadProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [position, setPosition] = useState<[number, number, number]>(
    config.transform?.position || [0, 0, 0]
  );
  const [rotation, setRotation] = useState<[number, number, number]>(
    config.transform?.rotation || [0, 0, 0]
  );
  const [scale, setScale] = useState<[number, number, number]>(
    config.transform?.scale || [1, 1, 1]
  );

  useEffect(() => {
    if (!containerRef.current) return;

    debugLogger.info('visualizer', 'Initializing visualizer scene');

    // Initialize scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a1a);
    sceneRef.current = scene;

    // Initialize camera
    const camera = new THREE.PerspectiveCamera(
      75,
      containerRef.current.clientWidth / containerRef.current.clientHeight,
      0.1,
      1000
    );
    camera.position.set(5, 5, 5);
    cameraRef.current = camera;

    // Initialize renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    renderer.setPixelRatio(window.devicePixelRatio);
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Initialize controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controlsRef.current = controls;

    // Add lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(5, 10, 7.5);
    scene.add(directionalLight);

    // Add grid helper
    const gridHelper = new THREE.GridHelper(10, 10);
    scene.add(gridHelper);

    // Animation loop
    const animate = () => {
      animationFrameRef.current = requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    // Handle resize
    const handleResize = () => {
      if (!containerRef.current || !camera || !renderer) return;
      camera.aspect = containerRef.current.clientWidth / containerRef.current.clientHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    };
    window.addEventListener('resize', handleResize);

    // Load models
    loadModels();

    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      if (rendererRef.current && containerRef.current) {
        containerRef.current.removeChild(rendererRef.current.domElement);
      }
      rendererRef.current?.dispose();
      loadedModelsRef.current.forEach(model => {
        model.traverse(child => {
          if (child instanceof THREE.Mesh) {
            child.geometry.dispose();
            if (Array.isArray(child.material)) {
              child.material.forEach(mat => mat.dispose());
            } else {
              child.material.dispose();
            }
          }
        });
      });
    };
  }, []);

  const loadModels = async () => {
    if (!sceneRef.current) return;

    setIsLoading(true);
    setError(null);
    debugLogger.info('visualizer', `Loading ${modelFiles.length} model file(s)`);

    const gltfLoader = new GLTFLoader();
    const objLoader = new OBJLoader();
    const fbxLoader = new FBXLoader();

    let loadedCount = 0;

    for (const file of modelFiles) {
      try {
        const url = URL.createObjectURL(file);
        const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));

        debugLogger.info('visualizer', `Loading model: ${file.name}`);

        let model: THREE.Object3D | null = null;

        if (ext === '.glb' || ext === '.gltf') {
          const gltf = await new Promise<any>((resolve, reject) => {
            gltfLoader.load(url, resolve, undefined, reject);
          });
          model = gltf.scene;
        } else if (ext === '.obj') {
          model = await new Promise<THREE.Object3D>((resolve, reject) => {
            objLoader.load(url, resolve, undefined, reject);
          });
        } else if (ext === '.fbx') {
          model = await new Promise<THREE.Object3D>((resolve, reject) => {
            fbxLoader.load(url, resolve, undefined, reject);
          });
        }

        if (model) {
          // Apply initial transform
          model.position.set(position[0], position[1], position[2]);
          const rotRad: [number, number, number] = [
            (rotation[0] * Math.PI) / 180,
            (rotation[1] * Math.PI) / 180,
            (rotation[2] * Math.PI) / 180,
          ];
          model.rotation.set(rotRad[0], rotRad[1], rotRad[2]);
          model.scale.set(scale[0], scale[1], scale[2]);

          sceneRef.current.add(model);
          loadedModelsRef.current.push(model);

          // Center camera on model
          const box = new THREE.Box3().setFromObject(model);
          const center = box.getCenter(new THREE.Vector3());
          const size = box.getSize(new THREE.Vector3());
          const maxDim = Math.max(size.x, size.y, size.z);
          const fov = cameraRef.current!.fov * (Math.PI / 180);
          let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2));
          cameraZ *= 1.5;

          if (cameraRef.current) {
            cameraRef.current.position.set(center.x + cameraZ, center.y + cameraZ, center.z + cameraZ);
            cameraRef.current.lookAt(center);
          }

          if (controlsRef.current) {
            controlsRef.current.target.copy(center);
            controlsRef.current.update();
          }

          debugLogger.success('visualizer', `Model loaded: ${file.name}`);
        }

        URL.revokeObjectURL(url);
        loadedCount++;
        setLoadProgress((loadedCount / modelFiles.length) * 100);
      } catch (err: any) {
        const errorMsg = `Failed to load ${file.name}: ${err.message}`;
        debugLogger.error('visualizer', errorMsg);
        setError(errorMsg);
      }
    }

    setIsLoading(false);
    debugLogger.success('visualizer', `Loaded ${loadedCount}/${modelFiles.length} model(s)`);
  };

  const handlePositionChange = (axis: number, value: number[]) => {
    const newPosition = [...position] as [number, number, number];
    newPosition[axis] = value[0];
    setPosition(newPosition);
    loadedModelsRef.current.forEach(model => {
      model.position.set(newPosition[0], newPosition[1], newPosition[2]);
    });
  };

  const handleRotationChange = (axis: number, value: number[]) => {
    const newRotation = [...rotation] as [number, number, number];
    newRotation[axis] = value[0];
    setRotation(newRotation);
    loadedModelsRef.current.forEach(model => {
      const rotRad: [number, number, number] = [
        (newRotation[0] * Math.PI) / 180,
        (newRotation[1] * Math.PI) / 180,
        (newRotation[2] * Math.PI) / 180,
      ];
      model.rotation.set(rotRad[0], rotRad[1], rotRad[2]);
    });
  };

  const handleScaleChange = (axis: number, value: number[]) => {
    const newScale = [...scale] as [number, number, number];
    newScale[axis] = value[0];
    setScale(newScale);
    loadedModelsRef.current.forEach(model => {
      model.scale.set(newScale[0], newScale[1], newScale[2]);
    });
  };

  const handleReset = () => {
    const defaultPos = config.transform?.position || [0, 0, 0];
    const defaultRot = config.transform?.rotation || [0, 0, 0];
    const defaultScale = config.transform?.scale || [1, 1, 1];

    setPosition(defaultPos as [number, number, number]);
    setRotation(defaultRot as [number, number, number]);
    setScale(defaultScale as [number, number, number]);

    loadedModelsRef.current.forEach(model => {
      model.position.set(defaultPos[0], defaultPos[1], defaultPos[2]);
      const rotRad: [number, number, number] = [
        (defaultRot[0] * Math.PI) / 180,
        (defaultRot[1] * Math.PI) / 180,
        (defaultRot[2] * Math.PI) / 180,
      ];
      model.rotation.set(rotRad[0], rotRad[1], rotRad[2]);
      model.scale.set(defaultScale[0], defaultScale[1], defaultScale[2]);
    });

    debugLogger.info('visualizer', 'Transform reset to configuration defaults');
  };

  const handleAutoFocus = () => {
    if (loadedModelsRef.current.length === 0 || !cameraRef.current || !controlsRef.current) return;

    const box = new THREE.Box3();
    loadedModelsRef.current.forEach(model => {
      box.expandByObject(model);
    });

    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z);
    const fov = cameraRef.current.fov * (Math.PI / 180);
    let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2));
    cameraZ *= 1.5;

    cameraRef.current.position.set(center.x + cameraZ, center.y + cameraZ, center.z + cameraZ);
    cameraRef.current.lookAt(center);
    controlsRef.current.target.copy(center);
    controlsRef.current.update();

    debugLogger.info('visualizer', 'Camera auto-focused on models');
  };

  return (
    <div className="grid h-full gap-4 lg:grid-cols-[1fr_300px]">
      {/* 3D Viewport */}
      <Card className="flex flex-col overflow-hidden">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>3D Viewport</CardTitle>
              <CardDescription>
                {config.name || 'Unnamed entity'} ({entityType})
              </CardDescription>
            </div>
            <Button variant="outline" size="sm" onClick={handleAutoFocus}>
              <Maximize2 className="mr-2 h-4 w-4" />
              Auto Focus
            </Button>
          </div>
        </CardHeader>
        <CardContent className="flex-1 p-0">
          <div ref={containerRef} className="h-full w-full" />
          {isLoading && (
            <div className="absolute inset-0 flex items-center justify-center bg-background/80">
              <div className="text-center">
                <div className="mb-2 h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
                <p className="text-sm text-muted-foreground">Loading models... {Math.round(loadProgress)}%</p>
              </div>
            </div>
          )}
          {error && (
            <div className="absolute bottom-4 left-4 right-4 rounded-md border border-destructive bg-destructive/10 p-3">
              <p className="text-sm text-destructive">{error}</p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Transform Controls */}
      <Card className="overflow-hidden">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Transform Controls</CardTitle>
            <Button variant="ghost" size="sm" onClick={handleReset}>
              <RotateCcw className="h-4 w-4" />
            </Button>
          </div>
          <CardDescription>Adjust position, rotation, and scale</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Position */}
          <div className="space-y-3">
            <Label className="text-sm font-semibold">Position</Label>
            <div className="space-y-2">
              <div>
                <Label className="text-xs text-muted-foreground">X: {position[0].toFixed(2)}</Label>
                <Slider
                  value={[position[0]]}
                  onValueChange={(v) => handlePositionChange(0, v)}
                  min={-10}
                  max={10}
                  step={0.1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Y: {position[1].toFixed(2)}</Label>
                <Slider
                  value={[position[1]]}
                  onValueChange={(v) => handlePositionChange(1, v)}
                  min={-10}
                  max={10}
                  step={0.1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Z: {position[2].toFixed(2)}</Label>
                <Slider
                  value={[position[2]]}
                  onValueChange={(v) => handlePositionChange(2, v)}
                  min={-10}
                  max={10}
                  step={0.1}
                />
              </div>
            </div>
          </div>

          {/* Rotation */}
          <div className="space-y-3">
            <Label className="text-sm font-semibold">Rotation (degrees)</Label>
            <div className="space-y-2">
              <div>
                <Label className="text-xs text-muted-foreground">X: {rotation[0].toFixed(0)}°</Label>
                <Slider
                  value={[rotation[0]]}
                  onValueChange={(v) => handleRotationChange(0, v)}
                  min={0}
                  max={360}
                  step={1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Y: {rotation[1].toFixed(0)}°</Label>
                <Slider
                  value={[rotation[1]]}
                  onValueChange={(v) => handleRotationChange(1, v)}
                  min={0}
                  max={360}
                  step={1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Z: {rotation[2].toFixed(0)}°</Label>
                <Slider
                  value={[rotation[2]]}
                  onValueChange={(v) => handleRotationChange(2, v)}
                  min={0}
                  max={360}
                  step={1}
                />
              </div>
            </div>
          </div>

          {/* Scale */}
          <div className="space-y-3">
            <Label className="text-sm font-semibold">Scale</Label>
            <div className="space-y-2">
              <div>
                <Label className="text-xs text-muted-foreground">X: {scale[0].toFixed(2)}</Label>
                <Slider
                  value={[scale[0]]}
                  onValueChange={(v) => handleScaleChange(0, v)}
                  min={0.1}
                  max={5}
                  step={0.1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Y: {scale[1].toFixed(2)}</Label>
                <Slider
                  value={[scale[1]]}
                  onValueChange={(v) => handleScaleChange(1, v)}
                  min={0.1}
                  max={5}
                  step={0.1}
                />
              </div>
              <div>
                <Label className="text-xs text-muted-foreground">Z: {scale[2].toFixed(2)}</Label>
                <Slider
                  value={[scale[2]]}
                  onValueChange={(v) => handleScaleChange(2, v)}
                  min={0.1}
                  max={5}
                  step={0.1}
                />
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
