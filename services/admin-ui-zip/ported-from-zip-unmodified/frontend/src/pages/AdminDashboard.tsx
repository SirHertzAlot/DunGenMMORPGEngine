import { useNavigate } from '@tanstack/react-router';
import { useInternetIdentity } from '../hooks/useInternetIdentity';
import { Button } from '@/components/ui/button';
import { Activity, Bug, Grid3x3, ArrowRight, FolderOpen, LogIn, Swords, Eye, FileCode, Database } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

export default function AdminDashboard() {
  const navigate = useNavigate();
  const { identity, isInitializing } = useInternetIdentity();

  const isAuthenticated = !!identity;

  return (
    <div className="flex w-full flex-col items-center justify-center overflow-y-auto p-4 sm:p-6 md:p-8">
      <div className="flex w-full max-w-2xl flex-col items-center space-y-8 text-center sm:space-y-12">
        {/* Large Centered Logo - Responsive sizing */}
        <div className="space-y-4 sm:space-y-6">
          <div className="flex justify-center">
            <div className="flex h-24 w-24 items-center justify-center rounded-2xl border-4 border-primary/30 bg-gradient-to-br from-primary to-accent shadow-2xl sm:h-32 sm:w-32 sm:rounded-3xl">
              <Grid3x3 className="h-12 w-12 text-primary-foreground sm:h-16 sm:w-16" />
            </div>
          </div>
          <h1 className="bg-gradient-to-r from-primary to-accent bg-clip-text text-4xl font-bold tracking-tight text-transparent sm:text-5xl md:text-6xl">
            DunGen
          </h1>
          <p className="text-base text-muted-foreground sm:text-lg md:text-xl">
            Procedural 3D Terrain & Game Object Generation System
          </p>
        </div>

        {/* Offline Mode Alert */}
        <Alert className="w-full">
          <LogIn className="h-4 w-4" />
          <AlertTitle>Offline Mode Active</AlertTitle>
          <AlertDescription>
            Application is running in local-only mode. Some features require backend connection.
            {!isAuthenticated && !isInitializing && (
              <Button
                variant="outline"
                size="sm"
                className="mt-3 w-full"
                onClick={() => navigate({ to: '/login' })}
              >
                <LogIn className="mr-2 h-4 w-4" />
                Sign In
              </Button>
            )}
          </AlertDescription>
        </Alert>

        {/* Navigation Buttons - Touch-friendly */}
        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/generator' })}
          >
            <Grid3x3 className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open 3D Terrain Generator
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Create and manage interactive 3D terrain grids with advanced erosion simulation
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="secondary"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/file-manager' })}
          >
            <FolderOpen className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open File Manager
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Upload and manage 3D assets (.glb, .gltf, .obj, .fbx)
            <span className="text-amber-500"> (requires backend connection)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/game-object-generator' })}
          >
            <Swords className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open Game Object Generator
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Generate NPCs, Items, Dungeons, Quests, and Boss Battles with ECS components
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/visualizer' })}
          >
            <Eye className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open Generic Visualizer
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            YAML-driven 3D entity visualization for characters, NPCs, items, terrain, and dungeons
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/yaml-service' })}
          >
            <FileCode className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open YAML Backend Service
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Generic YAML-based backend responder with request/response simulation and queue management
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/global-tables' })}
          >
            <Database className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open Global Tables Manager
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Manage game object tiers (common, rare, epic, legendary) and world population dynamics
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/observability' })}
          >
            <Activity className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open Observability
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Inspect runtime systems, backend signals, diagnostics, and operational state
            <span className="text-amber-500"> (offline mode)</span>
          </p>
        </div>

        <div className="w-full space-y-3 sm:space-y-4">
          <Button
            size="lg"
            variant="outline"
            className="group h-14 w-full text-base font-semibold shadow-lg transition-all hover:shadow-xl sm:h-16 sm:text-lg"
            onClick={() => navigate({ to: '/diagnostic-logs' })}
          >
            <Bug className="mr-2 h-5 w-5 sm:mr-3 sm:h-6 sm:w-6" />
            Open Diagnostic Logs
            <ArrowRight className="ml-2 h-5 w-5 transition-transform group-hover:translate-x-1 sm:ml-3 sm:h-6 sm:w-6" />
          </Button>
          <p className="text-xs text-muted-foreground sm:text-sm">
            Review authoritative diagnostic entries, source locations, correlation IDs, and failure details
            <span className="text-amber-500"> (requires diagnostic API)</span>
          </p>
        </div>

        {/* Additional Info - Responsive padding */}
        <div className="mt-6 w-full rounded-xl border border-primary/20 bg-primary/5 p-4 sm:mt-8 sm:p-6">
          <h2 className="mb-2 text-base font-semibold sm:text-lg">Features</h2>
          <ul className="space-y-2 text-left text-xs text-muted-foreground sm:text-sm">
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Advanced Perlin noise-based terrain generation</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Five erosion types: hydraulic, thermal, wind, plateau, and river</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Real-time 3D visualization with Three.js</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Grid-based terrain management and export</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>3D asset file management with preview capabilities</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Interactive game object generation with weighted graph traversal</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>ECS-compatible output for NPCs, Items, Dungeons, Quests, and Boss Battles</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>YAML-driven entity visualization with transform controls</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Generic YAML backend responder with queue system simulation</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Global Tables for tier-based game object generation</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
              <span>Secure authentication with Internet Identity</span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
