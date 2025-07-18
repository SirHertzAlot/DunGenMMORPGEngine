
import { logger } from '../logging/logger';
import { redisConnection } from '../config/redis';

export class UUIDTracker {
  private static instance: UUIDTracker;
  private readonly METRICS_KEY = 'uuid_metrics';
  private readonly TRACKING_KEY = 'uuid_tracking';

  private constructor() {}

  static getInstance(): UUIDTracker {
    if (!UUIDTracker.instance) {
      UUIDTracker.instance = new UUIDTracker();
    }
    return UUIDTracker.instance;
  }

  async trackUUID(uuid: string, type: string, source: string, metadata?: Record<string, any>): Promise<void> {
    try {
      const redis = redisConnection.getClient();
      const timestamp = Date.now();
      
      const trackingData = {
        uuid,
        type,
        source,
        timestamp,
        metadata: metadata || {}
      };

      // Store tracking data with TTL (30 days)
      await redis.setEx(`${this.TRACKING_KEY}:${uuid}`, 30 * 24 * 60 * 60, JSON.stringify(trackingData));
      
      // Update metrics
      await Promise.all([
        redis.hIncrBy(`${this.METRICS_KEY}:${type}`, 'count', 1),
        redis.hIncrBy(`${this.METRICS_KEY}:${source}`, 'count', 1),
        redis.hSet(`${this.METRICS_KEY}:${type}`, 'last_seen', timestamp.toString()),
        redis.hSet(`${this.METRICS_KEY}:${source}`, 'last_seen', timestamp.toString())
      ]);

      logger.debug('UUID tracked', {
        uuid,
        type,
        source,
        timestamp,
        metadata
      });
    } catch (error) {
      logger.error('Failed to track UUID', {
        error: (error as Error).message,
        uuid,
        type,
        source
      });
    }
  }

  async getUUIDInfo(uuid: string): Promise<any | null> {
    try {
      const redis = redisConnection.getClient();
      const data = await redis.get(`${this.TRACKING_KEY}:${uuid}`);
      
      if (!data) {
        return null;
      }

      return JSON.parse(data);
    } catch (error) {
      logger.error('Failed to get UUID info', {
        error: (error as Error).message,
        uuid
      });
      return null;
    }
  }

  async getMetrics(): Promise<Record<string, any>> {
    try {
      const redis = redisConnection.getClient();
      const keys = await redis.keys(`${this.METRICS_KEY}:*`);
      
      const metrics: Record<string, any> = {};
      
      for (const key of keys) {
        const data = await redis.hGetAll(key);
        const metricName = key.replace(`${this.METRICS_KEY}:`, '');
        metrics[metricName] = data;
      }

      return metrics;
    } catch (error) {
      logger.error('Failed to get UUID metrics', {
        error: (error as Error).message
      });
      return {};
    }
  }

  async cleanupExpiredUUIDs(): Promise<void> {
    try {
      const redis = redisConnection.getClient();
      const keys = await redis.keys(`${this.TRACKING_KEY}:*`);
      
      let cleanedCount = 0;
      const cutoffTime = Date.now() - (30 * 24 * 60 * 60 * 1000); // 30 days ago

      for (const key of keys) {
        const data = await redis.get(key);
        if (data) {
          const trackingData = JSON.parse(data);
          if (trackingData.timestamp < cutoffTime) {
            await redis.del(key);
            cleanedCount++;
          }
        }
      }

      logger.info('UUID cleanup completed', {
        cleanedCount,
        totalKeys: keys.length
      });
    } catch (error) {
      logger.error('Failed to cleanup expired UUIDs', {
        error: (error as Error).message
      });
    }
  }
}

export const uuidTracker = UUIDTracker.getInstance();
