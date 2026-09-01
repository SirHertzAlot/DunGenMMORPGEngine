import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Box, Cpu, Layers, Loader2, Upload } from "lucide-react";
import { Suspense } from "react";
import StarterScene from "../components/StarterScene";

function SceneLoader() {
  return (
    <div className="flex h-full w-full items-center justify-center bg-card">
      <div className="flex flex-col items-center gap-3 text-muted-foreground">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
        <p className="text-sm">Loading 3D scene...</p>
      </div>
    </div>
  );
}

export default function ModelStudioPage() {
  return (
    <div className="flex h-[calc(100vh-3.5rem)] flex-col overflow-hidden sm:h-[calc(100vh-4rem)]">
      {/* Page Header */}
      <div className="flex shrink-0 items-center justify-between border-b border-border bg-card px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent">
            <Box className="h-5 w-5 text-primary-foreground" />
          </div>
          <div>
            <h1 className="text-lg font-bold tracking-tight sm:text-xl">
              Model Studio
            </h1>
            <p className="text-xs text-muted-foreground">
              3D model loading &amp; manipulation workspace
            </p>
          </div>
        </div>
        <Badge variant="secondary" className="hidden sm:inline-flex">
          Preview Mode
        </Badge>
      </div>

      {/* Main Content */}
      <div className="flex flex-1 overflow-hidden">
        {/* 3D Viewport */}
        <div className="relative flex-1 overflow-hidden bg-[#0d0d1a]">
          <Suspense fallback={<SceneLoader />}>
            <StarterScene />
          </Suspense>

          {/* Overlay hint */}
          <div className="pointer-events-none absolute bottom-4 left-1/2 -translate-x-1/2">
            <div className="rounded-full border border-border/50 bg-card/80 px-4 py-1.5 text-xs text-muted-foreground backdrop-blur-sm">
              Left drag to orbit · Right drag to pan · Scroll to zoom
            </div>
          </div>
        </div>

        {/* Side Panel */}
        <aside className="hidden w-72 shrink-0 flex-col gap-3 overflow-y-auto border-l border-border bg-card p-4 xl:flex">
          <h2 className="text-sm font-semibold text-foreground">Scene Info</h2>

          <Card className="border-border/60">
            <CardHeader className="pb-2 pt-3 px-3">
              <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                Current Scene
              </CardTitle>
            </CardHeader>
            <CardContent className="px-3 pb-3">
              <div className="space-y-2 text-sm">
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Objects</span>
                  <span className="font-medium">3 primitives</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Lights</span>
                  <span className="font-medium">4 lights</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Renderer</span>
                  <span className="font-medium">Three.js / R3F</span>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="border-dashed border-border/60">
            <CardHeader className="pb-2 pt-3 px-3">
              <CardTitle className="flex items-center gap-2 text-xs font-medium text-muted-foreground uppercase tracking-wider">
                <Upload className="h-3.5 w-3.5" />
                FBX Model Loading
              </CardTitle>
              <CardDescription className="text-xs">
                Coming soon — upload FBX files to load and manipulate 3D models
                in this workspace.
              </CardDescription>
            </CardHeader>
          </Card>

          <Card className="border-dashed border-border/60">
            <CardHeader className="pb-2 pt-3 px-3">
              <CardTitle className="flex items-center gap-2 text-xs font-medium text-muted-foreground uppercase tracking-wider">
                <Layers className="h-3.5 w-3.5" />
                Scene Hierarchy
              </CardTitle>
              <CardDescription className="text-xs">
                Scene graph and object hierarchy panel — available once models
                are loaded.
              </CardDescription>
            </CardHeader>
          </Card>

          <Card className="border-dashed border-border/60">
            <CardHeader className="pb-2 pt-3 px-3">
              <CardTitle className="flex items-center gap-2 text-xs font-medium text-muted-foreground uppercase tracking-wider">
                <Cpu className="h-3.5 w-3.5" />
                Transform Controls
              </CardTitle>
              <CardDescription className="text-xs">
                Position, rotation, and scale gizmos for model manipulation —
                coming in a future update.
              </CardDescription>
            </CardHeader>
          </Card>

          <div className="mt-auto rounded-lg border border-border/40 bg-muted/30 p-3">
            <p className="text-xs text-muted-foreground">
              <span className="font-medium text-foreground">Tip:</span> Use
              OrbitControls to explore the scene. FBX model support will be
              added once files are uploaded to the File Manager.
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}
