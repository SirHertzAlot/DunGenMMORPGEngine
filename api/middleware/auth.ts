
import { Request, Response, NextFunction } from 'express';
import jwt from 'jsonwebtoken';
import { playerRepository } from '../../persistence/repos/playerRepository';
import { logger } from '../../logging/logger';

export interface AuthenticatedRequest extends Request {
  user?: {
    id: string;
    username: string;
    email: string;
  };
}

export const authenticateToken = async (req: AuthenticatedRequest, res: Response, next: NextFunction) => {
  try {
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.split(' ')[1]; // Bearer TOKEN

    if (!token) {
      return res.status(401).json({
        error: 'Unauthorized',
        message: 'Access token required'
      });
    }

    const jwtSecret = process.env.JWT_SECRET;
    if (!jwtSecret) {
      logger.error('JWT_SECRET not configured');
      return res.status(500).json({
        error: 'Internal Server Error',
        message: 'Authentication service misconfigured'
      });
    }

    const decoded = jwt.verify(token, jwtSecret) as { userId: string; username: string; email: string };
    
    // Verify user still exists and is valid
    const player = await playerRepository.findPlayerById(decoded.userId);
    if (!player) {
      return res.status(401).json({
        error: 'Unauthorized',
        message: 'Invalid or expired token'
      });
    }

    req.user = {
      id: decoded.userId,
      username: decoded.username,
      email: decoded.email
    };

    next();
  } catch (error) {
    if (error instanceof jwt.JsonWebTokenError) {
      return res.status(401).json({
        error: 'Unauthorized',
        message: 'Invalid token'
      });
    }

    logger.error('Authentication middleware error', { error: (error as Error).message });
    return res.status(500).json({
      error: 'Internal Server Error',
      message: 'Authentication failed'
    });
  }
};

export const optionalAuth = async (req: AuthenticatedRequest, res: Response, next: NextFunction) => {
  try {
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.split(' ')[1];

    if (!token) {
      return next(); // No token provided, continue without authentication
    }

    const jwtSecret = process.env.JWT_SECRET;
    if (!jwtSecret) {
      return next(); // No JWT secret configured, continue without authentication
    }

    const decoded = jwt.verify(token, jwtSecret) as { userId: string; username: string; email: string };
    const player = await playerRepository.findPlayerById(decoded.userId);
    
    if (player) {
      req.user = {
        id: decoded.userId,
        username: decoded.username,
        email: decoded.email
      };
    }

    next();
  } catch (error) {
    // On error with optional auth, just continue without user
    next();
  }
};
