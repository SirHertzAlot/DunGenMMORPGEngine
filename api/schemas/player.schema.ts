
import { z } from 'zod';

export const CreatePlayerSchema = z.object({
  username: z.string()
    .min(3, 'Username must be at least 3 characters')
    .max(20, 'Username must be at most 20 characters')
    .regex(/^[a-zA-Z0-9_]+$/, 'Username can only contain letters, numbers, and underscores'),
  email: z.string().email('Invalid email format'),
  password: z.string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/, 'Password must contain at least one lowercase letter, one uppercase letter, and one number')
});

export const UpdatePlayerSchema = z.object({
  username: z.string().min(3).max(20).optional(),
  email: z.string().email().optional(),
  level: z.number().int().min(1).max(100).optional(),
  experience: z.number().int().min(0).optional(),
  position: z.object({
    x: z.number(),
    y: z.number(),
    z: z.number(),
    regionId: z.string()
  }).optional(),
  stats: z.object({
    health: z.number().min(0).max(1000),
    mana: z.number().min(0).max(1000),
    strength: z.number().min(1).max(100),
    agility: z.number().min(1).max(100),
    intelligence: z.number().min(1).max(100)
  }).optional()
});

export const PlayerLoginSchema = z.object({
  username: z.string().min(3),
  password: z.string().min(8)
});

export type CreatePlayerInput = z.infer<typeof CreatePlayerSchema>;
export type UpdatePlayerInput = z.infer<typeof UpdatePlayerSchema>;
export type PlayerLoginInput = z.infer<typeof PlayerLoginSchema>;
