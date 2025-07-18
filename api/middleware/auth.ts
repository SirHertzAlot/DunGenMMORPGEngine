
import { Request, Response, NextFunction } from 'express';
import jwt from 'jsonwebtoken';
import { logger } from '../../logging/logger';

interface AuthenticatedRequest extends Request {
  user?: {
    id: string;
    username: string;
    role: string;
  };
}

export const authMiddleware = (req: AuthenticatedRequest, res: Response, next: NextFunction) => {
  try {
    const token = req.header('Authorization')?.replace('Bearer ', '');
    
    if (!token) {
      return res.status(401).json({
        error: 'Access denied',
        message: 'No token provided'
      });
    }
    
    const decoded = jwt.verify(token, process.env.JWT_SECRET || 'fallback-secret') as any;
    req.user = decoded;
    
    logger.info('User authenticated', {
      userId: decoded.id,
      username: decoded.username,
      role: decoded.role,
      path: req.path
    });
    
    next();
  } catch (error) {
    logger.warn('Authentication failed', {
      error: (error as Error).message,
      path: req.path,
      ip: req.ip
    });
    
    res.status(401).json({
      error: 'Invalid token',
      message: 'Authentication failed'
    });
  }
};
