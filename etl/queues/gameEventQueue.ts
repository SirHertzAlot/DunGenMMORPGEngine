
import { redis } from '../../config/redis';
import { logger } from '../../logging/logger';

export interface GameEvent {
  id: string;
  type: string;
  playerId: string;
  regionId: string;
  timestamp: number;
  data: any;
  priority: number;
}

export class GameEventQueue {
  private static instance: GameEventQueue;
  private queueName = 'game_events';

  private constructor() {}

  static getInstance(): GameEventQueue {
    if (!GameEventQueue.instance) {
      GameEventQueue.instance = new GameEventQueue();
    }
    return GameEventQueue.instance;
  }

  async enqueue(event: GameEvent): Promise<void> {
    try {
      const publisher = redis.getPublisher();
      const serializedEvent = JSON.stringify(event);
      
      // Add to priority queue
      await publisher.zAdd(this.queueName, {
        score: event.priority,
        value: serializedEvent
      });
      
      // Publish event for real-time processing
      await publisher.publish(`events:${event.type}`, serializedEvent);
      
      logger.info('Game event queued', {
        eventId: event.id,
        eventType: event.type,
        playerId: event.playerId,
        regionId: event.regionId,
        priority: event.priority
      });
    } catch (error) {
      logger.error('Error queuing game event', {
        error: (error as Error).message,
        event
      });
      throw error;
    }
  }

  async dequeue(count: number = 1): Promise<GameEvent[]> {
    try {
      const client = redis.getClient();
      
      // Get highest priority events
      const events = await client.zPopMax(this.queueName, count);
      
      return events.map(event => JSON.parse(event.value));
    } catch (error) {
      logger.error('Error dequeuing game events', {
        error: (error as Error).message,
        count
      });
      throw error;
    }
  }

  async subscribe(eventType: string, callback: (event: GameEvent) => void): Promise<void> {
    try {
      const subscriber = redis.getSubscriber();
      
      await subscriber.subscribe(`events:${eventType}`, (message) => {
        try {
          const event = JSON.parse(message);
          callback(event);
        } catch (error) {
          logger.error('Error processing subscribed event', {
            error: (error as Error).message,
            eventType,
            message
          });
        }
      });
      
      logger.info('Subscribed to game events', { eventType });
    } catch (error) {
      logger.error('Error subscribing to game events', {
        error: (error as Error).message,
        eventType
      });
      throw error;
    }
  }

  async getQueueSize(): Promise<number> {
    try {
      const client = redis.getClient();
      return await client.zCard(this.queueName);
    } catch (error) {
      logger.error('Error getting queue size', {
        error: (error as Error).message
      });
      return 0;
    }
  }
}

export const gameEventQueue = GameEventQueue.getInstance();
