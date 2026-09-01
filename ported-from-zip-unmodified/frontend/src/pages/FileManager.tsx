import { useState, useRef, useEffect } from 'react';
import { Upload, Trash2, Download, Search, Grid3x3Icon, List, Eye, Archive, Loader2, AlertCircle, Info } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { toast } from 'sonner';

type ViewMode = 'grid' | 'list';

export default function FileManager() {
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [searchQuery, setSearchQuery] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  return (
    <div className="flex h-full w-full flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border bg-card p-4 sm:p-6">
        <div className="mx-auto max-w-7xl">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h1 className="text-2xl font-bold sm:text-3xl">File Manager</h1>
              <p className="text-sm text-muted-foreground">Manage your 3D assets</p>
            </div>
            <div className="flex gap-2">
              <Button
                onClick={() => fileInputRef.current?.click()}
                className="flex-1 sm:flex-none"
              >
                <Upload className="mr-2 h-4 w-4" />
                Upload Files
              </Button>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                accept=".glb,.gltf,.obj,.fbx,.zip"
                className="hidden"
              />
            </div>
          </div>
        </div>
      </div>

      {/* Offline Mode Info - simplified */}
      <div className="border-b border-border bg-card/95 p-4">
        <div className="mx-auto max-w-7xl">
          <p className="text-sm text-muted-foreground">
            Connect to backend to upload and manage files. The authenticated /admin/files endpoint is available at port 8083.
          </p>
        </div>
      </div>

      {/* Controls */}
      <div className="border-b border-border bg-card/50 p-4">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="relative flex-1 sm:max-w-md">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search files..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-9"
            />
          </div>
          <div className="flex gap-2">
            <Button
              variant={viewMode === 'grid' ? 'default' : 'outline'}
              size="icon"
              onClick={() => setViewMode('grid')}
            >
              <Grid3x3Icon className="h-4 w-4" />
            </Button>
            <Button
              variant={viewMode === 'list' ? 'default' : 'outline'}
              size="icon"
              onClick={() => setViewMode('list')}
            >
              <List className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* File List */}
      <ScrollArea className="flex-1">
        <div className="mx-auto max-w-7xl p-4 sm:p-6">
          <div className="flex h-64 flex-col items-center justify-center gap-2">
            <Upload className="h-12 w-12 text-muted-foreground" />
            <p className="text-muted-foreground">Files will appear here after upload</p>
            <p className="text-xs text-muted-foreground">Use the upload button above to add files</p>
          </div>
        </div>
      </ScrollArea>

      {/* Action Buttons */}
      <div className="border-t border-border bg-card/50 p-4 sm:p-6">
        <div className="flex gap-3 sm:w-full sm:justify-between">
          <Button
            onClick={() => fileInputRef.current?.click()}
            className="flex-1 sm:flex-none"
          >
            <Upload className="mr-2 h-4 w-4" />
            Upload Files
          </Button>
          <Button
            variant="destructive"
          >
            <Trash2 className="mr-2 h-4 w-4" />
            Delete All
          </Button>
        </div>
      </div>
    </div>
  );
}