
import { BaseEntity } from '../../core/base/BaseEntity';
import { logger } from '../../logging/logger';

export enum GuildRank {
  MEMBER = 'member',
  OFFICER = 'officer',
  VICE_LEADER = 'vice_leader',
  LEADER = 'leader'
}

export interface IGuildMember {
  characterId: string;
  characterName: string;
  rank: GuildRank;
  joinedAt: Date;
  lastActiveAt: Date;
  contributions: Map<string, number>;
  permissions: Set<string>;
}

export interface IGuildPerks {
  experienceBonus: number;
  goldBonus: number;
  memberCapacity: number;
  bankSlots: number;
  guildHall: boolean;
}

export class Guild extends BaseEntity {
  public name: string;
  public tag: string;
  public description: string;
  public motd: string;
  public leaderId: string;
  public members: Map<string, IGuildMember>;
  public treasury: Map<string, number>;
  public level: number;
  public experience: number;
  public experienceToNext: number;
  public perks: IGuildPerks;
  public reputation: number;
  public isRecruiting: boolean;
  public requirements: {
    minLevel: number;
    applicationRequired: boolean;
  };

  constructor(
    name: string,
    tag: string,
    description: string,
    leaderId: string,
    leaderName: string,
    regionId: string,
    createdBy: string = 'system',
    traceId?: string
  ) {
    super(regionId, createdBy, traceId);

    this.name = name;
    this.tag = tag;
    this.description = description;
    this.motd = 'Welcome to the guild!';
    this.leaderId = leaderId;
    this.members = new Map();
    this.treasury = new Map();
    this.level = 1;
    this.experience = 0;
    this.experienceToNext = 10000;
    this.reputation = 0;
    this.isRecruiting = true;
    this.requirements = {
      minLevel: 1,
      applicationRequired: false
    };

    this.perks = {
      experienceBonus: 0.05,
      goldBonus: 0.02,
      memberCapacity: 50,
      bankSlots: 100,
      guildHall: false
    };

    // Add leader as first member
    this.members.set(leaderId, {
      characterId: leaderId,
      characterName: leaderName,
      rank: GuildRank.LEADER,
      joinedAt: new Date(),
      lastActiveAt: new Date(),
      contributions: new Map(),
      permissions: new Set(['invite', 'kick', 'promote', 'demote', 'edit_info', 'manage_treasury'])
    });

    logger.info('Guild created', {
      guildId: this.entityId,
      name: this.name,
      tag: this.tag,
      leaderId: this.leaderId,
      regionId: this.regionId,
      traceId: this.metadata.traceId
    });
  }

  public addMember(characterId: string, characterName: string, traceId?: string): boolean {
    if (this.members.has(characterId)) {
      logger.warn('Character already in guild', {
        guildId: this.entityId,
        characterId,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    if (this.members.size >= this.perks.memberCapacity) {
      logger.warn('Guild at member capacity', {
        guildId: this.entityId,
        currentMembers: this.members.size,
        capacity: this.perks.memberCapacity,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    this.members.set(characterId, {
      characterId,
      characterName,
      rank: GuildRank.MEMBER,
      joinedAt: new Date(),
      lastActiveAt: new Date(),
      contributions: new Map(),
      permissions: new Set(['chat'])
    });

    this.updateEntity('guild_system', traceId);

    logger.info('Member added to guild', {
      guildId: this.entityId,
      characterId,
      characterName,
      memberCount: this.members.size,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public removeMember(characterId: string, removedBy: string, traceId?: string): boolean {
    const member = this.members.get(characterId);
    if (!member) {
      logger.warn('Member not found in guild', {
        guildId: this.entityId,
        characterId,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    if (member.rank === GuildRank.LEADER) {
      logger.warn('Cannot remove guild leader', {
        guildId: this.entityId,
        characterId,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    this.members.delete(characterId);
    this.updateEntity('guild_system', traceId);

    logger.info('Member removed from guild', {
      guildId: this.entityId,
      characterId,
      characterName: member.characterName,
      removedBy,
      memberCount: this.members.size,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public promoteMember(characterId: string, promotedBy: string, traceId?: string): boolean {
    const member = this.members.get(characterId);
    if (!member) {
      logger.warn('Member not found for promotion', {
        guildId: this.entityId,
        characterId,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    const nextRank = this.getNextRank(member.rank);
    if (!nextRank) {
      logger.warn('Member already at highest rank', {
        guildId: this.entityId,
        characterId,
        currentRank: member.rank,
        traceId: traceId || this.metadata.traceId
      });
      return false;
    }

    member.rank = nextRank;
    member.permissions = this.getPermissionsForRank(nextRank);
    this.updateEntity('guild_system', traceId);

    logger.info('Member promoted', {
      guildId: this.entityId,
      characterId,
      characterName: member.characterName,
      newRank: nextRank,
      promotedBy,
      traceId: traceId || this.metadata.traceId
    });

    return true;
  }

  public addExperience(amount: number, source: string, traceId?: string): boolean {
    const oldLevel = this.level;
    this.experience += amount;

    while (this.experience >= this.experienceToNext && this.level < 100) {
      this.levelUp(traceId);
    }

    this.updateEntity('guild_experience_system', traceId);

    logger.info('Guild experience gained', {
      guildId: this.entityId,
      amount,
      source,
      newExperience: this.experience,
      levelUp: this.level > oldLevel,
      traceId: traceId || this.metadata.traceId
    });

    return this.level > oldLevel;
  }

  private levelUp(traceId?: string): void {
    this.level += 1;
    this.experience -= this.experienceToNext;
    this.experienceToNext = Math.floor(this.experienceToNext * 1.2);

    // Improve guild perks
    this.perks.experienceBonus += 0.01;
    this.perks.goldBonus += 0.005;
    this.perks.memberCapacity += 5;
    this.perks.bankSlots += 10;

    if (this.level >= 10 && !this.perks.guildHall) {
      this.perks.guildHall = true;
    }

    logger.info('Guild leveled up', {
      guildId: this.entityId,
      newLevel: this.level,
      newPerks: this.perks,
      traceId: traceId || this.metadata.traceId
    });
  }

  private getNextRank(currentRank: GuildRank): GuildRank | null {
    const rankOrder = [GuildRank.MEMBER, GuildRank.OFFICER, GuildRank.VICE_LEADER, GuildRank.LEADER];
    const currentIndex = rankOrder.indexOf(currentRank);
    return currentIndex < rankOrder.length - 1 ? rankOrder[currentIndex + 1] : null;
  }

  private getPermissionsForRank(rank: GuildRank): Set<string> {
    const permissions = new Set(['chat']);
    
    switch (rank) {
      case GuildRank.LEADER:
        permissions.add('disband');
        permissions.add('transfer_leadership');
        // fallthrough
      case GuildRank.VICE_LEADER:
        permissions.add('manage_treasury');
        permissions.add('edit_info');
        permissions.add('manage_perks');
        // fallthrough
      case GuildRank.OFFICER:
        permissions.add('invite');
        permissions.add('kick');
        permissions.add('promote');
        permissions.add('demote');
        permissions.add('guild_events');
        // fallthrough
      case GuildRank.MEMBER:
        break;
    }
    
    return permissions;
  }
}
