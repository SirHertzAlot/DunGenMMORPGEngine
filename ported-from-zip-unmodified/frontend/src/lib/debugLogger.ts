export type ConnectionState = 
  | 'offline';

export interface DebugLog {
  id: string;
  timestamp: Date;
  level: 'info' | 'warn' | 'error' | 'success';
  category: string;
  message: string;
  details?: Record<string, any>;
  connectionState?: ConnectionState;
}

type LogSubscriber = (log: DebugLog) => void;

class DebugLogger {
  private logs: DebugLog[] = [];
  private subscribers: LogSubscriber[] = [];
  private currentConnectionState: ConnectionState = 'offline';
  private maxLogs = 500;

  private addLog(log: DebugLog) {
    this.logs.push(log);
    
    if (this.logs.length > this.maxLogs) {
      this.logs = this.logs.slice(-this.maxLogs);
    }

    this.subscribers.forEach(subscriber => subscriber(log));

    const prefix = `[${log.category}]`;
    const message = `${prefix} ${log.message}`;
    const details = log.details ? [log.details] : [];

    switch (log.level) {
      case 'error':
        console.error(message, ...details);
        break;
      case 'warn':
        console.warn(message, ...details);
        break;
      case 'success':
        console.log(`✓ ${message}`, ...details);
        break;
      default:
        console.log(message, ...details);
    }
  }

  info(category: string, message: string, details?: Record<string, any>, connectionState?: ConnectionState) {
    this.addLog({
      id: `${Date.now()}-${Math.random()}`,
      timestamp: new Date(),
      level: 'info',
      category,
      message,
      details,
      connectionState: connectionState || this.currentConnectionState,
    });
  }

  warn(category: string, message: string, details?: Record<string, any>, connectionState?: ConnectionState) {
    this.addLog({
      id: `${Date.now()}-${Math.random()}`,
      timestamp: new Date(),
      level: 'warn',
      category,
      message,
      details,
      connectionState: connectionState || this.currentConnectionState,
    });
  }

  error(category: string, message: string, details?: Record<string, any>, connectionState?: ConnectionState) {
    this.addLog({
      id: `${Date.now()}-${Math.random()}`,
      timestamp: new Date(),
      level: 'error',
      category,
      message,
      details,
      connectionState: connectionState || this.currentConnectionState,
    });
  }

  success(category: string, message: string, details?: Record<string, any>, connectionState?: ConnectionState) {
    this.addLog({
      id: `${Date.now()}-${Math.random()}`,
      timestamp: new Date(),
      level: 'success',
      category,
      message,
      details,
      connectionState: connectionState || this.currentConnectionState,
    });
  }

  getCurrentConnectionState(): ConnectionState {
    return this.currentConnectionState;
  }

  subscribe(subscriber: LogSubscriber): () => void {
    this.subscribers.push(subscriber);
    return () => {
      this.subscribers = this.subscribers.filter(s => s !== subscriber);
    };
  }

  getLogs(): DebugLog[] {
    return [...this.logs];
  }

  clear() {
    this.logs = [];
  }
}

export const debugLogger = new DebugLogger();
