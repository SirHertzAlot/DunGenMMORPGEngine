// React Three Fiber component for rendering a spawn point marker sphere
// Elevated above floor level with emissive glow for visibility

import { memo } from "react";
import * as THREE from "three";

interface SpawnMarkerProps {
  position: [number, number, number];
  color: string; // hex or CSS color string
  label?: string;
}

function SpawnMarker({ position, color }: SpawnMarkerProps) {
  const threeColor = new THREE.Color(color);

  return (
    <group position={position}>
      {/* Main sphere */}
      <mesh castShadow>
        <sphereGeometry args={[4, 16, 16]} />
        <meshStandardMaterial
          color={threeColor}
          emissive={threeColor}
          emissiveIntensity={0.6}
          roughness={0.3}
          metalness={0.4}
        />
      </mesh>
      {/* Outer glow ring */}
      <mesh>
        <torusGeometry args={[5.5, 0.6, 8, 24]} />
        <meshStandardMaterial
          color={threeColor}
          emissive={threeColor}
          emissiveIntensity={0.8}
          roughness={0.2}
          metalness={0.5}
          transparent
          opacity={0.7}
        />
      </mesh>
      {/* Point light for local glow effect */}
      <pointLight color={color} intensity={0.8} distance={40} decay={2} />
    </group>
  );
}

export default memo(SpawnMarker);
