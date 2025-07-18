
import { BaseEntity } from '../../core/base/BaseEntity';
import { logger, gameLogger } from '../../logging/logger';
import { Item } from '../inventory/Item';

export enum QuestType {
  MAIN_STORY = 'main_story',
  SIDE_QUEST = 'side_quest',
  DAILY = 'daily',
  WEEKLY = 'weekly',
  GUILD = 'guild',
  EVENT = 'event',
  REPEATABLE = 'repeatable'
}

export enum QuestStatus {
  AVAILABLE = 'available',
  ACTIVE = 'active',
  COMPLETED = 'completed',
  FAILED = 'failed',
  ABANDONED = 'abandoned'
}

export enum ObjectiveType {
  KILL = 'kill',
  COLLECT = 'collect',
  DELIVER = 'deliver',
  TALK_TO = 'talk_to',
  REACH_LOCATION = 'reach_location',
  USE_ITEM = 'use_item',
  CRAFT = 'craft',
  LEVEL_UP = 'level_up'
}

export interface IQuestObjective {
  id: string;
  type: ObjectiveType;
  description: string;
  target: string;
  requiredCount: number;
  currentCount: number;
  isCompleted: boolean;
  isOptional: boolean;
}

export interface IQuestReward {
  experience: number;
  currency: Map<string, number>;
  items: Array<{ itemId: string; quantity: number; }>;
  reputation: Map<string, number>;
  skillPoints: number;
  attributePoints: number;
}

export interface IQuestRequirements {
  minLevel: number;
  maxLevel?: number;
  requiredQuests: string[];
  forbiddenQuests: string[];
  requiredItems: Array<{ itemId: string; quantity: number; }>;
  requiredClass?: string[];
  requiredRace?: string[];
  requiredGuild?: string;
  requiredReputation?: Map<string, number>;
}

export class Quest extends BaseEntity {
  public questId: string;
  public title: string;
  public description: string;
  public type: QuestType;
  public objectives: IQuestObjective[];
  public rewards: IQuestReward;
  public requirements: IQuestRequirements;
  public isAutoAccept: boolean;
  public isAutoComplete: boolean;
  public isShared: boolean;
  public maxPartySize: number;
  public timeLimit?: number;
  public cooldownTime?: number;
  public maxCompletions?: number;
  public priority: number;
  public zone: string;
  public giver?: string;
  public turnInNpc?: string;

  constructor(
    questId: string,
    title: string,
    description: string,
    type: QuestType,
    objectives: IQuestObjective[],
    rewards: IQuestReward,
    regionId: string,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);

    this.questId = questId;
    this.title = title;
    this.description = description;
    this.type = type;
    this.objectives = objectives;
    this.rewards = rewards;
    this.requirements = {
      minLevel: 1,
      requiredQuests: [],
      forbiddenQuests: [],
      requiredItems: []
    };
    this.isAutoAccept = false;
    this.isAutoComplete = false;
    this.isShared = false;
    this.maxPartySize = 1;
    this.priority = 1;
    this.zone = 'unknown';

