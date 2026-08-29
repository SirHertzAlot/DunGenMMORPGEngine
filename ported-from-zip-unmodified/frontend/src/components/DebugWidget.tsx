import { useState, useEffect, useRef } from 'react';
import { debugLogger, type DebugLog } from '@/lib/debugLogger';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { 
  Bug, 
  X, 
  Trash2,
  Info,
  AlertTriangle,
  XCircle,
  CheckCircle,
  Clock,
  WifiOff,
  Maximize2,
  Minimize2
} from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * Debug widget displaying static startup logs for offline mode.
 * Shows local-only mode status without any backend connection attempts.
 */
export default function DebugWidget() {
  const [logs, setLogs] = useState<DebugLog[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const [autoScroll, setAutoScroll] = useState(true);

  useEffect(() => {
    setLogs(debugLogger.getLogs());

    const unsubscribe = debugLogger.subscribe((log) => {
      setLogs(prev => [...prev, log]);
    });

    return () => {
      unsubscribe();
    };
  }, []);

  useEffect(() => {
    if (autoScroll && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [logs, autoScroll]);

  const handleClear = () => {
    debugLogger.clear();
    setLogs([]);
  };

  const getLogIcon = (level: DebugLog['level']) => {
    switch (level) {
      case 'error':
        return <XCircle className="h-4 w-4 text-destructive" />;
      case 'warn':
        return <AlertTriangle className="h-4 w-4 text-amber-500" />;
      case 'success':
        return <CheckCircle className="h-4 w-4 text-green-500" />;
      default:
        return <Info className="h-4 w-4 text-blue-500" />;
    }
  };

  const getLogBadgeVariant = (level: DebugLog['level']) => {
    switch (level) {
      case 'error':
        return 'destructive';
      case 'warn':
        return 'outline';
      case 'success':
        return 'default';
      default:
        return 'secondary';
    }
  };

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString('en-US', { 
      hour12: false, 
      hour: '2-digit', 
      minute: '2-digit', 
      second: '2-digit',
      fractionalSecondDigits: 3
    });
  };

  if (!isOpen) {
    return (
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 animate-in fade-in slide-in-from-bottom-2 duration-300">
        {/* Offline Status Badge */}
        <div className="flex items-center gap-2 rounded-full border-2 border-amber-500/30 bg-amber-500/20 px-3 py-2 shadow-lg backdrop-blur-sm transition-all duration-300 text-amber-700 dark:text-amber-300">
          <WifiOff className="h-5 w-5" />
          <span className="text-xs font-semibold">Offline Mode</span>
        </div>
        
        {/* Debug Button */}
        <Button
          onClick={() => setIsOpen(true)}
          size="icon"
          variant="outline"
          className="h-12 w-12 rounded-full shadow-lg border-2 hover:scale-105 transition-transform"
          title="Open Debug Console"
        >
          <Bug className="h-5 w-5" />
        </Button>
      </div>
    );
  }

  return (
    <Card 
      className={cn(
        "fixed bottom-4 right-4 z-50 shadow-2xl transition-all duration-300 border-2 animate-in fade-in zoom-in-95",
        isExpanded ? "w-[800px] h-[750px]" : "w-[500px] h-[550px]"
      )}
    >
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3 border-b-2">
        <div className="flex items-center gap-3 flex-1 min-w-0">
          <Bug className="h-5 w-5 text-primary shrink-0" />
          <CardTitle className="text-base font-bold">Debug Console</CardTitle>
          <Badge variant="secondary" className="text-xs font-mono">
            {logs.length}
          </Badge>
          
          {/* Offline Mode Badge */}
          <div className="flex items-center gap-1.5 rounded-full border-2 border-amber-500/30 bg-amber-500/20 px-2.5 py-1 text-xs font-bold transition-all text-amber-700 dark:text-amber-300">
            <WifiOff className="h-4 w-4" />
            <span>Offline</span>
          </div>
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <Button
            onClick={() => setIsExpanded(!isExpanded)}
            size="icon"
            variant="ghost"
            className="h-8 w-8"
            title={isExpanded ? "Minimize" : "Maximize"}
          >
            {isExpanded ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
          </Button>
          <Button
            onClick={handleClear}
            size="icon"
            variant="ghost"
            className="h-8 w-8"
            title="Clear logs"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
          <Button
            onClick={() => setIsOpen(false)}
            size="icon"
            variant="ghost"
            className="h-8 w-8"
            title="Close"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent className="p-0 h-[calc(100%-4rem)]">
        {/* Status Bar */}
        <div className="border-b-2 bg-muted/40 px-4 py-2.5">
          <div className="flex items-center justify-between text-xs">
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-1.5">
                <WifiOff className="h-3.5 w-3.5 text-amber-500" />
                <span className="font-semibold text-foreground">
                  Offline Mode
                </span>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="text-muted-foreground font-medium">
                  Local-only functionality
                </span>
              </div>
            </div>
            <Badge variant="default" className="text-xs font-mono border-2 border-amber-500/50 bg-amber-500/20 text-amber-700 dark:text-amber-300">
              <WifiOff className="h-3 w-3 mr-1" />
              No Backend
            </Badge>
          </div>
        </div>

        <ScrollArea 
          className="h-[calc(100%-3.5rem)] p-4" 
          ref={scrollRef}
          onScroll={(e) => {
            const target = e.target as HTMLDivElement;
            const isAtBottom = target.scrollHeight - target.scrollTop <= target.clientHeight + 50;
            setAutoScroll(isAtBottom);
          }}
        >
          {logs.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
              <Bug className="h-12 w-12 mb-3 opacity-50" />
              <p className="text-sm font-medium">No logs yet</p>
              <p className="text-xs mt-1">Application events will appear here</p>
            </div>
          ) : (
            <div className="space-y-2">
              {logs.map((log) => (
                <div
                  key={log.id}
                  className={cn(
                    "rounded-lg border-2 p-3 text-sm transition-all hover:shadow-md",
                    log.level === 'error' && "border-destructive/50 bg-destructive/5",
                    log.level === 'warn' && "border-amber-500/50 bg-amber-500/5",
                    log.level === 'success' && "border-green-500/50 bg-green-500/5",
                    log.level === 'info' && "border-border bg-muted/30"
                  )}
                >
                  <div className="flex items-start gap-2">
                    <div className="mt-0.5 shrink-0">{getLogIcon(log.level)}</div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1.5 flex-wrap">
                        <Badge 
                          variant={getLogBadgeVariant(log.level)}
                          className="text-xs font-mono font-bold"
                        >
                          {log.category}
                        </Badge>
                        <div className="flex items-center gap-1 text-xs text-muted-foreground">
                          <Clock className="h-3 w-3" />
                          <span className="font-mono font-medium">{formatTime(log.timestamp)}</span>
                        </div>
                      </div>
                      <p className="text-sm leading-relaxed break-words font-medium">
                        {log.message}
                      </p>
                      {log.details && Object.keys(log.details).length > 0 && (
                        <details className="mt-2">
                          <summary className="cursor-pointer text-xs text-muted-foreground hover:text-foreground font-medium">
                            View details ({Object.keys(log.details).length} items)
                          </summary>
                          <pre className="mt-2 p-2 bg-muted rounded text-xs overflow-x-auto font-mono border">
                            {JSON.stringify(log.details, null, 2)}
                          </pre>
                        </details>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
