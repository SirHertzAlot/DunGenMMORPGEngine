
import mongoose, { Schema, Document } from 'mongoose';

export interface IPlayer extends Document {
  _id: string;
  username: string;
  email: string;
  passwordHash: string;
  level: number;
  experience: number;
  position: {
    x: number;
    y: number;
    z: number;
    regionId: string;
  };
  stats: {
    health: number;
    maxHealth: number;
    mana: number;
    maxMana: number;
    strength: number;
    agility: number;
    intelligence: number;
  };
  inventory: {
    items: Array<{
      itemId: string;
      quantity: number;
      slot: number;
    }>;
    capacity: number;
  };
  guild?: {
    guildId: string;
    role: string;
    joinedAt: Date;
  };
  createdAt: Date;
  updatedAt: Date;
  lastLogin: Date;
  isOnline: boolean;
}

const PlayerSchema = new Schema<IPlayer>({
  username: {
    type: String,
    required: true,
    unique: true,
    trim: true,
    minlength: 3,
    maxlength: 20
  },
  email: {
    type: String,
    required: true,
    unique: true,
    trim: true,
    lowercase: true
  },
  passwordHash: {
    type: String,
    required: true
  },
  level: {
    type: Number,
    default: 1,
    min: 1,
    max: 100
  },
  experience: {
    type: Number,
    default: 0,
    min: 0
  },
  position: {
    x: { type: Number, default: 0 },
    y: { type: Number, default: 0 },
    z: { type: Number, default: 0 },
    regionId: { type: String, default: 'starter_region' }
  },
  stats: {
    health: { type: Number, default: 100 },
    maxHealth: { type: Number, default: 100 },
    mana: { type: Number, default: 100 },
    maxMana: { type: Number, default: 100 },
    strength: { type: Number, default: 10 },
    agility: { type: Number, default: 10 },
    intelligence: { type: Number, default: 10 }
  },
  inventory: {
    items: [{
      itemId: { type: String, required: true },
      quantity: { type: Number, required: true, min: 1 },
      slot: { type: Number, required: true }
    }],
    capacity: { type: Number, default: 30 }
  },
  guild: {
    guildId: { type: String },
    role: { type: String, enum: ['member', 'officer', 'leader'] },
    joinedAt: { type: Date }
  },
  lastLogin: { type: Date, default: Date.now },
  isOnline: { type: Boolean, default: false }
}, {
  timestamps: true,
  collection: 'players'
});

// Indexes for performance
PlayerSchema.index({ username: 1 });
PlayerSchema.index({ email: 1 });
PlayerSchema.index({ 'position.regionId': 1 });
PlayerSchema.index({ level: -1 });
PlayerSchema.index({ isOnline: 1 });

export const Player = mongoose.model<IPlayer>('Player', PlayerSchema);
