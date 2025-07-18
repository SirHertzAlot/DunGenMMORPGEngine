
import { BaseEntity } from '../../core/base/BaseEntity';
import { logger } from '../../logging/logger';

export enum ItemType {
  WEAPON = 'weapon',
  ARMOR = 'armor',
  CONSUMABLE = 'consumable',
  QUEST = 'quest',
  MATERIAL = 'material',
  TOOL = 'tool',
  CURRENCY = 'currency'
}

export enum ItemRarity {
  COMMON = 'common',
  UNCOMMON = 'uncommon',
  RARE = 'rare',
  EPIC = 'epic',
  LEGENDARY = 'legendary',
  MYTHIC = 'mythic'
}

export enum ItemSlot {
  MAIN_HAND = 'main_hand',
  OFF_HAND = 'off_hand',
  TWO_HAND = 'two_hand',
  HEAD = 'head',
  CHEST = 'chest',
  LEGS = 'legs',
  FEET = 'feet',
  HANDS = 'hands',
  RING = 'ring',
  NECK = 'neck',
  TRINKET = 'trinket'
}

export interface IItemStats {
  damage?: number;
  armor?: number;
  strength?: number;
  agility?: number;
  intelligence?: number;
  constitution?: number;
  wisdom?: number;
  charisma?: number;
  criticalChance?: number;
  criticalDamage?: number;
  attackSpeed?: number;
  magicResistance?: number;
  healthRegeneration?: number;
  manaRegeneration?: number;
}

export interface IItemRequirements {
  level: number;
  strength?: number;
  agility?: number;
  intelligence?: number;
  constitution?: number;
  wisdom?: number;
  charisma?: number;
  classRestrictions?: string[];
  raceRestrictions?: string[];
}

export class Item extends BaseEntity {
  public itemId: string;
  public name: string;
  public description: string;
  public type: ItemType;
  public rarity: ItemRarity;
  public slot?: ItemSlot;
  public stats: IItemStats;
  public requirements: IItemRequirements;
  public value: number;
  public weight: number;
  public stackSize: number;
  public isBindOnPickup: boolean;
  public isBindOnEquip: boolean;
  public durability: number;
  public maxDurability: number;
  public enchantments: Map<string, number>;
  public sockets: number;
  public socketedGems: string[];

  constructor(
    itemId: string,
    name: string,
    description: string,
    type: ItemType,
    rarity: ItemRarity,
    regionId: string,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);

    this.itemId = itemId;
    this.name = name;
    this.description = description;
    this.type = type;
    this.rarity = rarity;
    this.stats = {};
    this.requirements = { level: 1 };
    this.value = 1;
    this.weight = 1;
    this.stackSize = 1;
    this.isBindOnPickup = false;
    this.isBindOnEquip = false;
    this.durability = 100;
    this.maxDurability = 100;
    this.enchantments = new Map();
    this.sockets = 0;
    this.socketedGems = [];

    logger.info('Item created', {
      itemEntityId: this.entityId,
      itemId: this.itemId,
      name: this.name,
      type: this.type,
      rarity: this.rarity,
      regionId: this.regionId,
      traceId: this.metadata.traceId
    });
  }

  public addEnchantment(enchantmentId: string, power: number, traceId?: string): void {
    this.enchantments.set(enchantmentId, power);
    this.updateEntity('enchantment_system', traceId);

    logger.info('Item enchanted', {
      itemEntityId: this.entityId,
      enchantmentId,
      power,
      traceId: traceId || this.metadata.traceId
    });
  }

  public repair(amount: number, traceId?: string): number {
    const oldDurability = this.durability;
    this.durability = Math.min(this.maxDurability, this.durability + amount);
    const actualRepair = this.durability - oldDurability;
    
    this.updateEntity('repair_system', traceId);

    logger.info('Item repaired', {
      itemEntityId: this.entityId,
      repairAmount: actualRepair,
      newDurability: this.durability,
      traceId: traceId || this.metadata.traceId
    });

    return actualRepair;
  }

  public takeDurabilityDamage(amount: number, traceId?: string): boolean {
    this.durability = Math.max(0, this.durability - amount);
    const isBroken = this.durability === 0;
    
    this.updateEntity('durability_system', traceId);

    logger.debug('Item durability damaged', {
      itemEntityId: this.entityId,
      damage: amount,
      newDurability: this.durability,
      isBroken,
      traceId: traceId || this.metadata.traceId
    });

    return isBroken;
  }
}
