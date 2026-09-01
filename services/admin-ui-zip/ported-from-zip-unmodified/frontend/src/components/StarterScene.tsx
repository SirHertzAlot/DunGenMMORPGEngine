import {
  Environment,
  Grid,
  OrbitControls,
  PerspectiveCamera,
} from "@react-three/drei";
import { Canvas, useFrame } from "@react-three/fiber";
import { useRef } from "react";
import type * as THREE from "three";

function RotatingBox() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((_state, delta) => {
    if (meshRef.current) {
      meshRef.current.rotation.x += delta * 0.3;
      meshRef.current.rotation.y += delta * 0.5;
    }
  });

  return (
    <mesh ref={meshRef} castShadow receiveShadow>
      <boxGeometry args={[1.5, 1.5, 1.5]} />
      <meshStandardMaterial color="#7c5cbf" metalness={0.3} roughness={0.4} />
    </mesh>
  );
}

function FloatingSphere() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.position.y =
        Math.sin(state.clock.elapsedTime * 0.8) * 0.3 + 0.5;
    }
  });

  return (
    <mesh ref={meshRef} position={[2.5, 0.5, 0]} castShadow>
      <sphereGeometry args={[0.7, 32, 32]} />
      <meshStandardMaterial
        color="#4ade80"
        metalness={0.1}
        roughness={0.3}
        emissive="#1a5c30"
        emissiveIntensity={0.2}
      />
    </mesh>
  );
}

function FloatingTorus() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.x = state.clock.elapsedTime * 0.4;
      meshRef.current.rotation.z = state.clock.elapsedTime * 0.3;
      meshRef.current.position.y =
        Math.cos(state.clock.elapsedTime * 0.6) * 0.3 + 0.5;
    }
  });

  return (
    <mesh ref={meshRef} position={[-2.5, 0.5, 0]} castShadow>
      <torusGeometry args={[0.6, 0.25, 16, 48]} />
      <meshStandardMaterial
        color="#f97316"
        metalness={0.4}
        roughness={0.2}
        emissive="#7c2d12"
        emissiveIntensity={0.2}
      />
    </mesh>
  );
}

function SceneLighting() {
  return (
    <>
      <ambientLight intensity={0.4} />
      <directionalLight
        position={[5, 8, 5]}
        intensity={1.5}
        castShadow
        shadow-mapSize-width={1024}
        shadow-mapSize-height={1024}
        shadow-camera-far={50}
        shadow-camera-left={-10}
        shadow-camera-right={10}
        shadow-camera-top={10}
        shadow-camera-bottom={-10}
      />
      <pointLight position={[-5, 3, -5]} intensity={0.8} color="#7c5cbf" />
      <pointLight position={[5, 3, -5]} intensity={0.6} color="#4ade80" />
    </>
  );
}

export default function StarterScene() {
  return (
    <div className="h-full w-full">
      <Canvas shadows>
        <PerspectiveCamera makeDefault position={[0, 4, 8]} fov={60} />
        <SceneLighting />
        <Environment preset="city" />

        {/* Placeholder geometry objects */}
        <RotatingBox />
        <FloatingSphere />
        <FloatingTorus />

        {/* Ground grid */}
        <Grid
          args={[20, 20]}
          position={[0, -1, 0]}
          cellSize={1}
          cellThickness={0.5}
          cellColor="#4a4a6a"
          sectionSize={5}
          sectionThickness={1}
          sectionColor="#7c5cbf"
          fadeDistance={25}
          fadeStrength={1}
          followCamera={false}
          infiniteGrid
        />

        {/* Ground plane for shadows */}
        <mesh
          rotation={[-Math.PI / 2, 0, 0]}
          position={[0, -1, 0]}
          receiveShadow
        >
          <planeGeometry args={[40, 40]} />
          <meshStandardMaterial color="#1a1a2e" transparent opacity={0.6} />
        </mesh>

        <OrbitControls
          enablePan
          enableZoom
          enableRotate
          minDistance={2}
          maxDistance={30}
          target={[0, 0, 0]}
        />
      </Canvas>
    </div>
  );
}
