
import { v4 as uuidv4 } from 'uuid';
import { logger } from '../logging/logger';
import { RequestContext } from '../utils/requestContext';

export interface GameObjectMetadata {
  id: string;
  type: string;
  regionId?: string;
  createdAt: number;
  updatedAt: number;
  version: number;
}

export abstract class BaseGameObject {
  protected metadata: GameObjectMetadata;

  constructor(type: string, id?: string, regionId?: string) {
    const now = Date.now();
    this.metadata = {
      id: id || uuidv4(),
      type,
      regionId,
      createdAt: now,
      updatedAt: now,
      version: 1
    };
  }

  getId(): string {
    return this.metadata.id;
  }

  getType(): string {
    return this.metadata.type;
  }

  getRegionId(): string | undefined {
    return this.metadata.regionId;
  }

  getMetadata(): GameObjectMetadata {
    return { ...this.metadata };
  }

  protected updateMetadata(regionId?: string): void {
    this.metadata.updatedAt = Date.now();
    this.metadata.version++;
    if (regionId) {
      this.metadata.regionId = regionId;
    }
  }

  protected logWithContext(level: 'info' | 'warn' | 'error' | 'debug', message: string, data?: any, context?: RequestContext): void {
    const logData = {
      objectId: this.metadata.id,
      objectType: this.metadata.type,
      regionId: this.metadata.regionId,
      objectVersion: this.metadata.version,
      requestId: context?.requestId,
      userId: context?.userId,
      sessionId: context?.sessionId,
      timestamp: context?.timestamp,
      ...data
    };

    logger[level](message, logData);
  }

  // Abstract methods that must be implemented by subclasses
  abstract toJSON(): any;
  abstract validate(): boolean;
}

export class Player extends BaseGameObject {
  private username: string;
  private level: number;
  private position: { x: number; y: number; z: number };
  private stats: { health: number; mana: number; experience: number };

  constructor(username: string, level: number = 1, id?: string, regionId?: string) {
    super('Player', id, regionId);
    this.username = username;
    this.level = level;
    this.position = { x: 0, y: 0, z: 0 };
    this.stats = { health: 100, mana: 50, experience: 0 };
  }

  getUsername(): string {
    return this.username;
  }

  getLevel(): number {
    return this.level;
  }

  getPosition(): { x: number; y: number; z: number } {
    return { ...this.position };
  }

  getStats(): { health: number; mana: number; experience: number } {
    return { ...this.stats };
  }

  updatePosition(x: number, y: number, z: number, regionId?: string, context?: RequestContext): void {
    this.position = { x, y, z };
    this.updateMetadata(regionId);
    
    this.logWithContext('info', 'Player position updated', {
      oldPosition: this.position,
      newPosition: { x, y, z },
      newRegionId: regionId
    }, context);
  }

  levelUp(context?: RequestContext): void {
    this.level++;
    this.updateMetadata();
    
    this.logWithContext('info', 'Player leveled up', {
      newLevel: this.level,
      username: this.username
    }, context);
  }

  updateStats(health?: number, mana?: number, experience?: number, context?: RequestContext): void {
    if (health !== undefined) this.stats.health = health;
    if (mana !== undefined) this.stats.mana = mana;
    if (experience !== undefined) this.stats.experience = experience;
    
    this.updateMetadata();
    
    this.logWithContext('info', 'Player stats updated', {
      newStats: this.stats,
      username: this.username
    }, context);
  }

  toJSON(): any {
    return {
      ...this.metadata,
      username: this.username,
      level: this.level,
      position: this.position,
      stats: this.stats
    };
  }

  validate(): boolean {
    return this.username.length > 0 && this.level > 0;
  }
}

export class GameItem extends BaseGameObject {
  private name: string;
  private quantity: number;
  private properties: Record<string, any>;

  constructor(name: string, quantity: number = 1, properties: Record<string, any> = {}, id?: string, regionId?: string) {
    super('GameItem', id, regionId);
    this.name = name;
    this.quantity = quantity;
    this.properties = properties;
  }

  getName(): string {
    return this.name;
  }

  getQuantity(): number {
    return this.quantity;
  }

  getProperties(): Record<string, any> {
    return { ...this.properties };
  }

  updateQuantity(quantity: number, context?: RequestContext): void {
    const oldQuantity = this.quantity;
    this.quantity = quantity;
    this.updateMetadata();
    
    this.logWithContext('info', 'Item quantity updated', {
      itemName: this.name,
      oldQuantity,
      newQuantity: quantity
    }, context);
  }

  toJSON(): any {
    return {
      ...this.metadata,
      name: this.name,
      quantity: this.quantity,
      properties: this.properties
    };
  }

  validate(): boolean {
    return this.name.length > 0 && this.quantity >= 0;
  }
}
