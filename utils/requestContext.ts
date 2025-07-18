
import { Request, Response, NextFunction } from 'express';
import { v4 as uuidv4 } from 'uuid';

export interface RequestContext {
  requestId: string;
  userId?: string;
  sessionId?: string;
  regionId?: string;
  timestamp: number;
}

export interface ContextualRequest extends Request {
  context: RequestContext;
}

export const requestContextMiddleware = (req: ContextualRequest, res: Response, next: NextFunction) => {
  const requestId = req.headers['x-request-id'] as string || uuidv4();
  
  req.context = {
    requestId,
    userId: req.headers['x-user-id'] as string,
    sessionId: req.headers['x-session-id'] as string,
    regionId: req.headers['x-region-id'] as string,
    timestamp: Date.now()
  };

  // Set response headers for tracking
  res.setHeader('x-request-id', requestId);
  res.setHeader('x-timestamp', req.context.timestamp);

  next();
};

export const createContext = (overrides?: Partial<RequestContext>): RequestContext => {
  return {
    requestId: uuidv4(),
    timestamp: Date.now(),
    ...overrides
  };
};
