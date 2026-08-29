// React Three Fiber component for rendering a single wall cube
// Wall cubes are stacked 3 high to create walls

import { memo } from "react";
import * as THREE from "three";
import { CELL_SIZE } from "../../types/dungeon3d";

interface WallCubeProps {
  position: [number, number, number];
  color: string;
}

function WallCube({ position, color }: WallCubeProps) {
  return (
    <mesh position={position} castShadow receiveShadow>
      <boxGeometry args={[CELL_SIZE, CELL_SIZE, CELL_SIZE]} />
      <meshStandardMaterial
        color={new THREE.Color(color)}
        roughness={0.8}
        metalness={0.2}
      />
    </mesh>
  );
}

export default memo(WallCube);
