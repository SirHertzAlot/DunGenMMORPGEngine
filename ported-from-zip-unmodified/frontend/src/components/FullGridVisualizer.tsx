import { Card } from "@/components/ui/card";
import { useEffect, useRef, useState } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { extractCellHeightmap } from "../lib/heightmapGenerator";
import type { GridConfig } from "../types/grid";

interface FullGridVisualizerProps {
  gridConfig: GridConfig;
  onCellClick: (row: number, col: number) => void;
  largeHeightmap: Float32Array | null;
}

export default function FullGridVisualizer({
  gridConfig,
  onCellClick,
  largeHeightmap,
}: FullGridVisualizerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const raycasterRef = useRef<THREE.Raycaster>(new THREE.Raycaster());
  const mouseRef = useRef<THREE.Vector2>(new THREE.Vector2());
  const cellMeshesRef = useRef<THREE.Mesh[][]>([]);

  const [hoveredCell, setHoveredCell] = useState<{
    row: number;
    col: number;
  } | null>(null);

  // Initialize Three.js scene
  useEffect(() => {
    if (!containerRef.current) return;

    const container = containerRef.current;
    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0a0a);
    scene.fog = new THREE.Fog(0x0a0a0a, 100, 400);
    sceneRef.current = scene;

    // Camera
    const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    const gridSize = gridConfig.dimension;
    const cameraDistance = gridSize * 60;
    camera.position.set(
      cameraDistance * 0.7,
      cameraDistance * 0.5,
      cameraDistance * 0.7,
    );
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.minDistance = 50;
    controls.maxDistance = cameraDistance * 2;
    controls.maxPolarAngle = Math.PI / 2 - 0.1;
    controls.touches = {
      ONE: THREE.TOUCH.ROTATE,
      TWO: THREE.TOUCH.DOLLY_PAN,
    };
    controlsRef.current = controls;

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(100, 100, 50);
    directionalLight.castShadow = true;
    directionalLight.shadow.camera.left = -200;
    directionalLight.shadow.camera.right = 200;
    directionalLight.shadow.camera.top = 200;
    directionalLight.shadow.camera.bottom = -200;
    directionalLight.shadow.mapSize.width = 2048;
    directionalLight.shadow.mapSize.height = 2048;
    scene.add(directionalLight);

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
    window.addEventListener("resize", handleResize);

    // Cleanup
    return () => {
      window.removeEventListener("resize", handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      controls.dispose();
      renderer.dispose();
      if (container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [gridConfig.dimension]);

  // Generate terrain meshes using equal-area partitioning from large heightmap
  useEffect(() => {
    if (!sceneRef.current || !largeHeightmap) return;

    const scene = sceneRef.current;
    const { dimension, cells } = gridConfig;

    // Clear existing meshes
    for (const row of cellMeshesRef.current) {
      for (const mesh of row) {
        scene.remove(mesh);
        mesh.geometry.dispose();
        if (Array.isArray(mesh.material)) {
          for (const mat of mesh.material) mat.dispose();
        } else {
          mesh.material.dispose();
        }
      }
    }

    const cellSize = 100;
    const gap = 10;
    const totalSize = dimension * cellSize + (dimension - 1) * gap;
    const offset = totalSize / 2 - cellSize / 2;
    const resolution = 128;

    const newCellMeshes: THREE.Mesh[][] = [];

    cells.forEach((row, rowIndex) => {
      const meshRow: THREE.Mesh[] = [];

      row.forEach((cell, colIndex) => {
        const segments = 64;
        const geometry = new THREE.PlaneGeometry(
          cellSize,
          cellSize,
          segments,
          segments,
        );
        const positions = geometry.attributes.position.array as Float32Array;

        // Extract localized heightmap section for this cell
        const cellHeightmap = extractCellHeightmap(
          largeHeightmap,
          rowIndex,
          colIndex,
          dimension,
          dimension,
          resolution,
        );

        // Apply heightmap to geometry
        const params = cell.noiseSettings;
        for (let i = 0; i < positions.length; i += 3) {
          const localX = positions[i];
          const localY = positions[i + 1];

          // Map geometry position to heightmap coordinates
          const u = localX / cellSize + 0.5;
          const v = localY / cellSize + 0.5;
          const hmX = Math.floor(u * (resolution - 1));
          const hmY = Math.floor(v * (resolution - 1));
          const hmIndex = hmY * resolution + hmX;

          // Get height from heightmap and apply elevation
          const height = cellHeightmap[hmIndex];
          const z = height * params.elevation;
          positions[i + 2] = z;
        }

        // Color the terrain based on height
        const colors = new Float32Array(positions.length);
        for (let i = 0; i < positions.length; i += 3) {
          const z = positions[i + 2];
          const normalizedHeight =
            (z + params.elevation / 2) / params.elevation;

          let color: THREE.Color;
          if (normalizedHeight < 0.3) {
            color = new THREE.Color().setStyle("oklch(0.45 0.15 240)");
          } else if (normalizedHeight < 0.65) {
            const t = (normalizedHeight - 0.3) / 0.35;
            const lightGreen = new THREE.Color().setStyle(
              "oklch(0.75 0.18 145)",
            );
            const darkGreen = new THREE.Color().setStyle(
              "oklch(0.55 0.18 145)",
            );
            color = lightGreen.lerp(darkGreen, t);
          } else {
            const t = (normalizedHeight - 0.65) / 0.35;
            const brown = new THREE.Color().setStyle("oklch(0.55 0.05 70)");
            const gray = new THREE.Color().setStyle("oklch(0.75 0.02 270)");
            color = brown.lerp(gray, t);
          }

          colors[i] = color.r;
          colors[i + 1] = color.g;
          colors[i + 2] = color.b;
        }

        geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
        geometry.computeVertexNormals();

        const material = new THREE.MeshStandardMaterial({
          vertexColors: true,
          flatShading: false,
          roughness: 0.8,
          metalness: 0.2,
        });

        const mesh = new THREE.Mesh(geometry, material);
        mesh.rotation.x = -Math.PI / 2;

        // Position the mesh in the grid
        const x = colIndex * (cellSize + gap) - offset;
        const z = rowIndex * (cellSize + gap) - offset;
        mesh.position.set(x, 0, z);

        mesh.receiveShadow = true;
        mesh.castShadow = true;

        // Store cell coordinates in userData for raycasting
        mesh.userData = { row: rowIndex, col: colIndex };

        scene.add(mesh);
        meshRow.push(mesh);
      });

      newCellMeshes.push(meshRow);
    });

    cellMeshesRef.current = newCellMeshes;
  }, [gridConfig, largeHeightmap]);

  // Handle hover highlighting
  useEffect(() => {
    if (!hoveredCell) {
      // Reset all materials to normal
      for (const row of cellMeshesRef.current) {
        for (const mesh of row) {
          if (mesh.material instanceof THREE.MeshStandardMaterial) {
            mesh.material.emissive.setHex(0x000000);
            mesh.material.emissiveIntensity = 0;
          }
        }
      }
      return;
    }

    // Highlight hovered cell
    for (const row of cellMeshesRef.current) {
      for (const mesh of row) {
        if (mesh.material instanceof THREE.MeshStandardMaterial) {
          if (
            mesh.userData.row === hoveredCell.row &&
            mesh.userData.col === hoveredCell.col
          ) {
            mesh.material.emissive.setHex(0x4488ff);
            mesh.material.emissiveIntensity = 0.3;
          } else {
            mesh.material.emissive.setHex(0x000000);
            mesh.material.emissiveIntensity = 0;
          }
        }
      }
    }
  }, [hoveredCell]);

  // Mouse move handler for hover detection
  const handleMouseMove = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current || !cameraRef.current) return;

    const rect = containerRef.current.getBoundingClientRect();
    mouseRef.current.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouseRef.current.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current);

    const allMeshes = cellMeshesRef.current.flat();
    const intersects = raycasterRef.current.intersectObjects(allMeshes);

    if (intersects.length > 0) {
      const mesh = intersects[0].object as THREE.Mesh;
      setHoveredCell({ row: mesh.userData.row, col: mesh.userData.col });
    } else {
      setHoveredCell(null);
    }
  };

  // Click handler
  const handleClick = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current || !cameraRef.current) return;

    const rect = containerRef.current.getBoundingClientRect();
    mouseRef.current.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouseRef.current.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current);

    const allMeshes = cellMeshesRef.current.flat();
    const intersects = raycasterRef.current.intersectObjects(allMeshes);

    if (intersects.length > 0) {
      const mesh = intersects[0].object as THREE.Mesh;
      onCellClick(mesh.userData.row, mesh.userData.col);
    }
  };

  return (
    <div className="relative h-full w-full overflow-hidden">
      <div
        ref={containerRef}
        className="h-full w-full cursor-pointer touch-none"
        onMouseMove={handleMouseMove}
        onClick={handleClick}
        onKeyDown={(e) => e.key === "Enter" && handleClick(e as never)}
        role="button"
        tabIndex={0}
        aria-label="Terrain grid visualizer"
      />

      {hoveredCell && (
        <Card className="absolute left-4 top-4 bg-card/90 p-3 backdrop-blur-sm">
          <div className="text-sm font-mono">
            <div className="font-semibold">
              Cell [{hoveredCell.row}, {hoveredCell.col}]
            </div>
            <div className="text-xs text-muted-foreground mt-1">
              Click to edit
            </div>
          </div>
        </Card>
      )}

      {!largeHeightmap && (
        <Card className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 bg-card/90 p-4 backdrop-blur-sm">
          <div className="text-sm text-center">
            <div className="font-semibold mb-1">Generating heightmap...</div>
            <div className="text-xs text-muted-foreground">
              Please wait while the terrain is being generated
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}
