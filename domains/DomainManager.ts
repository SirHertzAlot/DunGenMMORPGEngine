
import { Character } from './character/Character';
import { Inventory } from './inventory/Inventory';
import { Item } from './inventory/Item';
import { Guild } from './guild/Guild';
import { Quest, PlayerQuest } from './quest/Quest';
import { logger, gameLogger, createRequestContext } from '../logging/logger';
import { v4 as uuidv4 } from 'uuid';

export interface IGameContext {
  traceId: string;
  sessionId: string;
  requestId: string;
  playerId?: string;
  characterId?: string;
  regionId: string;
  timestamp: Date;
}

export class DomainManager {
  private static instance: DomainManager;
  private characters: Map<string, Character> = new Map();
  private inventories: Map<string, Inventory> = new Map();
  private guilds: Map<string, Guild> = new Map();
  private quests: Map<string, Quest> = new Map();
  private playerQuests: Map<string, PlayerQuest[]> = new Map();

  private constructor() {
    logger.info('Domain Manager initialized', {
      instanceId: uuidv4(),
      timestamp: new Date()
    });
  }

  static getInstance(): DomainManager {
    if (!DomainManager.instance) {
      DomainManager.instance = new DomainManager();
    }
    return DomainManager.instance;
  }

  public createGameContext(req?: any, regionId: string = 'default'): IGameContext {
    const requestContext = createRequestContext(req);
    return {
      ...requestContext,
      regionId,
      timestamp: new Date()
    };
  }

  // Character Management
  public async createCharacter(
    playerId: string,
    name: string,
    classType: string,
    race: string,
    context: IGameContext
  ): Promise<{ character: Character; inventory: Inventory }> {
    try {
      const startPosition = {
        x: 0,
        y: 0,
        z: 0,
        rotation: 0,
        mapId: 'starter_map',
        zoneId: 'starter_zone'
      };

      const character = new Character(
        playerId,
        name,
        classType,
        race,
        context.regionId,
        startPosition,
        'character_creation_system',
        context.traceId
      );

      const inventory = new Inventory(
        character.entityId,
        50, // max slots
        1000, // max weight
        context.regionId,
        'inventory_system',
        context.traceId
      );

      // Add starting currency
      inventory.addCurrency('gold', 100, context.traceId);
      inventory.addCurrency('silver', 0, context.traceId);

      this.characters.set(character.entityId, character);
      this.inventories.set(character.entityId, inventory);

      gameLogger.playerAction('character_created', playerId, {
        characterId: character.entityId,
        characterName: name,
        classType,
        race,
        startingStats: character.stats
      }, context.traceId);

      logger.info('Character and inventory created successfully', {
        characterId: character.entityId,
        inventoryId: inventory.entityId,
        playerId,
        traceId: context.traceId
      });

      return { character, inventory };
    } catch (error) {
      logger.error('Failed to create character', {
        playerId,
        name,
        classType,
        race,
        error: (error as Error).message,
        traceId: context.traceId
      });
      throw error;
    }
  }

  public getCharacter(characterId: string): Character | undefined {
    return this.characters.get(characterId);
  }

  public getInventory(characterId: string): Inventory | undefined {
    return this.inventories.get(characterId);
  }

  // Combat System
  public async executeCombat(
    attackerId: string,
    defenderId: string,
    skillId: string,
    context: IGameContext
  ): Promise<any> {
    try {
      const attacker = this.characters.get(attackerId);
      const defender = this.characters.get(defenderId);

      if (!attacker || !defender) {
        throw new Error('Invalid combat participants');
      }

      // Basic damage calculation
      const baseDamage = attacker.stats.strength * 2;
      const criticalRoll = Math.random();
      const isCritical = criticalRoll < attacker.combat.criticalChance;
      const finalDamage = isCritical ? baseDamage * 2 : baseDamage;

      // Apply damage
      const isDead = defender.takeDamage(finalDamage, `combat_${skillId}`, context.traceId);

      // Set combat flags
      attacker.combat.isInCombat = true;
      attacker.combat.lastAttackTime = new Date();
      defender.combat.isInCombat = true;

      const combatResult = {
        attackerId,
        defenderId,
        skillId,
        damage: finalDamage,
        isCritical,
        isDead,
        attackerHealth: attacker.stats.health,
        defenderHealth: defender.stats.health
      };

      gameLogger.combatEvent('attack', [attackerId, defenderId], combatResult, context.traceId);

      return combatResult;
    } catch (error) {
      logger.error('Combat execution failed', {
        attackerId,
        defenderId,
        skillId,
        error: (error as Error).message,
        traceId: context.traceId
      });
      throw error;
    }
  }

