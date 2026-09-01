import { Alert, AlertDescription } from '@/components/ui/alert';
import { WifiOff } from 'lucide-react';

/**
 * Static connection status banner showing offline mode.
 * No backend connection attempts - purely informational.
 */
export default function ConnectionBanner() {
  // Always show offline mode banner
  return (
    <div className="border-b border-border bg-amber-500/10 px-4 py-2">
      <div className="container mx-auto flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <WifiOff className="h-4 w-4 text-amber-500" />
          <span className="text-sm font-medium text-amber-700 dark:text-amber-300">
            Offline Mode: No backend connected — local-only functionality active
          </span>
        </div>
      </div>
    </div>
  );
}
