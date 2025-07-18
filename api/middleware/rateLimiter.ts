
import { Request, Response, NextFunction } from 'express';
import { createClient } from 'redis';
import { logger } from '../../logging/logger';

const redis = createClient({
  url: process.env.REDIS_URL || 'redis://localhost:6379'
});

redis.connect().catch(err => {
  logger.error('Redis connection error', { error: err.message });
});

interface RateLimitConfig {
  windowMs: number;
  maxRequests: number;
  keyGenerator: (req: Request) => string;
}

const defaultConfig: RateLimitConfig = {
  windowMs: 60 * 1000, // 1 minute
  maxRequests: 100,
  keyGenerator: (req) => req.ip
};

export const rateLimiter = async (req: Request, res: Response, next: NextFunction) => {
  try {
    const key = `rate_limit:${defaultConfig.keyGenerator(req)}`;
    const current = await redis.incr(key);
    
    if (current === 1) {
      await redis.expire(key, Math.ceil(defaultConfig.windowMs / 1000));
    }
    
    if (current > defaultConfig.maxRequests) {
      logger.warn('Rate limit exceeded', {
        ip: req.ip,
        path: req.path,
        current,
        limit: defaultConfig.maxRequests
      });
      
      return res.status(429).json({
        error: 'Too Many Requests',
        message: 'Rate limit exceeded. Please try again later.',
        retryAfter: Math.ceil(defaultConfig.windowMs / 1000)
      });
    }
    
    res.setHeader('X-RateLimit-Limit', defaultConfig.maxRequests);
    res.setHeader('X-RateLimit-Remaining', Math.max(0, defaultConfig.maxRequests - current));
    
    next();
  } catch (error) {
    logger.error('Rate limiter error', { error: (error as Error).message });
    next(); // Continue on error to not block requests
  }
};
