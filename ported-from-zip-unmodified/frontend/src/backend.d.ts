import type { Principal } from "@icp-sdk/core/principal";
export interface Some<T> {
    __kind__: "Some";
    value: T;
}
export interface None {
    __kind__: "None";
}
export type Option<T> = Some<T> | None;
export class ExternalBlob {
    getBytes(): Promise<Uint8Array<ArrayBuffer>>;
    getDirectURL(): string;
    static fromURL(url: string): ExternalBlob;
    static fromBytes(blob: Uint8Array<ArrayBuffer>): ExternalBlob;
    withUploadProgress(onProgress: (percentage: number) => void): ExternalBlob;
}
export interface RiverSettings {
    channelStr: number;
    rainfallAmt: number;
    evaporation: number;
    flowMultiplier: number;
    maxRiverLength: bigint;
    tributaryDensity: number;
    flowThreshold: number;
    springFreq: number;
    lakeDepth: number;
    riverWidth: number;
}
export interface NoiseSettings {
    roughness: number;
    radialWeight: number;
    scale: number;
    heightRange: number;
    noiseSeed: bigint;
    intensity: number;
}
export interface GameObject {
    id: Uint8Array;
    behavior: Behavior;
    owner: Principal;
    appearance: Appearance;
    animation: Animation;
    position: Position;
    objectType: ObjectType;
}
export interface WindSettings {
    direction: number;
    iterations: bigint;
    erosionRate: number;
    sedimentTransfer: number;
    windStrength: number;
}
export interface ErosionSettings {
    biomeFactors: BiomeFactors;
    wind: WindSettings;
    hydraulic: HydraulicSettings;
    mountain: MountainSettings;
    thermal: ThermalSettings;
    river: RiverSettings;
}
export type WeaponObjectType = {
    __kind__: "axe";
    axe: null;
} | {
    __kind__: "bow";
    bow: null;
} | {
    __kind__: "dagger";
    dagger: null;
} | {
    __kind__: "shield";
    shield: null;
} | {
    __kind__: "custom";
    custom: string;
} | {
    __kind__: "mace";
    mace: null;
} | {
    __kind__: "wand";
    wand: null;
} | {
    __kind__: "spear";
    spear: null;
} | {
    __kind__: "staff";
    staff: null;
} | {
    __kind__: "sword";
    sword: null;
};
export interface StatusEffect {
    name: string;
    effectId: Uint8Array;
    isBuff: boolean;
    description: string;
    durationSeconds: number;
    magnitude: number;
}
export interface Resistances {
    physicalResistance: number;
    poisonResistance: number;
    iceResistance: number;
    lightningResistance: number;
    fireResistance: number;
}
export interface Stats {
    stamina: bigint;
    strength: bigint;
    defense: bigint;
    agility: bigint;
    charisma: bigint;
    intelligence: bigint;
}
export interface TierDefinition {
    id: Uint8Array;
    rarityPercentage: number;
    owner: Principal;
    name: string;
    statRanges: StatRanges;
    level: TierLevel;
    specialAttributes: Array<string>;
}
export interface MasterySkill {
    id: Uint8Array;
    owner: Principal;
    name: string;
    description: string;
    requiredMasteryLevel: bigint;
    objectType: WeaponObjectType;
}
export interface StealthSystem {
    isHidden: boolean;
}
export interface LootItem {
    id: Uint8Array;
    statBuffs?: StatBuffs;
    debuffAbilities?: Array<string>;
    owner: Principal;
    name: string;
    worldBreakingPowers?: WorldBreakingPowers;
    tier: TierLevel;
    defensePower: bigint;
    attackPower: bigint;
    specialTraits: Array<string>;
    resistances?: Resistances;
    elementalPower: bigint;
    isExcellent: boolean;
}
export interface Health {
    max: bigint;
    current: bigint;
}
export interface CombatSystem {
    damage: bigint;
    armor: bigint;
    canAttack: boolean;
    weaponType: string;
    experience: bigint;
}
export interface CellConfig {
    noiseSettings: NoiseSettings;
    lightingSettings: LightingSettings;
    erosionSettings: ErosionSettings;
}
export interface Animation {
    run: string;
    hasIdle: boolean;
    hasWalk: boolean;
    hasAttack: boolean;
    idle: string;
    walk: string;
    emote: string;
    hasRun: boolean;
    attack: string;
    hasEmote: boolean;
}
export interface BiomeFactors {
    biomeType: string;
    forestDensity: number;
    vegetation: number;
    soilQuality: number;
}
export interface Movement {
    canJump: boolean;
    aggroRadius: number;
    speed: number;
    canFly: boolean;
}
export interface HydraulicSettings {
    waterVolume: number;
    depositionRate: number;
    evaporationRate: number;
    transferRate: number;
    sedimentCap: number;
    iterations: bigint;
    erosionStr: number;
    rainFreq: number;
    rainAmount: number;
    soilTransfer: number;
}
export interface Color {
    b: number;
    g: number;
    r: number;
}
export interface Interaction {
    interactionType: string;
    canInteract: boolean;
    interactionRange: number;
}
export interface StatBuffs {
    manaBonus: bigint;
    agilityBonus: bigint;
    strengthBonus: bigint;
    healthBonus: bigint;
}
export interface MasteryTree {
    owner: Principal;
    objectType: WeaponObjectType;
    skills: Array<Uint8Array>;
}
export interface MobEntity {
    id: Uint8Array;
    dangerRating: bigint;
    behavior: Behavior;
    activeDebuffs: Array<StatusEffect>;
    activeBuffs: Array<StatusEffect>;
    owner: Principal;
    appearance: Appearance;
    name: string;
    animation: Animation;
    mobType: MobType;
    position: Position;
    health: Health;
}
export interface Navigation {
    waypoints: Array<Position>;
    canNavigate: boolean;
    patrolPaths: Array<Position>;
}
export interface MountainSettings {
    flatteningStr: number;
    threshold: number;
    plateauHeight: number;
    smoothingStr: number;
    snowLine: number;
    snowCoverage: number;
}
export interface FileMetadata {
    id: Uint8Array;
    extractionSource?: Uint8Array;
    owner: Principal;
    file: ExternalBlob;
    name: string;
    size: bigint;
    fileType: FileType;
    uploadTimestamp: bigint;
    relativePath: string;
    archiveType?: ArchiveType;
    isDirectory: boolean;
}
export interface WorldBreakingPowers {
    efficientMana: boolean;
    allySummoning: boolean;
    aoeUltimates: boolean;
    reducedCooldowns: boolean;
}
export interface ThermalSettings {
    threshold: number;
    transferRatio: number;
    gravityStrength: number;
    creepFactor: number;
    incubation: number;
}
export interface CharacterMasteryProgress {
    masteryPoints: bigint;
    owner: Principal;
    masteryLevel: bigint;
    unlockedSkillIds: Array<Uint8Array>;
    characterId: Uint8Array;
    objectType: WeaponObjectType;
}
export interface ItemInstanceMastery {
    masteryPoints: bigint;
    owner: Principal;
    masteryLevel: bigint;
    itemInstanceId: Uint8Array;
    characterId: Uint8Array;
    objectType: WeaponObjectType;
}
export interface GridConfig {
    dim: bigint;
    owner: Principal;
    cells: Array<Array<CellConfig>>;
}
export interface Position {
    x: number;
    y: number;
    z: number;
}
export interface Behavior {
    movement: Movement;
    inventorySystem: InventorySystem;
    interaction: Interaction;
    navigation: Navigation;
    combatSystem: CombatSystem;
    stealthSystem: StealthSystem;
    health: Health;
}
export interface InventorySystem {
    currentInventory: bigint;
    inventoryCapacity: bigint;
}
export interface Appearance {
    pattern: string;
    color: Color;
    outfitColor: Color;
    stats: Stats;
    skinTone: string;
    material: string;
}
export interface StatRanges {
    maxDefense: bigint;
    maxAttack: bigint;
    minAttack: bigint;
    maxElemental: bigint;
    minDefense: bigint;
    minElemental: bigint;
}
export interface LightingSettings {
    sunlight: number;
    shadowDepth: number;
    directionalIntensity: number;
    elevationTransform: number;
    ambientLight: number;
    intensity: number;
}
export type MobType = {
    __kind__: "orc";
    orc: null;
} | {
    __kind__: "troll";
    troll: null;
} | {
    __kind__: "custom";
    custom: string;
} | {
    __kind__: "goblin";
    goblin: null;
} | {
    __kind__: "wraith";
    wraith: null;
} | {
    __kind__: "skeleton";
    skeleton: null;
};
export interface Heightmap {
    dim: bigint;
    externalBlob: ExternalBlob;
    owner: Principal;
}
export interface UserProfile {
    name: string;
}
export interface MasteryRollResult {
    skillsUnlocked: bigint;
    total: bigint;
    roll1: bigint;
    roll2: bigint;
}
export enum ArchiveType {
    tar = "tar",
    zip = "zip"
}
export enum FileType {
    FBX = "FBX",
    GLB = "GLB",
    OBJ = "OBJ",
    GLTF = "GLTF"
}
export enum ObjectType {
    interactiveObject = "interactiveObject",
    playerCharacter = "playerCharacter",
    environmentalObject = "environmentalObject",
    collectible = "collectible",
    vehicle = "vehicle",
    nonPlayerCharacter = "nonPlayerCharacter",
    weapon = "weapon"
}
export enum TierLevel {
    epic = "epic",
    legendary = "legendary",
    rare = "rare",
    common = "common"
}
export enum UserRole {
    admin = "admin",
    user = "user",
    guest = "guest"
}
export interface backendInterface {
    applyBuffToMob(mobId: Uint8Array, buff: StatusEffect): Promise<void>;
    applyDebuffToMob(mobId: Uint8Array, debuff: StatusEffect): Promise<void>;
    assignCallerUserRole(user: Principal, role: UserRole): Promise<void>;
    clearAllLootItems(): Promise<void>;
    clearMobBuffs(mobId: Uint8Array): Promise<void>;
    clearMobDebuffs(mobId: Uint8Array): Promise<void>;
    createDefaultMobEntity(id: Uint8Array, mobType: MobType, name: string, position: Position, behavior: Behavior, animation: Animation, appearance: Appearance): Promise<MobEntity>;
    deleteFile(id: Uint8Array): Promise<void>;
    deleteLootItem(id: Uint8Array): Promise<void>;
    deleteMasterySkill(id: Uint8Array): Promise<void>;
    deleteMobEntity(id: Uint8Array): Promise<void>;
    deleteObjectPrefab(id: Uint8Array): Promise<void>;
    deleteTierDefinition(id: Uint8Array): Promise<void>;
    getAllCharacterMasteryProgress(characterId: Uint8Array): Promise<Array<CharacterMasteryProgress>>;
    getAllGridConfigs(): Promise<Array<[Uint8Array, GridConfig]>>;
    getAllItemInstanceMasteries(characterId: Uint8Array): Promise<Array<ItemInstanceMastery>>;
    getAllLootItems(): Promise<Array<LootItem>>;
    getAllMobEntities(): Promise<Array<MobEntity>>;
    getAllTierDefinitions(): Promise<Array<TierDefinition>>;
    getCallerUserProfile(): Promise<UserProfile | null>;
    getCallerUserRole(): Promise<UserRole>;
    getCharacterMasteryProgress(characterId: Uint8Array, objectType: WeaponObjectType): Promise<CharacterMasteryProgress | null>;
    getDirectoryContents(directoryId: Uint8Array): Promise<Array<FileMetadata>>;
    getFile(id: Uint8Array): Promise<FileMetadata | null>;
    getFiles(): Promise<Array<FileMetadata>>;
    getFilesByExtractionSource(extractionSource: Uint8Array): Promise<Array<FileMetadata>>;
    getGridConfig(uuid: Uint8Array): Promise<GridConfig | null>;
    getHeightmap(uuid: Uint8Array): Promise<Heightmap | null>;
    getItemInstanceMastery(characterId: Uint8Array, itemInstanceId: Uint8Array): Promise<ItemInstanceMastery | null>;
    getLootItem(id: Uint8Array): Promise<LootItem | null>;
    getLootItemsByTier(tier: TierLevel): Promise<Array<LootItem>>;
    getLootItemsPaginated(offset: bigint, limit: bigint): Promise<Array<LootItem>>;
    getLootTableStats(): Promise<{
        commonCount: bigint;
        rareCount: bigint;
        legendaryCount: bigint;
        excellentCount: bigint;
        epicCount: bigint;
        totalItems: bigint;
    }>;
    getMasterySkill(id: Uint8Array): Promise<MasterySkill | null>;
    getMasterySkillsByObjectType(objectType: WeaponObjectType): Promise<Array<MasterySkill>>;
    getMasteryTree(objectType: WeaponObjectType): Promise<MasteryTree | null>;
    getMobBuffDebuffState(mobId: Uint8Array): Promise<{
        dangerRating: bigint;
        activeDebuffs: Array<StatusEffect>;
        activeBuffs: Array<StatusEffect>;
    } | null>;
    getMobEntitiesByDangerRating(dangerRating: bigint): Promise<Array<MobEntity>>;
    getMobEntity(id: Uint8Array): Promise<MobEntity | null>;
    getObjectPrefab(id: Uint8Array): Promise<GameObject | null>;
    getObjectPrefabs(): Promise<Array<GameObject>>;
    getTierDefinition(id: Uint8Array): Promise<TierDefinition | null>;
    getUserProfile(user: Principal): Promise<UserProfile | null>;
    isCallerAdmin(): Promise<boolean>;
    rollMasteryAdvancement(characterId: Uint8Array, objectType: WeaponObjectType, seed: bigint): Promise<MasteryRollResult>;
    saveCallerUserProfile(profile: UserProfile): Promise<void>;
    saveCharacterMasteryProgress(progress: CharacterMasteryProgress): Promise<void>;
    saveFile(id: Uint8Array, file: ExternalBlob, name: string, size: bigint, fileType: FileType, uploadTimestamp: bigint, extractionSource: Uint8Array | null, relativePath: string, isDirectory: boolean, archiveType: ArchiveType | null): Promise<void>;
    saveGridConfig(uuid: Uint8Array, config: GridConfig): Promise<void>;
    saveHeightmap(uuid: Uint8Array, blob: ExternalBlob, dim: bigint): Promise<void>;
    saveItemInstanceMastery(mastery: ItemInstanceMastery): Promise<void>;
    saveLootItem(id: Uint8Array, item: LootItem): Promise<void>;
    saveLootItemBatch(items: Array<[Uint8Array, LootItem]>): Promise<void>;
    saveMasterySkill(id: Uint8Array, skill: MasterySkill): Promise<void>;
    saveMasteryTree(objectType: WeaponObjectType, skillIds: Array<Uint8Array>): Promise<void>;
    saveMobEntity(id: Uint8Array, mob: MobEntity): Promise<void>;
    saveObjectPrefab(id: Uint8Array, prefab: GameObject): Promise<void>;
    saveTierDefinition(id: Uint8Array, tier: TierDefinition): Promise<void>;
}