    logger.info('Quest created', {
      questId: this.questId,
      questEntityId: this.entityId,
      title: this.title,
      type: this.type,
      objectiveCount: this.objectives.length,
      regionId: this.regionId,
      traceId: this.metadata.traceId
    });
  }

  public updateObjective(objectiveId: string, progress: number, traceId?: string): boolean {
    const objective = this.objectives.find(obj => obj.id === objectiveId);
    if (!objective) {
      logger.warn('Objective not found', {
        questId: this.questId,
        objectiveId,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    const oldCount = objective.currentCount;
    objective.currentCount = Math.min(objective.requiredCount, objective.currentCount + progress);
    objective.isCompleted = objective.currentCount >= objective.requiredCount;

    this.updateEntity('quest_system', traceId);

    gameLogger.playerAction('quest_progress', '', {
      questId: this.questId,
      objectiveId,
      progress,
      oldCount,
      newCount: objective.currentCount,
      isCompleted: objective.isCompleted
    }, traceId);

    return objective.isCompleted;
  }

  public isQuestCompleted(): boolean {
    const requiredObjectives = this.objectives.filter(obj => !obj.isOptional);
    return requiredObjectives.every(obj => obj.isCompleted);
  }

  public canAccept(playerLevel: number, completedQuests: string[], inventory: any, traceId?: string): boolean {
    // Level requirements
    if (playerLevel < this.requirements.minLevel) {
      logger.debug('Quest level requirement not met', {
        questId: this.questId,
        playerLevel,
        requiredLevel: this.requirements.minLevel,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    if (this.requirements.maxLevel && playerLevel > this.requirements.maxLevel) {
      logger.debug('Player level too high for quest', {
        questId: this.questId,
        playerLevel,
        maxLevel: this.requirements.maxLevel,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    // Quest prerequisites
    for (const requiredQuest of this.requirements.requiredQuests) {
      if (!completedQuests.includes(requiredQuest)) {
        logger.debug('Required quest not completed', {
          questId: this.questId,
          requiredQuest,
          traceId: traceId || this.metadata.traceId
        });
        return false;
      }
    }

    // Forbidden quests
    for (const forbiddenQuest of this.requirements.forbiddenQuests) {
      if (completedQuests.includes(forbiddenQuest)) {
        logger.debug('Forbidden quest completed', {
          questId: this.questId,
          forbiddenQuest,
          traceId: traceId || this.metadata.traceId
        });
        return false;
      }
    }

    return true;
  }

  public getCompletionReward(): IQuestReward {
    return {
      experience: this.rewards.experience,
      currency: new Map(this.rewards.currency),
      items: [...this.rewards.items],
      reputation: new Map(this.rewards.reputation),
      skillPoints: this.rewards.skillPoints,
      attributePoints: this.rewards.attributePoints
    };
  }
}

export class PlayerQuest extends BaseEntity {
  public playerId: string;
  public questId: string;
  public quest: Quest;
  public status: QuestStatus;
  public acceptedAt: Date;
  public completedAt?: Date;
  public abandonedAt?: Date;
  public expiresAt?: Date;
  public completionCount: number;
  public lastCompletedAt?: Date;

  constructor(
    playerId: string,
    quest: Quest,
    regionId: string,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);

    this.playerId = playerId;
    this.questId = quest.questId;
    this.quest = quest;
    this.status = QuestStatus.ACTIVE;
    this.acceptedAt = new Date();
    this.completionCount = 0;

    if (quest.timeLimit) {
      this.expiresAt = new Date(Date.now() + quest.timeLimit * 1000);
    }

    gameLogger.playerAction('quest_accepted', playerId, {
      questId: this.questId,
      questTitle: quest.title,
      questType: quest.type
    }, traceId);

    logger.info('Player quest created', {
      playerQuestId: this.entityId,
      playerId: this.playerId,
      questId: this.questId,
      questTitle: quest.title,
      traceId: this.metadata.traceId
    });
  }

  public updateProgress(objectiveId: string, progress: number, traceId?: string): boolean {
    if (this.status !== QuestStatus.ACTIVE) {
      logger.warn('Cannot update progress on inactive quest', {
        playerQuestId: this.entityId,
        playerId: this.playerId,
        questId: this.questId,
        status: this.status,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    const objectiveCompleted = this.quest.updateObjective(objectiveId, progress, traceId);
    
    if (this.quest.isQuestCompleted()) {
      this.complete(traceId);
      return true;
    }

    this.updateEntity('quest_progress_system', traceId);
    return objectiveCompleted;
  }

  public complete(traceId?: string): void {
    this.status = QuestStatus.COMPLETED;
    this.completedAt = new Date();
    this.completionCount += 1;
    this.lastCompletedAt = new Date();

    this.updateEntity('quest_completion_system', traceId);

    gameLogger.playerAction('quest_completed', this.playerId, {
      questId: this.questId,
      questTitle: this.quest.title,
      questType: this.quest.type,
      completionTime: this.completedAt.getTime() - this.acceptedAt.getTime(),
      completionCount: this.completionCount
    }, traceId);

    logger.info('Player quest completed', {
      playerQuestId: this.entityId,
      playerId: this.playerId,
      questId: this.questId,
      questTitle: this.quest.title,
      completionCount: this.completionCount,
      traceId: traceId || this.metadata.traceId
    });
  }

  public abandon(traceId?: string): void {
    this.status = QuestStatus.ABANDONED;
    this.abandonedAt = new Date();

    this.updateEntity('quest_abandon_system', traceId);

    gameLogger.playerAction('quest_abandoned', this.playerId, {
      questId: this.questId,
      questTitle: this.quest.title,
      questType: this.quest.type,
      timeActive: this.abandonedAt.getTime() - this.acceptedAt.getTime()
    }, traceId);

    logger.info('Player quest abandoned', {
      playerQuestId: this.entityId,
      playerId: this.playerId,
      questId: this.questId,
      questTitle: this.quest.title,
      traceId: traceId || this.metadata.traceId
    });
  }

  public checkExpiration(traceId?: string): boolean {
    if (this.expiresAt && new Date() > this.expiresAt && this.status === QuestStatus.ACTIVE) {
      this.status = QuestStatus.FAILED;
      this.updateEntity('quest_expiration_system', traceId);

      gameLogger.playerAction('quest_expired', this.playerId, {
        questId: this.questId,
        questTitle: this.quest.title,
        questType: this.quest.type,
        expirationTime: this.expiresAt
      }, traceId);

      logger.info('Player quest expired', {
        playerQuestId: this.entityId,
        playerId: this.playerId,
        questId: this.questId,
        questTitle: this.quest.title,
        expiresAt: this.expiresAt,
        traceId: traceId || this.metadata.traceId
      });

      return true;
    }

    return false;
  }
}
