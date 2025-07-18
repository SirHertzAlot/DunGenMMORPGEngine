
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

import { redisConnection } from '../../config/redis';
import { logger } from '../../logging/logger';

export interface GameEvent {
  id: string;
  type: string;
  playerId: string;
  regionId: string;
  timestamp: number;
  data: any;
  priority?: number;
}

export class GameEventQueue {
  private static instance: GameEventQueue;
  private readonly QUEUE_KEY = 'game_events';
  private readonly PRIORITY_QUEUE_KEY = 'priority_game_events';

  private constructor() {}

  static getInstance(): GameEventQueue {
    if (!GameEventQueue.instance) {
      GameEventQueue.instance = new GameEventQueue();
    }
    return GameEventQueue.instance;
  }

  async publishEvent(event: GameEvent): Promise<void> {
    try {
      const redis = redisConnection.getClient();
      const eventData = JSON.stringify(event);
      
      if (event.priority && event.priority > 5) {
        // High priority events go to priority queue
        await redis.lPush(this.PRIORITY_QUEUE_KEY, eventData);
      } else {
        // Normal events go to regular queue
        await redis.lPush(this.QUEUE_KEY, eventData);
      }

      logger.debug('Event published to queue', { 
        eventId: event.id, 
        type: event.type, 
        priority: event.priority 
      });
    } catch (error) {
      logger.error('Failed to publish event to queue', { 
        error: (error as Error).message, 
        event 
      });
      throw error;
    }
  }

  async subscribeToEvents(callback: (event: GameEvent) => Promise<void>): Promise<void> {
    try {
      const redis = redisConnection.getClient();
      
      logger.info('Starting event queue subscription');
      
      while (true) {
        try {
          // Check priority queue first
          let eventData = await redis.brPop(
            redis.commandOptions({ isolated: true }),
            { key: this.PRIORITY_QUEUE_KEY, timeout: 1 }
          );
          
          // If no priority events, check regular queue
          if (!eventData) {
            eventData = await redis.brPop(
              redis.commandOptions({ isolated: true }),
              { key: this.QUEUE_KEY, timeout: 5 }
            );
          }

          if (eventData) {
            const event: GameEvent = JSON.parse(eventData.element);
            await callback(event);
            
            logger.debug('Event processed from queue', { 
              eventId: event.id, 
              type: event.type 
            });
          }
        } catch (error) {
          logger.error('Error processing event from queue', { 
            error: (error as Error).message 
          });
          // Continue processing other events
        }
      }
    } catch (error) {
      logger.error('Event queue subscription failed', { 
        error: (error as Error).message 
      });
      throw error;
    }
  }

  async getQueueSize(): Promise<{ regular: number; priority: number }> {
    try {
      const redis = redisConnection.getClient();
      const [regular, priority] = await Promise.all([
        redis.lLen(this.QUEUE_KEY),
        redis.lLen(this.PRIORITY_QUEUE_KEY)
      ]);
      
      return { regular, priority };
    } catch (error) {
      logger.error('Failed to get queue size', { error: (error as Error).message });
      throw error;
    }
  }
}

export const gameEventQueue = GameEventQueue.getInstance();
