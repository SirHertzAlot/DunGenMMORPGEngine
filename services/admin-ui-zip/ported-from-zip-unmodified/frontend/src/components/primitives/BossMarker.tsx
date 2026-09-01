// React Three Fiber component for rendering a boss room marker
// Distinct from SpawnMarker: uses amber/gold color with skull-like visual

import { useFrame } from "@react-three/fiber";
import { memo, useRef } from "react";
import * as THREE from "three";

interface BossMarkerProps {
  position: [number, number, number];
}

function BossMarker({ position }: BossMarkerProps) {
  const groupRef = useRef<THREE.Group>(null);
  const color = new THREE.Color("#f59e0b"); // amber-500
  const darkColor = new THREE.Color("#92400e"); // amber-800

  useFrame((_, delta) => {
    if (groupRef.current) {
      groupRef.current.rotation.y += delta * 0.8;
    }
  });

  return (
    <group position={position}>
      {/* Rotating group for the decorative elements */}
      <group ref={groupRef}>
        {/* Outer spiky ring (octahedron-like) */}
        <mesh>
          <octahedronGeometry args={[6, 0]} />
          <meshStandardMaterial
            color={color}
            emissive={color}
            emissiveIntensity={0.5}
            roughness={0.2}
            metalness={0.8}
            wireframe
          />
        </mesh>
      </group>

      {/* Main sphere (non-rotating) */}
      <mesh castShadow>
        <sphereGeometry args={[4.5, 16, 16]} />
        <meshStandardMaterial
          color={darkColor}
          emissive={color}
          emissiveIntensity={0.4}
          roughness={0.3}
          metalness={0.6}
        />
      </mesh>

      {/* Torus ring */}
      <mesh rotation={[Math.PI / 2, 0, 0]}>
        <torusGeometry args={[6.5, 0.7, 8, 24]} />
        <meshStandardMaterial
          color={color}
          emissive={color}
          emissiveIntensity={0.9}
          roughness={0.1}
          metalness={0.7}
          transparent
          opacity={0.85}
        />
      </mesh>

      {/* Point light for amber glow */}
      <pointLight color="#f59e0b" intensity={1.2} distance={50} decay={2} />
    </group>
  );
}

export default memo(BossMarker);
