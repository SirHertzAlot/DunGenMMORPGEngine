
import { v4 as uuidv4 } from 'uuid';
import { logger } from '../../logging/logger';

export interface IBaseEntity {
  id: string;
  entityId: string;
  createdAt: Date;
  updatedAt: Date;
  regionId: string;
  metadata: {
    version: number;
    lastModifiedBy: string;
    traceId?: string;
  };
}

export abstract class BaseEntity implements IBaseEntity {
  public readonly id: string;
  public readonly entityId: string;
  public createdAt: Date;
  public updatedAt: Date;
  public regionId: string;
  public metadata: {
    version: number;
    lastModifiedBy: string;
    traceId?: string;
  };

  constructor(regionId: string, createdBy: string = 'system', traceId?: string) {
    this.id = uuidv4();
    this.entityId = uuidv4();
    this.createdAt = new Date();
    this.updatedAt = new Date();
    this.regionId = regionId;
    this.metadata = {
      version: 1,
      lastModifiedBy: createdBy,
      traceId: traceId || uuidv4()
    };

    logger.debug('Base entity created', {
      entityId: this.entityId,
      id: this.id,
      regionId: this.regionId,
      traceId: this.metadata.traceId,
      createdBy
    });
  }

  protected updateEntity(modifiedBy: string, traceId?: string): void {
    this.updatedAt = new Date();
    this.metadata.version += 1;
    this.metadata.lastModifiedBy = modifiedBy;
    this.metadata.traceId = traceId || uuidv4();

    logger.debug('Entity updated', {
      entityId: this.entityId,
      version: this.metadata.version,
      traceId: this.metadata.traceId,
      modifiedBy
    });
  }
}
