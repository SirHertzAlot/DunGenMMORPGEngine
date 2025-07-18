
import { createClient, RedisClientType } from 'redis';
import { logger } from '../logging/logger';

export class RedisConnection {
  private static instance: RedisConnection;
  private client: RedisClientType | null = null;
  private subscriber: RedisClientType | null = null;
  private publisher: RedisClientType | null = null;

  private constructor() {}

  static getInstance(): RedisConnection {
    if (!RedisConnection.instance) {
      RedisConnection.instance = new RedisConnection();
    }
    return RedisConnection.instance;
  }

  async connect(): Promise<void> {
    try {
      const redisUrl = process.env.REDIS_URL || 'redis://localhost:6379';
      
      // Main client for general operations
      this.client = createClient({ url: redisUrl });
      
      // Dedicated clients for pub/sub
      this.subscriber = createClient({ url: redisUrl });
      this.publisher = createClient({ url: redisUrl });

      // Connect all clients
      await Promise.all([
        this.client.connect(),
        this.subscriber.connect(),
        this.publisher.connect()
      ]);

      logger.info('Redis connected successfully', { url: redisUrl });

      // Handle connection events
      this.client.on('error', (error) => {
        logger.error('Redis client error', { error: error.message });
      });

      this.subscriber.on('error', (error) => {
        logger.error('Redis subscriber error', { error: error.message });
      });

      this.publisher.on('error', (error) => {
        logger.error('Redis publisher error', { error: error.message });
      });

    } catch (error) {
      logger.error('Redis connection failed', { error: (error as Error).message });
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    try {
      if (this.client) await this.client.disconnect();
      if (this.subscriber) await this.subscriber.disconnect();
      if (this.publisher) await this.publisher.disconnect();
      
      logger.info('Redis disconnected');
    } catch (error) {
      logger.error('Error disconnecting from Redis', { error: (error as Error).message });
      throw error;
    }
  }

  getClient(): RedisClientType {
    if (!this.client) {
      throw new Error('Redis client not connected');
    }
    return this.client;
  }

  getSubscriber(): RedisClientType {
    if (!this.subscriber) {
      throw new Error('Redis subscriber not connected');
    }
    return this.subscriber;
  }

  getPublisher(): RedisClientType {
    if (!this.publisher) {
      throw new Error('Redis publisher not connected');
    }
    return this.publisher;
  }
}

export const redis = RedisConnection.getInstance();
