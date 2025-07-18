
import { BaseEntity } from '../../core/base/BaseEntity';
import { logger } from '../../logging/logger';

export interface ICharacterStats {
  health: number;
  maxHealth: number;
  mana: number;
  maxMana: number;
  stamina: number;
  maxStamina: number;
  strength: number;
  agility: number;
  intelligence: number;
  constitution: number;
  wisdom: number;
  charisma: number;
}

export interface ICharacterProgression {
  level: number;
  experience: number;
  experienceToNext: number;
  skillPoints: number;
  attributePoints: number;
}

export interface ICharacterPosition {
  x: number;
  y: number;
  z: number;
  rotation: number;
  mapId: string;
  zoneId: string;
}

export interface ICharacterCombat {
  isInCombat: boolean;
  combatStartTime?: Date;
  lastAttackTime?: Date;
  attackSpeed: number;
  criticalChance: number;
  dodgeChance: number;
  blockChance: number;
}

export class Character extends BaseEntity {
  public playerId: string;
  public name: string;
  public classType: string;
  public race: string;
  public stats: ICharacterStats;
  public progression: ICharacterProgression;
  public position: ICharacterPosition;
  public combat: ICharacterCombat;
  public isOnline: boolean;
  public lastLoginTime?: Date;
  public totalPlayTime: number;
  public reputation: Map<string, number>;
  public titles: string[];
  public activeTitle?: string;

  constructor(
    playerId: string,
    name: string,
    classType: string,
    race: string,
    regionId: string,
    startPosition: ICharacterPosition,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);
    
    this.playerId = playerId;
    this.name = name;
    this.classType = classType;
    this.race = race;
    this.isOnline = false;
    this.totalPlayTime = 0;
    this.reputation = new Map();
    this.titles = [];

    // Initialize stats based on class and race
    this.stats = this.initializeStats(classType, race);
    this.progression = {
      level: 1,
      experience: 0,
      experienceToNext: 1000,
      skillPoints: 0,
      attributePoints: 5
    };
    this.position = startPosition;
    this.combat = {
      isInCombat: false,
      attackSpeed: 1.0,
      criticalChance: 0.05,
      dodgeChance: 0.1,
      blockChance: 0.05
    };

    logger.info('Character created', {
      characterId: this.entityId,
      playerId: this.playerId,
      name: this.name,
      classType: this.classType,
      race: this.race,
      regionId: this.regionId,
      traceId: this.metadata.traceId
    });
  }

  private initializeStats(classType: string, race: string): ICharacterStats {
    const baseStats = {
      health: 100,
      maxHealth: 100,
      mana: 50,
      maxMana: 50,
      stamina: 100,
      maxStamina: 100,
      strength: 10,
      agility: 10,
      intelligence: 10,
      constitution: 10,
      wisdom: 10,
      charisma: 10
    };

    // Class bonuses
    switch (classType.toLowerCase()) {
      case 'warrior':
        baseStats.strength += 5;
        baseStats.constitution += 3;
        baseStats.maxHealth += 50;
        break;
      case 'mage':
        baseStats.intelligence += 5;
        baseStats.wisdom += 3;
        baseStats.maxMana += 100;
        break;
      case 'rogue':
        baseStats.agility += 5;
        baseStats.charisma += 3;
        break;
      case 'cleric':
        baseStats.wisdom += 4;
        baseStats.constitution += 3;
        baseStats.maxMana += 50;
        break;
    }

    // Race bonuses
    switch (race.toLowerCase()) {
      case 'human':
        baseStats.charisma += 2;
        break;
      case 'elf':
        baseStats.intelligence += 2;
        baseStats.agility += 1;
        break;
      case 'dwarf':
        baseStats.constitution += 2;
        baseStats.strength += 1;
        break;
      case 'orc':
        baseStats.strength += 2;
        baseStats.constitution += 1;
        break;
    }

    baseStats.health = baseStats.maxHealth;
    baseStats.mana = baseStats.maxMana;
    baseStats.stamina = baseStats.maxStamina;

    return baseStats;
  }

  public gainExperience(amount: number, source: string, traceId?: string): boolean {
    const oldLevel = this.progression.level;
    this.progression.experience += amount;

    while (this.progression.experience >= this.progression.experienceToNext) {
      this.levelUp(traceId);
    }

    this.updateEntity('experience_system', traceId);

    logger.info('Experience gained', {
      characterId: this.entityId,
      amount,
      source,
      newExperience: this.progression.experience,
      levelUp: this.progression.level > oldLevel,
      traceId: traceId || this.metadata.traceId
    });

    return this.progression.level > oldLevel;
  }

  private levelUp(traceId?: string): void {
    this.progression.level += 1;
    this.progression.experience -= this.progression.experienceToNext;
    this.progression.experienceToNext = Math.floor(this.progression.experienceToNext * 1.15);
    this.progression.attributePoints += 5;
    this.progression.skillPoints += 3;

    // Level up stat increases
    this.stats.maxHealth += 10 + (this.stats.constitution * 2);
    this.stats.maxMana += 5 + this.stats.wisdom;
    this.stats.maxStamina += 5 + this.stats.constitution;

    // Restore health/mana on level up
    this.stats.health = this.stats.maxHealth;
    this.stats.mana = this.stats.maxMana;
    this.stats.stamina = this.stats.maxStamina;

    logger.info('Character leveled up', {
      characterId: this.entityId,
      newLevel: this.progression.level,
      attributePoints: this.progression.attributePoints,
      skillPoints: this.progression.skillPoints,
      traceId: traceId || this.metadata.traceId
    });
  }

  public takeDamage(amount: number, source: string, traceId?: string): boolean {
    const actualDamage = Math.max(0, amount);
    this.stats.health = Math.max(0, this.stats.health - actualDamage);
    
    const isDead = this.stats.health === 0;
    
    this.updateEntity('combat_system', traceId);

    logger.info('Character took damage', {
      characterId: this.entityId,
      damage: actualDamage,
      source,
      newHealth: this.stats.health,
      isDead,
      traceId: traceId || this.metadata.traceId
    });

    return isDead;
  }

  public heal(amount: number, source: string, traceId?: string): number {
    const oldHealth = this.stats.health;
    this.stats.health = Math.min(this.stats.maxHealth, this.stats.health + amount);
    const actualHealing = this.stats.health - oldHealth;
    
    this.updateEntity('healing_system', traceId);

    logger.info('Character healed', {
      characterId: this.entityId,
      healing: actualHealing,
      source,
      newHealth: this.stats.health,
      traceId: traceId || this.metadata.traceId
    });

    return actualHealing;
  }

  public moveToPosition(newPosition: ICharacterPosition, traceId?: string): void {
    const oldPosition = { ...this.position };
    this.position = newPosition;
    this.updateEntity('movement_system', traceId);

    logger.debug('Character moved', {
      characterId: this.entityId,
      oldPosition,
      newPosition,
      traceId: traceId || this.metadata.traceId
    });
  }
}
