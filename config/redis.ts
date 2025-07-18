
import { createClient } from 'redis';
import { logger } from '../logging/logger';

export class RedisConnection {
  private static instance: RedisConnection;
  private client: ReturnType<typeof createClient> | null = null;
  private pubClient: ReturnType<typeof createClient> | null = null;
  private subClient: ReturnType<typeof createClient> | null = null;

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
      await this.client.connect();
      
      // Pub/Sub clients
      this.pubClient = createClient({ url: redisUrl });
      this.subClient = createClient({ url: redisUrl });
      
      await this.pubClient.connect();
      await this.subClient.connect();

      logger.info('Redis connected successfully', { url: redisUrl });

      // Handle connection events
      this.client.on('error', (error) => {
        logger.error('Redis client error', { error: error.message });
      });

      this.pubClient.on('error', (error) => {
        logger.error('Redis pub client error', { error: error.message });
      });

      this.subClient.on('error', (error) => {
        logger.error('Redis sub client error', { error: error.message });
      });

    } catch (error) {
      logger.error('Failed to connect to Redis', { error: (error as Error).message });
      throw error;
    }
  }

  getClient() {
    if (!this.client) {
      throw new Error('Redis client not initialized. Call connect() first.');
    }
    return this.client;
  }

  getPubClient() {
    if (!this.pubClient) {
      throw new Error('Redis pub client not initialized. Call connect() first.');
    }
    return this.pubClient;
  }

  getSubClient() {
    if (!this.subClient) {
      throw new Error('Redis sub client not initialized. Call connect() first.');
    }
    return this.subClient;
  }

  async disconnect(): Promise<void> {
    if (this.client) await this.client.quit();
    if (this.pubClient) await this.pubClient.quit();
    if (this.subClient) await this.subClient.quit();
    logger.info('Redis disconnected');
  }
}

export const redisConnection = RedisConnection.getInstance();
