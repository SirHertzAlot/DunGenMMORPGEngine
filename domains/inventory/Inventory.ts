
import { BaseEntity } from '../../core/base/BaseEntity';
import { Item } from './Item';
import { logger } from '../../logging/logger';

export interface IInventorySlot {
  slotId: string;
  item?: Item;
  quantity: number;
  isLocked: boolean;
}

export interface IEquipmentSlot {
  slotType: string;
  item?: Item;
  isLocked: boolean;
}

export class Inventory extends BaseEntity {
  public characterId: string;
  public slots: Map<string, IInventorySlot>;
  public equipment: Map<string, IEquipmentSlot>;
  public maxSlots: number;
  public maxWeight: number;
  public currentWeight: number;
  public currency: Map<string, number>;

  constructor(
    characterId: string,
    maxSlots: number,
    maxWeight: number,
    regionId: string,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);

    this.characterId = characterId;
    this.maxSlots = maxSlots;
    this.maxWeight = maxWeight;
    this.currentWeight = 0;
    this.slots = new Map();
    this.equipment = new Map();
    this.currency = new Map();

    // Initialize inventory slots
    for (let i = 0; i < maxSlots; i++) {
      this.slots.set(`slot_${i}`, {
        slotId: `slot_${i}`,
        quantity: 0,
        isLocked: false
      });
    }

    // Initialize equipment slots
    const equipmentSlots = [
      'main_hand', 'off_hand', 'head', 'chest', 'legs', 'feet',
      'hands', 'ring1', 'ring2', 'neck', 'trinket1', 'trinket2'
    ];

    equipmentSlots.forEach(slot => {
      this.equipment.set(slot, {
        slotType: slot,
        isLocked: false
      });
    });

    logger.info('Inventory created', {
      inventoryId: this.entityId,
      characterId: this.characterId,
      maxSlots: this.maxSlots,
      maxWeight: this.maxWeight,
      traceId: this.metadata.traceId
    });
  }

  public addItem(item: Item, quantity: number = 1, traceId?: string): boolean {
    // Check weight capacity
    const totalWeight = item.weight * quantity;
    if (this.currentWeight + totalWeight > this.maxWeight) {
      logger.warn('Inventory weight capacity exceeded', {
        inventoryId: this.entityId,
        currentWeight: this.currentWeight,
        maxWeight: this.maxWeight,
        attemptedWeight: totalWeight,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    // Try to stack with existing items
    if (item.stackSize > 1) {
      for (const [slotId, slot] of this.slots.entries()) {
        if (slot.item && slot.item.itemId === item.itemId) {
          const canStack = Math.min(quantity, item.stackSize - slot.quantity);
          if (canStack > 0) {
            slot.quantity += canStack;
            this.currentWeight += item.weight * canStack;
            quantity -= canStack;
            
            if (quantity === 0) {
              this.updateEntity('inventory_system', traceId);
              
              logger.info('Item stacked in inventory', {
                inventoryId: this.entityId,
                itemId: item.itemId,
                slotId,
                quantity: canStack,
                traceId: traceId || this.metadata.traceId
              });
              return true;
            }
          }
        }
      }
    }

    // Find empty slots for remaining quantity
    while (quantity > 0) {
      const emptySlot = Array.from(this.slots.values()).find(slot => !slot.item);
      if (!emptySlot) {
        logger.warn('No empty slots available', {
          inventoryId: this.entityId,
          remainingQuantity: quantity,
          traceId: traceId || this.metadata.traceId
        });
        return false;
      }

      const stackAmount = Math.min(quantity, item.stackSize);
      emptySlot.item = item;
      emptySlot.quantity = stackAmount;
      this.currentWeight += item.weight * stackAmount;
      quantity -= stackAmount;
    }

    this.updateEntity('inventory_system', traceId);

    logger.info('Item added to inventory', {
      inventoryId: this.entityId,
      itemId: item.itemId,
      quantity,
      newWeight: this.currentWeight,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public removeItem(itemId: string, quantity: number = 1, traceId?: string): boolean {
    let remainingToRemove = quantity;

    for (const [slotId, slot] of this.slots.entries()) {
      if (slot.item && slot.item.itemId === itemId && remainingToRemove > 0) {
        const removeAmount = Math.min(remainingToRemove, slot.quantity);
        slot.quantity -= removeAmount;
        this.currentWeight -= slot.item.weight * removeAmount;
        remainingToRemove -= removeAmount;

        if (slot.quantity === 0) {
          slot.item = undefined;
        }
      }
    }

    if (remainingToRemove > 0) {
      logger.warn('Could not remove all requested items', {
        inventoryId: this.entityId,
        itemId,
        requestedQuantity: quantity,
        removedQuantity: quantity - remainingToRemove,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    this.updateEntity('inventory_system', traceId);

    logger.info('Item removed from inventory', {
      inventoryId: this.entityId,
      itemId,
      quantity,
      newWeight: this.currentWeight,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public equipItem(slotId: string, equipmentSlot: string, traceId?: string): boolean {
    const inventorySlot = this.slots.get(slotId);
    const targetSlot = this.equipment.get(equipmentSlot);

    if (!inventorySlot || !inventorySlot.item || !targetSlot) {
      logger.warn('Invalid equip operation', {
        inventoryId: this.entityId,
        slotId,
        equipmentSlot,
        hasInventorySlot: !!inventorySlot,
        hasItem: !!inventorySlot?.item,
        hasTargetSlot: !!targetSlot,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    const item = inventorySlot.item;

    // Check if item can be equipped in this slot
    if (item.slot && item.slot !== equipmentSlot.replace(/\d+$/, '')) {
      logger.warn('Item cannot be equipped in this slot', {
        inventoryId: this.entityId,
        itemSlot: item.slot,
        targetSlot: equipmentSlot,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    // Unequip current item if any
    if (targetSlot.item) {
      this.addItem(targetSlot.item, 1, traceId);
    }

    // Equip new item
    targetSlot.item = item;
    inventorySlot.item = undefined;
    inventorySlot.quantity = 0;

    this.updateEntity('equipment_system', traceId);

    logger.info('Item equipped', {
      inventoryId: this.entityId,
      itemId: item.itemId,
      equipmentSlot,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public addCurrency(currencyType: string, amount: number, traceId?: string): void {
    const current = this.currency.get(currencyType) || 0;
    this.currency.set(currencyType, current + amount);
    this.updateEntity('currency_system', traceId);

    logger.info('Currency added', {
      inventoryId: this.entityId,
      currencyType,
      amount,
      newTotal: current + amount,
      traceId: traceId || this.metadata.traceId
    });
  }

  public removeCurrency(currencyType: string, amount: number, traceId?: string): boolean {
    const current = this.currency.get(currencyType) || 0;
    if (current < amount) {
      logger.warn('Insufficient currency', {
        inventoryId: this.entityId,
        currencyType,
        requested: amount,
        available: current,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    this.currency.set(currencyType, current - amount);
    this.updateEntity('currency_system', traceId);

    logger.info('Currency removed', {
      inventoryId: this.entityId,
      currencyType,
      amount,
      newTotal: current - amount,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }
}
