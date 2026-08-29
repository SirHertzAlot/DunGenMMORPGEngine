import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { X, RotateCcw, Loader2, AlertCircle } from 'lucide-react';
import type { FileMetadata } from '../backend';
import { debugLogger } from '../lib/debugLogger';

interface ModelPreviewProps {
  file: FileMetadata;
  onClose: () => void;
}

type LoadingStage = 'initialization' | 'download' | 'parse' | 'texture' | 'mesh' | 'render' | 'complete';

const MAX_LOAD_RETRIES = 3;
const RETRY_DELAY_BASE = 1000;

export default function ModelPreview({ file, onClose }: ModelPreviewProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const modelRef = useRef<THREE.Object3D | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const mountedRef = useRef<boolean>(true);
  const sceneInitializedRef = useRef<boolean>(false);
  
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [loadingProgress, setLoadingProgress] = useState(0);
  const [loadingMessage, setLoadingMessage] = useState('Offline mode - model preview unavailable');
  const [retryCount, setRetryCount] = useState(0);
  const [downloadProgress, setDownloadProgress] = useState(0);
  const [currentStage, setCurrentStage] = useState<LoadingStage>('initialization');

  useEffect(() => {
    mountedRef.current = true;
    
    // In offline mode, show error immediately
    setError('Model preview requires backend connection');
    setIsLoading(false);
    debugLogger.warn('ModelPreview', 'Model preview unavailable in offline mode', {
      fileName: file.name,
    }, 'offline');
    
    return () => {
      mountedRef.current = false;
    };
  }, [file]);

  const handleReset = () => {
    debugLogger.info('ModelPreview', 'Reset not available in offline mode', undefined, 'offline');
  };

  const getStageLabel = (stage: LoadingStage): string => {
    switch (stage) {
      case 'initialization': return 'Initialization';
      case 'download': return 'Download';
      case 'parse': return 'Parse';
      case 'texture': return 'Texture';
      case 'mesh': return 'Mesh';
      case 'render': return 'Render';
      case 'complete': return 'Complete';
    }
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-4xl h-[80vh] p-0 flex flex-col">
        <DialogHeader className="p-6 pb-4 shrink-0">
          <div className="flex items-center justify-between">
            <div className="flex-1 min-w-0">
              <DialogTitle className="truncate" title={file.name}>{file.name}</DialogTitle>
              <p className="text-xs text-muted-foreground mt-1">
                {file.fileType} • {(Number(file.size) / 1024 / 1024).toFixed(2)} MB
                {file.relativePath && ` • ${file.relativePath}`}
              </p>
            </div>
            <div className="flex gap-2 ml-4">
              <Button size="sm" variant="outline" onClick={handleReset} disabled>
                <RotateCcw className="h-4 w-4" />
              </Button>
              <Button size="sm" variant="ghost" onClick={onClose}>
                <X className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </DialogHeader>
        
        <div className="relative flex-1 overflow-hidden">
          {error && (
            <div className="absolute inset-0 flex flex-col items-center justify-center bg-background/95 backdrop-blur-sm z-10">
              <div className="text-center max-w-md px-4">
                <AlertCircle className="h-12 w-12 text-destructive mx-auto mb-4" />
                <p className="text-destructive font-medium mb-2">Model preview unavailable</p>
                <p className="text-sm text-muted-foreground mb-4">{error}</p>
                <p className="text-xs text-muted-foreground mb-4">
                  Model preview requires backend connection
                </p>
                <div className="flex gap-2 justify-center">
                  <Button 
                    variant="outline" 
                    size="sm"
                    onClick={onClose}
                  >
                    Close
                  </Button>
                </div>
              </div>
            </div>
          )}
          
          <div ref={containerRef} className="h-full w-full bg-muted/20" />
        </div>
        
        <div className="border-t border-border p-4 text-xs text-muted-foreground shrink-0">
          <div className="flex flex-wrap gap-4">
            <span>• Model preview requires backend connection</span>
            <span>• Connect to backend to view 3D models</span>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
