
import { Player, IPlayer } from '../models/player.model';
import { logger } from '../../logging/logger';
import { CreatePlayerInput, UpdatePlayerInput } from '../../api/schemas/player.schema';

export class PlayerRepository {
  private static instance: PlayerRepository;

  private constructor() {}

  static getInstance(): PlayerRepository {
    if (!PlayerRepository.instance) {
      PlayerRepository.instance = new PlayerRepository();
    }
    return PlayerRepository.instance;
  }

  async createPlayer(playerData: CreatePlayerInput & { passwordHash: string }): Promise<IPlayer> {
    try {
      const player = new Player({
        username: playerData.username,
        email: playerData.email,
        passwordHash: playerData.passwordHash
      });

      const savedPlayer = await player.save();
      logger.info('Player created successfully', { playerId: savedPlayer._id, username: savedPlayer.username });
      return savedPlayer;
    } catch (error) {
      logger.error('Failed to create player', { error: (error as Error).message, playerData: { username: playerData.username, email: playerData.email } });
      throw error;
    }
  }

  async findPlayerById(playerId: string): Promise<IPlayer | null> {
    try {
      const player = await Player.findById(playerId).exec();
      return player;
    } catch (error) {
      logger.error('Failed to find player by ID', { error: (error as Error).message, playerId });
      throw error;
    }
  }

  async findPlayerByUsername(username: string): Promise<IPlayer | null> {
    try {
      const player = await Player.findOne({ username }).exec();
      return player;
    } catch (error) {
      logger.error('Failed to find player by username', { error: (error as Error).message, username });
      throw error;
    }
  }

  async findPlayerByEmail(email: string): Promise<IPlayer | null> {
    try {
      const player = await Player.findOne({ email }).exec();
      return player;
    } catch (error) {
      logger.error('Failed to find player by email', { error: (error as Error).message, email });
      throw error;
    }
  }

  async updatePlayer(playerId: string, updateData: UpdatePlayerInput): Promise<IPlayer | null> {
    try {
      const player = await Player.findByIdAndUpdate(
        playerId,
        { $set: updateData },
        { new: true, runValidators: true }
      ).exec();

      if (player) {
        logger.info('Player updated successfully', { playerId, updateData });
      }
      return player;
    } catch (error) {
      logger.error('Failed to update player', { error: (error as Error).message, playerId, updateData });
      throw error;
    }
  }

  async deletePlayer(playerId: string): Promise<boolean> {
    try {
      const result = await Player.findByIdAndDelete(playerId).exec();
      if (result) {
        logger.info('Player deleted successfully', { playerId });
        return true;
      }
      return false;
    } catch (error) {
      logger.error('Failed to delete player', { error: (error as Error).message, playerId });
      throw error;
    }
  }

  async setPlayerOnlineStatus(playerId: string, isOnline: boolean): Promise<void> {
    try {
      await Player.findByIdAndUpdate(
        playerId,
        { 
          isOnline,
          ...(isOnline ? {} : { lastLogin: new Date() })
        }
      ).exec();

      logger.info('Player online status updated', { playerId, isOnline });
    } catch (error) {
      logger.error('Failed to update player online status', { error: (error as Error).message, playerId, isOnline });
      throw error;
    }
  }

  async getPlayersByRegion(regionId: string): Promise<IPlayer[]> {
    try {
      const players = await Player.find({ 
        'position.regionId': regionId,
        isOnline: true 
      }).exec();
      return players;
    } catch (error) {
      logger.error('Failed to get players by region', { error: (error as Error).message, regionId });
      throw error;
    }
  }

  async updatePlayerPosition(playerId: string, position: { x: number; y: number; z: number; regionId: string }): Promise<IPlayer | null> {
    try {
      const player = await Player.findByIdAndUpdate(
        playerId,
        { $set: { position } },
        { new: true }
      ).exec();

      if (player) {
        logger.debug('Player position updated', { playerId, position });
      }
      return player;
    } catch (error) {
      logger.error('Failed to update player position', { error: (error as Error).message, playerId, position });
      throw error;
    }
  }
}

export const playerRepository = PlayerRepository.getInstance();