  // Guild Management
  public async createGuild(
    name: string,
    tag: string,
    description: string,
    leaderId: string,
    leaderName: string,
    context: IGameContext
  ): Promise<Guild> {
    try {
      const guild = new Guild(
        name,
        tag,
        description,
        leaderId,
        leaderName,
        context.regionId,
        'guild_system',
        context.traceId
      );

      this.guilds.set(guild.entityId, guild);

      gameLogger.guildEvent('created', guild.entityId, {
        name,
        tag,
        leaderId,
        leaderName
      }, context.traceId);

      return guild;
    } catch (error) {
      logger.error('Failed to create guild', {
        name,
        tag,
        leaderId,
        error: (error as Error).message,
        traceId: context.traceId
      });
      throw error;
    }
  }

  public getGuild(guildId: string): Guild | undefined {
    return this.guilds.get(guildId);
  }

  // Quest Management
  public async acceptQuest(
    playerId: string,
    questId: string,
    context: IGameContext
  ): Promise<PlayerQuest> {
    try {
      const quest = this.quests.get(questId);
      if (!quest) {
        throw new Error('Quest not found');
      }

      const character = this.characters.get(context.characterId!);
      if (!character) {
        throw new Error('Character not found');
      }

      // Check if quest can be accepted
      const playerQuests = this.playerQuests.get(playerId) || [];
      const completedQuests = playerQuests
        .filter(pq => pq.status === 'completed')
        .map(pq => pq.questId);

      if (!quest.canAccept(character.progression.level, completedQuests, null, context.traceId)) {
        throw new Error('Quest requirements not met');
      }

      const playerQuest = new PlayerQuest(
        playerId,
        quest,
        context.regionId,
        'quest_system',
        context.traceId
      );

      if (!this.playerQuests.has(playerId)) {
        this.playerQuests.set(playerId, []);
      }
      this.playerQuests.get(playerId)!.push(playerQuest);

      return playerQuest;
    } catch (error) {
      logger.error('Failed to accept quest', {
        playerId,
        questId,
        error: (error as Error).message,
        traceId: context.traceId
      });
      throw error;
    }
  }

  // Experience and Progression
  public async awardExperience(
    characterId: string,
    amount: number,
    source: string,
    context: IGameContext
  ): Promise<boolean> {
    try {
      const character = this.characters.get(characterId);
      if (!character) {
        throw new Error('Character not found');
      }

      const leveledUp = character.gainExperience(amount, source, context.traceId);

      gameLogger.playerAction('experience_gained', character.playerId, {
        characterId,
        amount,
        source,
        newLevel: character.progression.level,
        newExperience: character.progression.experience,
        leveledUp
      }, context.traceId);

      return leveledUp;
    } catch (error) {
      logger.error('Failed to award experience', {
        characterId,
        amount,
        source,
        error: (error as Error).message,
        traceId: context.traceId
      });
      throw error;
    }
  }

  // Utility Methods
  public getGameStats(): any {
    return {
      totalCharacters: this.characters.size,
      totalGuilds: this.guilds.size,
      totalQuests: this.quests.size,
      activePlayerQuests: Array.from(this.playerQuests.values()).reduce((sum, quests) => sum + quests.length, 0),
      timestamp: new Date()
    };
  }

  public async performHealthCheck(context: IGameContext): Promise<any> {
    const stats = this.getGameStats();
    
    gameLogger.systemEvent('health_check', 'domain_manager', stats, context.traceId);
    
    return {
      status: 'healthy',
      ...stats,
      regionId: context.regionId,
      traceId: context.traceId
    };
  }
}

export default DomainManager.getInstance();
