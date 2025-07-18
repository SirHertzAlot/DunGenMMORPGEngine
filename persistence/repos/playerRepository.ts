
import { Player, IPlayer } from '../models/player.model';
import { logger } from '../../logging/logger';

export class PlayerRepository {
  async create(playerData: Partial<IPlayer>): Promise<IPlayer> {
    try {
      const player = new Player(playerData);
      const savedPlayer = await player.save();
      
      logger.info('Player created', {
        playerId: savedPlayer._id,
        username: savedPlayer.username
      });
      
      return savedPlayer;
    } catch (error) {
      logger.error('Error creating player', {
        error: (error as Error).message,
        playerData
      });
      throw error;
    }
  }

  async findById(playerId: string): Promise<IPlayer | null> {
    try {
      const player = await Player.findById(playerId).exec();
      return player;
    } catch (error) {
      logger.error('Error finding player by ID', {
        error: (error as Error).message,
        playerId
      });
      throw error;
    }
  }

  async findByUsername(username: string): Promise<IPlayer | null> {
    try {
      const player = await Player.findOne({ username }).exec();
      return player;
    } catch (error) {
      logger.error('Error finding player by username', {
        error: (error as Error).message,
        username
      });
      throw error;
    }
  }

  async findByRegion(regionId: string, limit: number = 100): Promise<IPlayer[]> {
    try {
      const players = await Player.find({
        'position.regionId': regionId,
        isOnline: true
      })
      .limit(limit)
      .select('username level position stats isOnline')
      .exec();
      
      return players;
    } catch (error) {
      logger.error('Error finding players by region', {
        error: (error as Error).message,
        regionId
      });
      throw error;
    }
  }

  async update(playerId: string, updateData: Partial<IPlayer>): Promise<IPlayer | null> {
    try {
      const player = await Player.findByIdAndUpdate(
        playerId,
        { $set: updateData },
        { new: true, runValidators: true }
      ).exec();
      
      if (player) {
        logger.info('Player updated', {
          playerId,
          username: player.username,
          updateData
        });
      }
      
      return player;
    } catch (error) {
      logger.error('Error updating player', {
        error: (error as Error).message,
        playerId,
        updateData
      });
      throw error;
    }
  }

  async delete(playerId: string): Promise<boolean> {
    try {
      const result = await Player.findByIdAndDelete(playerId).exec();
      
      if (result) {
        logger.info('Player deleted', {
          playerId,
          username: result.username
        });
        return true;
      }
      
      return false;
    } catch (error) {
      logger.error('Error deleting player', {
        error: (error as Error).message,
        playerId
      });
      throw error;
    }
  }

  async setOnlineStatus(playerId: string, isOnline: boolean): Promise<void> {
    try {
      await Player.findByIdAndUpdate(
        playerId,
        { 
          $set: { 
            isOnline,
            lastLogin: isOnline ? new Date() : undefined
          }
        }
      ).exec();
      
      logger.info('Player online status updated', {
        playerId,
        isOnline
      });
    } catch (error) {
      logger.error('Error updating player online status', {
        error: (error as Error).message,
        playerId,
        isOnline
      });
      throw error;
    }
  }
}

export const playerRepository = new PlayerRepository();
