
import { Request, Response } from 'express';
import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';
import { playerRepository } from '../../persistence/repos/playerRepository';
import { CreatePlayerSchema, PlayerLoginSchema, UpdatePlayerSchema } from '../schemas/player.schema';
import { AuthenticatedRequest } from '../middleware/auth';
import { logger } from '../../logging/logger';
import { gameEventQueue } from '../../etl/queues/gameEventQueue';
import { v4 as uuidv4 } from 'uuid';

export class PlayerController {
  async register(req: Request, res: Response): Promise<void> {
    try {
      const validatedData = CreatePlayerSchema.parse(req.body);

      // Check if username or email already exists
      const [existingUsername, existingEmail] = await Promise.all([
        playerRepository.findPlayerByUsername(validatedData.username),
        playerRepository.findPlayerByEmail(validatedData.email)
      ]);

      if (existingUsername) {
        res.status(409).json({
          error: 'Conflict',
          message: 'Username already exists'
        });
        return;
      }

      if (existingEmail) {
        res.status(409).json({
          error: 'Conflict',
          message: 'Email already exists'
        });
        return;
      }

      // Hash password
      const saltRounds = 12;
      const passwordHash = await bcrypt.hash(validatedData.password, saltRounds);

      // Create player
      const player = await playerRepository.createPlayer({
        username: validatedData.username,
        email: validatedData.email,
        passwordHash
      });

      // Generate JWT token
      const jwtSecret = process.env.JWT_SECRET!;
      const token = jwt.sign(
        { 
          userId: player._id, 
          username: player.username, 
          email: player.email 
        },
        jwtSecret,
        { expiresIn: process.env.JWT_EXPIRES_IN || '24h' }
      );

      // Publish player registration event
      await gameEventQueue.publishEvent({
        id: uuidv4(),
        type: 'player.registered',
        playerId: player._id,
        regionId: player.position.regionId,
        timestamp: Date.now(),
        data: {
          username: player.username,
          level: player.level
        }
      });

      res.status(201).json({
        message: 'Player registered successfully',
        data: {
          player: {
            id: player._id,
            username: player.username,
            email: player.email,
            level: player.level,
            position: player.position,
            stats: player.stats
          },
          token
        }
      });
    } catch (error) {
      if (error instanceof Error && error.name === 'ZodError') {
        res.status(400).json({
          error: 'Validation Error',
          message: 'Invalid input data',
          details: error.message
        });
        return;
      }

      logger.error('Player registration failed', { error: (error as Error).message });
      res.status(500).json({
        error: 'Internal Server Error',
        message: 'Failed to register player'
      });
    }
  }

  async login(req: Request, res: Response): Promise<void> {
    try {
      const validatedData = PlayerLoginSchema.parse(req.body);

      // Find player by username
      const player = await playerRepository.findPlayerByUsername(validatedData.username);
      if (!player) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'Invalid credentials'
        });
        return;
      }

      // Verify password
      const isValidPassword = await bcrypt.compare(validatedData.password, player.passwordHash);
      if (!isValidPassword) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'Invalid credentials'
        });
        return;
      }

      // Update online status
      await playerRepository.setPlayerOnlineStatus(player._id, true);

      // Generate JWT token
      const jwtSecret = process.env.JWT_SECRET!;
      const token = jwt.sign(
        { 
          userId: player._id, 
          username: player.username, 
          email: player.email 
        },
        jwtSecret,
        { expiresIn: process.env.JWT_EXPIRES_IN || '24h' }
      );

      // Publish login event
      await gameEventQueue.publishEvent({
        id: uuidv4(),
        type: 'player.login',
        playerId: player._id,
        regionId: player.position.regionId,
        timestamp: Date.now(),
        data: {
          username: player.username,
          level: player.level,
          position: player.position
        }
      });

      res.json({
        message: 'Login successful',
        data: {
          player: {
            id: player._id,
            username: player.username,
            email: player.email,
            level: player.level,
            position: player.position,
            stats: player.stats,
            inventory: player.inventory
          },
          token
        }
      });
    } catch (error) {
      if (error instanceof Error && error.name === 'ZodError') {
        res.status(400).json({
          error: 'Validation Error',
          message: 'Invalid input data',
          details: error.message
        });
        return;
      }

      logger.error('Player login failed', { error: (error as Error).message });
      res.status(500).json({
        error: 'Internal Server Error',
        message: 'Login failed'
      });
    }
  }

  async logout(req: AuthenticatedRequest, res: Response): Promise<void> {
    try {
      if (!req.user) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'User not authenticated'
        });
        return;
      }

      // Update online status
      await playerRepository.setPlayerOnlineStatus(req.user.id, false);

      // Publish logout event
      await gameEventQueue.publishEvent({
        id: uuidv4(),
        type: 'player.logout',
        playerId: req.user.id,
        regionId: 'unknown', // Could be fetched from player data if needed
        timestamp: Date.now(),
        data: {
          username: req.user.username
        }
      });

      res.json({
        message: 'Logout successful'
      });
    } catch (error) {
      logger.error('Player logout failed', { error: (error as Error).message });
      res.status(500).json({
        error: 'Internal Server Error',
        message: 'Logout failed'
      });
    }
  }

  async getProfile(req: AuthenticatedRequest, res: Response): Promise<void> {
    try {
      if (!req.user) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'User not authenticated'
        });
        return;
      }

      const player = await playerRepository.findPlayerById(req.user.id);
      if (!player) {
        res.status(404).json({
          error: 'Not Found',
          message: 'Player not found'
        });
        return;
      }

      res.json({
        data: {
          id: player._id,
          username: player.username,
          email: player.email,
          level: player.level,
          experience: player.experience,
          position: player.position,
          stats: player.stats,
          inventory: player.inventory,
          guild: player.guild,
          createdAt: player.createdAt,
          lastLogin: player.lastLogin,
          isOnline: player.isOnline
        }
      });
    } catch (error) {
      logger.error('Failed to get player profile', { error: (error as Error).message });
      res.status(500).json({
        error: 'Internal Server Error',
        message: 'Failed to retrieve player profile'
      });
    }
  }

  async updateProfile(req: AuthenticatedRequest, res: Response): Promise<void> {
    try {
      if (!req.user) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'User not authenticated'
        });
        return;
      }

      const validatedData = UpdatePlayerSchema.parse(req.body);
      
      const updatedPlayer = await playerRepository.updatePlayer(req.user.id, validatedData);
      if (!updatedPlayer) {
        res.status(404).json({
          error: 'Not Found',
          message: 'Player not found'
        });
        return;
      }

      // Publish profile update event
      await gameEventQueue.publishEvent({
        id: uuidv4(),
        type: 'player.profile_updated',
        playerId: req.user.id,
        regionId: updatedPlayer.position.regionId,
        timestamp: Date.now(),
        data: validatedData
      });

      res.json({
        message: 'Profile updated successfully',
        data: {
          id: updatedPlayer._id,
          username: updatedPlayer.username,
          email: updatedPlayer.email,
          level: updatedPlayer.level,
          experience: updatedPlayer.experience,
          position: updatedPlayer.position,
          stats: updatedPlayer.stats
        }
      });
    } catch (error) {
      if (error instanceof Error && error.name === 'ZodError') {
        res.status(400).json({
          error: 'Validation Error',
          message: 'Invalid input data',
          details: error.message
        });
        return;
      }

      logger.error('Failed to update player profile', { error: (error as Error).message });
      res.status(500).json({
        error: 'Internal Server Error',
        message: 'Failed to update player profile'
      });
    }
  }
}

export const playerController = new PlayerController();
