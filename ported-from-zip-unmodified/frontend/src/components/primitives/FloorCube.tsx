// React Three Fiber component for rendering a single floor cube
// Floor cubes are positioned so their top face is at ground level (y=0)

import { memo } from "react";
import * as THREE from "three";
import { CELL_SIZE } from "../../types/dungeon3d";

interface FloorCubeProps {
  position: [number, number, number];
  color: string;
}

function FloorCube({ position, color }: FloorCubeProps) {
  return (
    <mesh position={position} receiveShadow>
      <boxGeometry args={[CELL_SIZE, CELL_SIZE, CELL_SIZE]} />
      <meshStandardMaterial
        color={new THREE.Color(color)}
        roughness={0.9}
        metalness={0.1}
      />
    </mesh>
  );
}

export default memo(FloorCube);
