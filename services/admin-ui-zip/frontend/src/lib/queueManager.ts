import { debugLogger } from './debugLogger';

/**
 * Lightweight Publish/Subscribe Queue Manager
 * Decouples frontend and backend runtime communication
 */

export type QueueTopic = 'runtime:command' | 'runtime:state' | 'runtime:metrics' | 'runtime:events';

export interface QueueMessage<T = unknown> {
  id: string;
  topic: QueueTopic;
  timestamp: number;
  data: T;
}

export interface CommandMessage {
  command: 'start' | 'stop' | 'pause' | 'resume' | 'reset' | 'spawn-entity';
  payload?: unknown;
}

export interface StateMessage {
  state: 'stopped' | 'starting' | 'running' | 'stopping';
  tick: number;
  entityCount: number;
}

export interface MetricsMessage {
  uptime: number;
  entitiesSimulated: number;
  avgTickTime: number;
  cpuLoad: number;
  memoryUsage: number;
  connectedPlayers: number;
}

export interface EventMessage {
  eventType: string;
  entityId?: string;
  componentType?: string;
  data?: unknown;
}

type MessageHandler<T = unknown> = (message: QueueMessage<T>) => void;

class QueueManager {
  private subscribers: Map<QueueTopic, Set<MessageHandler>>;
  private messageQueue: QueueMessage[];
  private processingInterval: number | null = null;
  private isProcessing: boolean = false;

  constructor() {
    this.subscribers = new Map();
    this.messageQueue = [];

    // Initialize topic subscriptions
    const topics: QueueTopic[] = ['runtime:command', 'runtime:state', 'runtime:metrics', 'runtime:events'];
    topics.forEach(topic => {
      this.subscribers.set(topic, new Set());
    });

    debugLogger.info('QueueManager', 'Queue system initialized', {
      topics: topics.length,
    });
  }

  /**
   * Publish a message to a topic
   */
  publish<T>(topic: QueueTopic, data: T): void {
    const message: QueueMessage<T> = {
      id: this.generateMessageId(),
      topic,
      timestamp: Date.now(),
      data,
    };

    this.messageQueue.push(message);

    debugLogger.info('QueueManager', `Message published to ${topic}`, {
      messageId: message.id,
      queueSize: this.messageQueue.length,
    });

    // Process immediately if not already processing
    if (!this.isProcessing) {
      this.processQueue();
    }
  }

  /**
   * Subscribe to a topic
   */
  subscribe<T>(topic: QueueTopic, handler: MessageHandler<T>): () => void {
    const handlers = this.subscribers.get(topic);
    if (!handlers) {
      debugLogger.warn('QueueManager', `Topic ${topic} not found`);
      return () => {};
    }

    handlers.add(handler as MessageHandler);

    debugLogger.info('QueueManager', `Subscribed to ${topic}`, {
      subscriberCount: handlers.size,
    });

    // Return unsubscribe function
    return () => {
      handlers.delete(handler as MessageHandler);
      debugLogger.info('QueueManager', `Unsubscribed from ${topic}`, {
        subscriberCount: handlers.size,
      });
    };
  }

  /**
   * Process queued messages
   */
  private processQueue(): void {
    if (this.isProcessing || this.messageQueue.length === 0) {
      return;
    }

    this.isProcessing = true;

    while (this.messageQueue.length > 0) {
      const message = this.messageQueue.shift();
      if (!message) continue;

      const handlers = this.subscribers.get(message.topic);
      if (!handlers || handlers.size === 0) {
        debugLogger.warn('QueueManager', `No subscribers for topic ${message.topic}`);
        continue;
      }

      // Deliver message to all subscribers
      handlers.forEach(handler => {
        try {
          handler(message);
        } catch (error) {
          debugLogger.error('QueueManager', `Handler error for topic ${message.topic}`, {
            error: String(error),
            messageId: message.id,
          });
        }
      });

      debugLogger.info('QueueManager', `Message delivered to ${handlers.size} subscribers`, {
        topic: message.topic,
        messageId: message.id,
      });
    }

    this.isProcessing = false;
  }

  /**
   * Start periodic queue processing
   */
  startProcessing(intervalMs: number = 100): void {
    if (this.processingInterval !== null) {
      debugLogger.warn('QueueManager', 'Queue processing already started');
      return;
    }

    this.processingInterval = window.setInterval(() => {
      this.processQueue();
    }, intervalMs);

    debugLogger.success('QueueManager', 'Queue processing started', {
      intervalMs,
    });
  }

  /**
   * Stop periodic queue processing
   */
  stopProcessing(): void {
    if (this.processingInterval !== null) {
      clearInterval(this.processingInterval);
      this.processingInterval = null;
      debugLogger.info('QueueManager', 'Queue processing stopped');
    }
  }

  /**
   * Get queue statistics
   */
  getStats(): {
    queueSize: number;
    subscriberCounts: Record<QueueTopic, number>;
    isProcessing: boolean;
  } {
    const subscriberCounts: Record<string, number> = {};
    this.subscribers.forEach((handlers, topic) => {
      subscriberCounts[topic] = handlers.size;
    });

    return {
      queueSize: this.messageQueue.length,
      subscriberCounts: subscriberCounts as Record<QueueTopic, number>,
      isProcessing: this.isProcessing,
    };
  }

  /**
   * Clear all messages from queue
   */
  clearQueue(): void {
    const clearedCount = this.messageQueue.length;
    this.messageQueue = [];
    debugLogger.info('QueueManager', `Queue cleared: ${clearedCount} messages removed`);
  }

  /**
   * Clear all subscriptions
   */
  clearSubscriptions(): void {
    this.subscribers.forEach(handlers => handlers.clear());
    debugLogger.info('QueueManager', 'All subscriptions cleared');
  }

  /**
   * Reset queue manager
   */
  reset(): void {
    this.stopProcessing();
    this.clearQueue();
    this.clearSubscriptions();
    debugLogger.info('QueueManager', 'Queue manager reset');
  }

  private generateMessageId(): string {
    return `msg_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
}

// Singleton instance
export const queueManager = new QueueManager();

// Start processing on initialization
queueManager.startProcessing(100);

export default QueueManager;
