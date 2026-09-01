import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { generatePerlinNoise } from '../lib/perlinNoise';
import { applyHydraulicErosion, applyThermalErosion, applyWindErosion, applyPlateauErosion, applyRiverErosion } from '../lib/erosion';
import { Progress } from '@/components/ui/progress';
import type { CellConfig } from '../types/grid';
import type { HydraulicErosionParams, ThermalErosionParams, WindErosionParams, PlateauErosionParams, RiverErosionParams } from '../lib/erosion';

interface TerrainSceneProps {
  params: CellConfig['noiseSettings'];
  hydraulicParams: HydraulicErosionParams;
  thermalParams: ThermalErosionParams;
  windParams: WindErosionParams;
  plateauParams: PlateauErosionParams;
  riverParams: RiverErosionParams;
  applyHydraulicTrigger: number;
  applyThermalTrigger: number;
  applyWindTrigger: number;
  applyPlateauTrigger: number;
  applyRiverTrigger: number;
  resetRiverTrigger: number;
}

export default function TerrainScene({ 
  params, 
  hydraulicParams, 
  thermalParams,
  windParams,
  plateauParams,
  riverParams,
  applyHydraulicTrigger,
  applyThermalTrigger,
  applyWindTrigger,
  applyPlateauTrigger,
  applyRiverTrigger,
  resetRiverTrigger
}: TerrainSceneProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const terrainMeshRef = useRef<THREE.Mesh | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const geometryRef = useRef<THREE.PlaneGeometry | null>(null);
  const riverDataRef = useRef<{ waterDepth: Float32Array; flowVectors: Float32Array; poolingBasins: Float32Array } | null>(null);

  const [isApplyingErosion, setIsApplyingErosion] = useState(false);
  const [erosionProgress, setErosionProgress] = useState(0);
  const [erosionType, setErosionType] = useState<'hydraulic' | 'thermal' | 'wind' | 'plateau' | 'river'>('hydraulic');

  // Initialize Three.js scene
  useEffect(() => {
    if (!containerRef.current) return;

    const container = containerRef.current;
    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0a0a);
    scene.fog = new THREE.Fog(0x0a0a0a, 50, 200);
    sceneRef.current = scene;

    // Camera
    const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    camera.position.set(40, 30, 40);
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Controls with enhanced touch support
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.minDistance = 20;
    controls.maxDistance = 150;
    controls.maxPolarAngle = Math.PI / 2 - 0.1;
    // Enhanced touch controls
    controls.touches = {
      ONE: THREE.TOUCH.ROTATE,
      TWO: THREE.TOUCH.DOLLY_PAN
    };
    controls.enableZoom = true;
    controls.zoomSpeed = 1.2;
    controls.rotateSpeed = 0.8;
    controls.panSpeed = 0.8;
    controlsRef.current = controls;

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(50, 50, 25);
    directionalLight.castShadow = true;
    directionalLight.shadow.camera.left = -50;
    directionalLight.shadow.camera.right = 50;
    directionalLight.shadow.camera.top = 50;
    directionalLight.shadow.camera.bottom = -50;
    directionalLight.shadow.mapSize.width = 2048;
    directionalLight.shadow.mapSize.height = 2048;
    scene.add(directionalLight);

    // Hemisphere light for better color
    const hemisphereLight = new THREE.HemisphereLight(0x87ceeb, 0x362312, 0.3);
    scene.add(hemisphereLight);

    // Animation loop
    const animate = () => {
      animationFrameRef.current = requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    // Handle resize
    const handleResize = () => {
      if (!container) return;
      const newWidth = container.clientWidth;
      const newHeight = container.clientHeight;
      
      if (newWidth > 0 && newHeight > 0) {
        camera.aspect = newWidth / newHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(newWidth, newHeight);
      }
    };
    window.addEventListener('resize', handleResize);

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      controls.dispose();
      renderer.dispose();
      if (container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, []);

  // Update terrain when params change
  useEffect(() => {
    if (!sceneRef.current) return;

    const scene = sceneRef.current;

    // Remove old terrain
    if (terrainMeshRef.current) {
      scene.remove(terrainMeshRef.current);
      terrainMeshRef.current.geometry.dispose();
      if (Array.isArray(terrainMeshRef.current.material)) {
        terrainMeshRef.current.material.forEach((mat) => mat.dispose());
      } else {
        terrainMeshRef.current.material.dispose();
      }
    }

    // Reset river data when terrain regenerates
    riverDataRef.current = null;

    // Create terrain geometry
    const size = 100;
    const segments = 128;
    const geometry = new THREE.PlaneGeometry(size, size, segments, segments);
    geometryRef.current = geometry;

    // Generate height map using Perlin noise
    const positions = geometry.attributes.position.array as Float32Array;

    for (let i = 0; i < positions.length; i += 3) {
      const x = positions[i];
      const y = positions[i + 1];

      // Generate height using Perlin noise with octaves
      let height = 0;
      let amplitude = 1;
      let frequency = params.noiseScale;
      let maxValue = 0;

      for (let octave = 0; octave < params.octaves; octave++) {
        height += generatePerlinNoise(x * frequency, y * frequency) * amplitude;
        maxValue += amplitude;
        amplitude *= params.persistence;
        frequency *= 2;
      }

      // Normalize height
      height = height / maxValue;
      
      // Apply elevation multiplier
      const z = height * params.elevation;
      positions[i + 2] = z;
    }

    updateTerrainColors(geometry, params.elevation);
    geometry.computeVertexNormals();

    // Create material
    const material = new THREE.MeshStandardMaterial({
      vertexColors: true,
      flatShading: false,
      roughness: 0.8,
      metalness: 0.2,
    });

    // Create mesh
    const terrain = new THREE.Mesh(geometry, material);
    terrain.rotation.x = -Math.PI / 2;
    terrain.receiveShadow = true;
    terrain.castShadow = true;
    scene.add(terrain);
    terrainMeshRef.current = terrain;
  }, [params]);

  // Apply hydraulic erosion when triggered
  useEffect(() => {
    if (applyHydraulicTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    const applyErosion = async () => {
      setErosionType('hydraulic');
      setIsApplyingErosion(true);
      setErosionProgress(0);

      await new Promise(resolve => setTimeout(resolve, 50));

      const geometry = geometryRef.current!;
      const positions = geometry.attributes.position.array as Float32Array;
      const segments = 128;

      const clonedPositions = new Float32Array(positions);

      applyHydraulicErosion(clonedPositions, segments, hydraulicParams, (progress) => {
        setErosionProgress(progress * 100);
      });

      for (let i = 0; i < positions.length; i += 3) {
        positions[i + 2] = clonedPositions[i + 2];
      }

      updateTerrainColors(geometry, params.elevation);
      geometry.computeVertexNormals();
      geometry.attributes.position.needsUpdate = true;
      geometry.attributes.color.needsUpdate = true;

      setErosionProgress(100);
      await new Promise(resolve => setTimeout(resolve, 300));
      setIsApplyingErosion(false);
    };

    applyErosion();
  }, [applyHydraulicTrigger, hydraulicParams, params.elevation]);

  // Apply thermal erosion when triggered
  useEffect(() => {
    if (applyThermalTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    const applyErosion = async () => {
      setErosionType('thermal');
      setIsApplyingErosion(true);
      setErosionProgress(0);

      await new Promise(resolve => setTimeout(resolve, 50));

      const geometry = geometryRef.current!;
      const positions = geometry.attributes.position.array as Float32Array;
      const segments = 128;

      const clonedPositions = new Float32Array(positions);

      applyThermalErosion(clonedPositions, segments, thermalParams, (progress) => {
        setErosionProgress(progress * 100);
      });

      for (let i = 0; i < positions.length; i += 3) {
        positions[i + 2] = clonedPositions[i + 2];
      }

      updateTerrainColors(geometry, params.elevation);
      geometry.computeVertexNormals();
      geometry.attributes.position.needsUpdate = true;
      geometry.attributes.color.needsUpdate = true;

      setErosionProgress(100);
      await new Promise(resolve => setTimeout(resolve, 300));
      setIsApplyingErosion(false);
    };

    applyErosion();
  }, [applyThermalTrigger, thermalParams, params.elevation]);

  // Apply wind erosion when triggered
  useEffect(() => {
    if (applyWindTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    const applyErosion = async () => {
      setErosionType('wind');
      setIsApplyingErosion(true);
      setErosionProgress(0);

      await new Promise(resolve => setTimeout(resolve, 50));

      const geometry = geometryRef.current!;
      const positions = geometry.attributes.position.array as Float32Array;
      const segments = 128;

      const clonedPositions = new Float32Array(positions);

      applyWindErosion(clonedPositions, segments, windParams, (progress) => {
        setErosionProgress(progress * 100);
      });

      for (let i = 0; i < positions.length; i += 3) {
        positions[i + 2] = clonedPositions[i + 2];
      }

      updateTerrainColors(geometry, params.elevation);
      geometry.computeVertexNormals();
      geometry.attributes.position.needsUpdate = true;
      geometry.attributes.color.needsUpdate = true;

      setErosionProgress(100);
      await new Promise(resolve => setTimeout(resolve, 300));
      setIsApplyingErosion(false);
    };

    applyErosion();
  }, [applyWindTrigger, windParams, params.elevation]);

  // Apply plateau erosion when triggered
  useEffect(() => {
    if (applyPlateauTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    const applyErosion = async () => {
      setErosionType('plateau');
      setIsApplyingErosion(true);
      setErosionProgress(0);

      await new Promise(resolve => setTimeout(resolve, 50));

      const geometry = geometryRef.current!;
      const positions = geometry.attributes.position.array as Float32Array;
      const segments = 128;

      const clonedPositions = new Float32Array(positions);

      applyPlateauErosion(clonedPositions, segments, plateauParams, params.elevation, (progress) => {
        setErosionProgress(progress * 100);
      });

      for (let i = 0; i < positions.length; i += 3) {
        positions[i + 2] = clonedPositions[i + 2];
      }

      updateTerrainColors(geometry, params.elevation);
      geometry.computeVertexNormals();
      geometry.attributes.position.needsUpdate = true;
      geometry.attributes.color.needsUpdate = true;

      setErosionProgress(100);
      await new Promise(resolve => setTimeout(resolve, 300));
      setIsApplyingErosion(false);
    };

    applyErosion();
  }, [applyPlateauTrigger, plateauParams, params.elevation]);

  // Apply river erosion when triggered
  useEffect(() => {
    if (applyRiverTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    const applyErosion = async () => {
      setErosionType('river');
      setIsApplyingErosion(true);
      setErosionProgress(0);

      await new Promise(resolve => setTimeout(resolve, 50));

      const geometry = geometryRef.current!;
      const positions = geometry.attributes.position.array as Float32Array;
      const segments = 128;

      const clonedPositions = new Float32Array(positions);

      const riverData = applyRiverErosion(clonedPositions, segments, riverParams, (progress) => {
        setErosionProgress(progress * 100);
      });

      // Store river data for export
      riverDataRef.current = riverData;

      for (let i = 0; i < positions.length; i += 3) {
        positions[i + 2] = clonedPositions[i + 2];
      }

      updateTerrainColors(geometry, params.elevation, riverData);
      geometry.computeVertexNormals();
      geometry.attributes.position.needsUpdate = true;
      geometry.attributes.color.needsUpdate = true;

      setErosionProgress(100);
      await new Promise(resolve => setTimeout(resolve, 300));
      setIsApplyingErosion(false);
    };

    applyErosion();
  }, [applyRiverTrigger, riverParams, params.elevation]);

  // Reset river erosion when triggered
  useEffect(() => {
    if (resetRiverTrigger === 0 || !geometryRef.current || !terrainMeshRef.current) return;

    riverDataRef.current = null;
    const geometry = geometryRef.current;
    updateTerrainColors(geometry, params.elevation);
    geometry.attributes.color.needsUpdate = true;
  }, [resetRiverTrigger, params.elevation]);

  return (
    <div className="relative h-full w-full overflow-hidden touch-none">
      <div ref={containerRef} className="h-full w-full" />

      {isApplyingErosion && (
        <div className="absolute inset-0 flex items-center justify-center bg-background/80 backdrop-blur-sm">
          <div className="w-80 max-w-[90%] space-y-3 rounded-lg border border-border bg-card p-4 shadow-lg sm:space-y-4 sm:p-6">
            <div className="space-y-2">
              <h3 className="text-base font-semibold sm:text-lg">
                Applying {erosionType.charAt(0).toUpperCase() + erosionType.slice(1)} Erosion
              </h3>
              <p className="text-xs text-muted-foreground sm:text-sm">
                Simulating {
                  erosionType === 'hydraulic' ? hydraulicParams.iterations :
                  erosionType === 'thermal' ? thermalParams.iterations :
                  erosionType === 'wind' ? windParams.iterations :
                  erosionType === 'plateau' ? plateauParams.iterations :
                  riverParams.iterations
                } iterations...
              </p>
            </div>
            <Progress value={erosionProgress} className="w-full" />
            <p className="text-center text-xs font-mono text-muted-foreground sm:text-sm">
              {Math.round(erosionProgress)}%
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

function updateTerrainColors(
  geometry: THREE.PlaneGeometry, 
  elevation: number,
  riverData?: { waterDepth: Float32Array; flowVectors: Float32Array; poolingBasins: Float32Array } | null
) {
  const positions = geometry.attributes.position.array as Float32Array;
  const colors = new Float32Array(positions.length);

  for (let i = 0; i < positions.length; i += 3) {
    const z = positions[i + 2];
    const vertexIndex = i / 3;

    // Color based on height
    let color: THREE.Color;
    const normalizedHeight = (z + elevation / 2) / elevation;

    // Check if this vertex has river/water data
    const hasRiverData = riverData && riverData.waterDepth[vertexIndex] > 0.1;
    const isPooling = riverData && riverData.poolingBasins[vertexIndex] > 0.5;

    if (hasRiverData) {
      // River or lake water - deeper blue for pooling areas
      if (isPooling) {
        color = new THREE.Color().setStyle('oklch(0.40 0.18 240)'); // Deep blue for lakes
      } else {
        color = new THREE.Color().setStyle('oklch(0.50 0.16 240)'); // River blue
      }
    } else if (normalizedHeight < 0.3) {
      // Water - blue
      color = new THREE.Color().setStyle('oklch(0.45 0.15 240)');
    } else if (normalizedHeight < 0.65) {
      // Grass - green
      const t = (normalizedHeight - 0.3) / 0.35;
      const lightGreen = new THREE.Color().setStyle('oklch(0.75 0.18 145)');
      const darkGreen = new THREE.Color().setStyle('oklch(0.55 0.18 145)');
      color = lightGreen.lerp(darkGreen, t);
    } else {
      // Mountains - gray/brown
      const t = (normalizedHeight - 0.65) / 0.35;
      const brown = new THREE.Color().setStyle('oklch(0.55 0.05 70)');
      const gray = new THREE.Color().setStyle('oklch(0.75 0.02 270)');
      color = brown.lerp(gray, t);
    }

    colors[i] = color.r;
    colors[i + 1] = color.g;
    colors[i + 2] = color.b;
  }

  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}
