import List "mo:core/List";
import Map "mo:core/Map";
import Principal "mo:core/Principal";
import Runtime "mo:core/Runtime";
import Storage "blob-storage/Storage";
import Array "mo:core/Array";
import Nat "mo:core/Nat";
import Float "mo:core/Float";
import Int "mo:core/Int";
import AccessControl "authorization/access-control";
import MixinAuthorization "authorization/MixinAuthorization";
import MixinStorage "blob-storage/Mixin";
import Text "mo:core/Text";
import Debug "mo:core/Debug";
import Blob "mo:core/Blob";
import Char "mo:core/Char";
import Iter "mo:core/Iter";
import Nat8 "mo:core/Nat8";

actor {
  let accessControlState = AccessControl.initState();
  include MixinAuthorization(accessControlState);
  include MixinStorage();

  let MAX_FILE_SIZE : Nat = 3_800_000;

  public type UserProfile = {
    name : Text;
  };

  let userProfiles = Map.empty<Principal, UserProfile>();

  public query ({ caller }) func getCallerUserProfile() : async ?UserProfile {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can access profiles");
    };
    userProfiles.get(caller);
  };

  public query ({ caller }) func getUserProfile(user : Principal) : async ?UserProfile {
    if (caller != user and not AccessControl.isAdmin(accessControlState, caller)) {
      Runtime.trap("Unauthorized: Can only view your own profile");
    };
    userProfiles.get(user);
  };

  public shared ({ caller }) func saveCallerUserProfile(profile : UserProfile) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save profiles");
    };
    userProfiles.add(caller, profile);
  };

  public type FileMetadata = {
    id : Blob;
    file : Storage.ExternalBlob;
    name : Text;
    size : Nat;
    fileType : FileType;
    uploadTimestamp : Int;
    owner : Principal;
    extractionSource : ?Blob;
    relativePath : Text;
    isDirectory : Bool;
    archiveType : ?ArchiveType;
  };

  type FileMetadataInternal = FileMetadata;

  public type FileType = {
    #GLB;
    #GLTF;
    #OBJ;
    #FBX;
  };

  public type ArchiveType = {
    #zip;
    #tar;
  };

  let files = Map.empty<Blob, FileMetadataInternal>();

  public shared ({ caller }) func saveFile(
    id : Blob,
    file : Storage.ExternalBlob,
    name : Text,
    size : Nat,
    fileType : FileType,
    uploadTimestamp : Int,
    extractionSource : ?Blob,
    relativePath : Text,
    isDirectory : Bool,
    archiveType : ?ArchiveType,
  ) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can upload files");
    };

    if (size > MAX_FILE_SIZE) {
      Runtime.trap("File size exceeds maximum limit of 3.8 MB");
    };

    switch (extractionSource) {
      case (?sourceId) {
        switch (files.get(sourceId)) {
          case (null) {
            Runtime.trap("Extraction source file not found");
          };
          case (?sourceFile) {
            if (sourceFile.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
              Runtime.trap("Unauthorized: You don't own the source archive");
            };
          };
        };
      };
      case (null) {};
    };

    let fileMetadata : FileMetadataInternal = {
      id;
      file;
      name;
      size;
      fileType;
      uploadTimestamp;
      owner = caller;
      extractionSource;
      relativePath;
      isDirectory;
      archiveType;
    };
    files.add(id, fileMetadata);
  };

  public query ({ caller }) func getFiles() : async [FileMetadata] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view files");
    };

    if (AccessControl.isAdmin(accessControlState, caller)) {
      files.values().toArray().map(func(f) { f });
    } else {
      files.values().filter(
        func(f : FileMetadataInternal) : Bool {
          Principal.equal(f.owner, caller);
        }
      ).toArray().map(func(f) { f });
    };
  };

  public query ({ caller }) func getFile(id : Blob) : async ?FileMetadata {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view files");
    };

    switch (files.get(id)) {
      case (null) { null };
      case (?file) {
        if (Principal.equal(file.owner, caller) or AccessControl.isAdmin(accessControlState, caller)) {
          ?file;
        } else {
          Runtime.trap("Unauthorized: You can only view your own files");
        };
      };
    };
  };

  public query ({ caller }) func getFilesByExtractionSource(
    extractionSource : Blob,
  ) : async [FileMetadata] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view files");
    };

    switch (files.get(extractionSource)) {
      case (null) {
        Runtime.trap("Extraction source file not found");
      };
      case (?sourceFile) {
        if (sourceFile.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You don't own the source archive");
        };
      };
    };

    files.values().filter(
      func(f : FileMetadataInternal) : Bool {
        let matchesSource = switch (f.extractionSource) {
          case (?source) { Blob.equal(source, extractionSource) };
          case (null) { false };
        };
        matchesSource;
      }
    ).toArray().map(func(f) { f });
  };

  public query ({ caller }) func getDirectoryContents(
    directoryId : Blob,
  ) : async [FileMetadata] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view directory contents");
    };

    switch (files.get(directoryId)) {
      case (null) {
        Runtime.trap("Directory not found");
      };
      case (?dir) {
        if (dir.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You don't own this directory");
        };
      };
    };

    files.values().filter(
      func(f : FileMetadataInternal) : Bool {
        if (f.isDirectory) {
          false;
        } else {
          let matchesDir = switch (f.extractionSource) {
            case (?source) { Blob.equal(source, directoryId) };
            case (null) { false };
          };
          matchesDir;
        };
      }
    ).toArray().map(func(f) { f });
  };

  public shared ({ caller }) func deleteFile(id : Blob) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can delete files");
    };

    switch (files.get(id)) {
      case (null) {
        Runtime.trap("File not found");
      };
      case (?file) {
        if (file.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You can only delete your own files");
        };
        files.remove(id);
      };
    };
  };

  public type GridConfig = {
    dim : Nat;
    cells : [[CellConfig]];
    owner : Principal;
  };

  public type CellConfig = {
    noiseSettings : NoiseSettings;
    erosionSettings : ErosionSettings;
    lightingSettings : LightingSettings;
  };

  public type NoiseSettings = {
    noiseSeed : Nat;
    scale : Float;
    roughness : Float;
    intensity : Float;
    radialWeight : Float;
    heightRange : Float;
  };

  public type ErosionSettings = {
    hydraulic : HydraulicSettings;
    thermal : ThermalSettings;
    wind : WindSettings;
    mountain : MountainSettings;
    river : RiverSettings;
    biomeFactors : BiomeFactors;
  };

  public type HydraulicSettings = {
    rainAmount : Float;
    waterVolume : Float;
    rainFreq : Float;
    evaporationRate : Float;
    erosionStr : Float;
    sedimentCap : Float;
    iterations : Nat;
    transferRate : Float;
    soilTransfer : Float;
    depositionRate : Float;
  };

  public type ThermalSettings = {
    threshold : Float;
    gravityStrength : Float;
    incubation : Float;
    creepFactor : Float;
    transferRatio : Float;
  };

  public type WindSettings = {
    direction : Float;
    erosionRate : Float;
    sedimentTransfer : Float;
    windStrength : Float;
    iterations : Nat;
  };

  public type MountainSettings = {
    threshold : Float;
    plateauHeight : Float;
    flatteningStr : Float;
    smoothingStr : Float;
    snowLine : Float;
    snowCoverage : Float;
  };

  public type RiverSettings = {
    flowThreshold : Float;
    lakeDepth : Float;
    riverWidth : Float;
    flowMultiplier : Float;
    channelStr : Float;
    evaporation : Float;
    tributaryDensity : Float;
    springFreq : Float;
    rainfallAmt : Float;
    maxRiverLength : Nat;
  };

  public type BiomeFactors = {
    biomeType : Text;
    forestDensity : Float;
    vegetation : Float;
    soilQuality : Float;
  };

  public type LightingSettings = {
    intensity : Float;
    ambientLight : Float;
    directionalIntensity : Float;
    sunlight : Float;
    shadowDepth : Float;
    elevationTransform : Float;
  };

  let gridConfigs = Map.empty<Blob, GridConfig>();

  public shared ({ caller }) func saveGridConfig(uuid : Blob, config : GridConfig) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save grid configurations");
    };

    switch (gridConfigs.get(uuid)) {
      case (null) {
        let configWithOwner = {
          dim = config.dim;
          cells = config.cells;
          owner = caller;
        };
        gridConfigs.add(uuid, configWithOwner);
      };
      case (?existing) {
        if (existing.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You can only modify your own grid configurations");
        };
        let configWithOwner = {
          dim = config.dim;
          cells = config.cells;
          owner = existing.owner;
        };
        gridConfigs.add(uuid, configWithOwner);
      };
    };
  };

  public query ({ caller }) func getGridConfig(uuid : Blob) : async ?GridConfig {
    switch (gridConfigs.get(uuid)) {
      case (null) { null };
      case (?config) {
        if (Principal.equal(config.owner, caller) or AccessControl.isAdmin(accessControlState, caller)) {
          ?config;
        } else {
          Runtime.trap("Unauthorized: You can only view your own grid configurations");
        };
      };
    };
  };

  public query ({ caller }) func getAllGridConfigs() : async [(Blob, GridConfig)] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view grid configurations");
    };

    if (AccessControl.isAdmin(accessControlState, caller)) {
      gridConfigs.toArray();
    } else {
      gridConfigs.toArray().filter(
        func((uuid : Blob, config : GridConfig)) : Bool {
          Principal.equal(config.owner, caller);
        }
      );
    };
  };

  public type GameObject = {
    id : Blob;
    objectType : ObjectType;
    position : Position;
    behavior : Behavior;
    animation : Animation;
    appearance : Appearance;
    owner : Principal;
  };

  public type ObjectType = {
    #nonPlayerCharacter;
    #playerCharacter;
    #weapon;
    #vehicle;
    #environmentalObject;
    #interactiveObject;
    #collectible;
  };

  public type Position = {
    x : Float;
    y : Float;
    z : Float;
  };

  public type Behavior = {
    health : Health;
    movement : Movement;
    interaction : Interaction;
    inventorySystem : InventorySystem;
    stealthSystem : StealthSystem;
    combatSystem : CombatSystem;
    navigation : Navigation;
  };

  public type Health = {
    max : Int;
    current : Int;
  };

  public type Movement = {
    speed : Float;
    aggroRadius : Float;
    canJump : Bool;
    canFly : Bool;
  };

  public type Interaction = {
    canInteract : Bool;
    interactionType : Text;
    interactionRange : Float;
  };

  public type InventorySystem = {
    inventoryCapacity : Nat;
    currentInventory : Nat;
  };

  public type StealthSystem = {
    isHidden : Bool;
  };

  public type CombatSystem = {
    damage : Int;
    armor : Int;
    weaponType : Text;
    canAttack : Bool;
    experience : Nat;
  };

  public type Navigation = {
    waypoints : [Position];
    patrolPaths : [Position];
    canNavigate : Bool;
  };

  public type Animation = {
    hasIdle : Bool;
    idle : Text;
    hasWalk : Bool;
    walk : Text;
    hasRun : Bool;
    run : Text;
    hasAttack : Bool;
    attack : Text;
    hasEmote : Bool;
    emote : Text;
  };

  public type Appearance = {
    color : Color;
    skinTone : Text;
    outfitColor : Color;
    pattern : Text;
    material : Text;
    stats : Stats;
  };

  public type Color = {
    r : Float;
    g : Float;
    b : Float;
  };

  public type Stats = {
    strength : Int;
    agility : Int;
    intelligence : Int;
    charisma : Int;
    stamina : Int;
    defense : Int;
  };

  let objectPrefabMap = Map.empty<Blob, GameObject>();

  public shared ({ caller }) func saveObjectPrefab(id : Blob, prefab : GameObject) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save game objects");
    };

    let prefabWithOwner = {
      prefab with owner = caller
    };
    objectPrefabMap.add(id, prefabWithOwner);
  };

  public query ({ caller }) func getObjectPrefab(id : Blob) : async ?GameObject {
    switch (objectPrefabMap.get(id)) {
      case (null) { null };
      case (?prefab) {
        if (Principal.equal(prefab.owner, caller) or AccessControl.isAdmin(accessControlState, caller)) {
          ?prefab;
        } else {
          Runtime.trap("Unauthorized: You can only view your own game objects");
        };
      };
    };
  };

  public query ({ caller }) func getObjectPrefabs() : async [GameObject] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view game objects");
    };

    if (AccessControl.isAdmin(accessControlState, caller)) {
      objectPrefabMap.values().toArray();
    } else {
      objectPrefabMap.values().filter(
        func(prefab : GameObject) : Bool {
          Principal.equal(prefab.owner, caller);
        }
      ).toArray();
    };
  };

  public shared ({ caller }) func deleteObjectPrefab(id : Blob) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can delete game objects");
    };

    switch (objectPrefabMap.get(id)) {
      case (null) {
        Runtime.trap("Object prefab not found");
      };
      case (?prefab) {
        if (prefab.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You can only delete your own game objects");
        };
        objectPrefabMap.remove(id);
      };
    };
  };

  public type TierLevel = {
    #common;
    #rare;
    #epic;
    #legendary;
  };

  public type TierDefinition = {
    id : Blob;
    name : Text;
    level : TierLevel;
    rarityPercentage : Float;
    statRanges : StatRanges;
    specialAttributes : [Text];
    owner : Principal;
  };

  public type StatRanges = {
    minAttack : Int;
    maxAttack : Int;
    minDefense : Int;
    maxDefense : Int;
    minElemental : Int;
    maxElemental : Int;
  };

  let tierDefinitions = Map.empty<Blob, TierDefinition>();

  public shared ({ caller }) func saveTierDefinition(id : Blob, tier : TierDefinition) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save tier definitions");
    };

    let tierWithOwner = {
      tier with owner = caller
    };
    tierDefinitions.add(id, tierWithOwner);
  };

  public query ({ caller }) func getTierDefinition(id : Blob) : async ?TierDefinition {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view tier definitions");
    };

    switch (tierDefinitions.get(id)) {
      case (null) { null };
      case (?tier) {
        if (Principal.equal(tier.owner, caller) or AccessControl.isAdmin(accessControlState, caller)) {
          ?tier;
        } else {
          Runtime.trap("Unauthorized: You can only view your own tier definitions");
        };
      };
    };
  };

  public query ({ caller }) func getAllTierDefinitions() : async [TierDefinition] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view tier definitions");
    };

    if (AccessControl.isAdmin(accessControlState, caller)) {
      tierDefinitions.values().toArray();
    } else {
      tierDefinitions.values().filter(
        func(tier : TierDefinition) : Bool {
          Principal.equal(tier.owner, caller);
        }
      ).toArray();
    };
  };

  public shared ({ caller }) func deleteTierDefinition(id : Blob) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can delete tier definitions");
    };

    switch (tierDefinitions.get(id)) {
      case (null) {
        Runtime.trap("Tier definition not found");
      };
      case (?tier) {
        if (tier.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You can only delete your own tier definitions");
        };
        tierDefinitions.remove(id);
      };
    };
  };

  public type LootItem = {
    id : Blob;
    name : Text;
    tier : TierLevel;
    isExcellent : Bool;
    attackPower : Int;
    defensePower : Int;
    elementalPower : Int;
    specialTraits : [Text];
    statBuffs : ?StatBuffs;
    resistances : ?Resistances;
    debuffAbilities : ?[Text];
    worldBreakingPowers : ?WorldBreakingPowers;
    owner : Principal;
  };

  public type StatBuffs = {
    healthBonus : Int;
    manaBonus : Int;
    strengthBonus : Int;
    agilityBonus : Int;
  };

  public type Resistances = {
    physicalResistance : Float;
    fireResistance : Float;
    iceResistance : Float;
    lightningResistance : Float;
    poisonResistance : Float;
  };

  public type WorldBreakingPowers = {
    reducedCooldowns : Bool;
    efficientMana : Bool;
    allySummoning : Bool;
    aoeUltimates : Bool;
  };

  let worldLootTable = Map.empty<Blob, LootItem>();

  public shared ({ caller }) func saveLootItem(id : Blob, item : LootItem) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save loot items");
    };

    let itemWithOwner = {
      item with owner = caller
    };
    worldLootTable.add(id, itemWithOwner);
  };

  public shared ({ caller }) func saveLootItemBatch(items : [(Blob, LootItem)]) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can save loot items");
    };

    for ((id, item) in items.vals()) {
      let itemWithOwner = {
        item with owner = caller
      };
      worldLootTable.add(id, itemWithOwner);
    };
  };

  public query ({ caller }) func getLootItem(id : Blob) : async ?LootItem {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view loot items");
    };

    switch (worldLootTable.get(id)) {
      case (null) { null };
      case (?item) {
        if (Principal.equal(item.owner, caller) or AccessControl.isAdmin(accessControlState, caller)) {
          ?item;
        } else {
          Runtime.trap("Unauthorized: You can only view your own loot items");
        };
      };
    };
  };

  public query ({ caller }) func getLootItemsByTier(tier : TierLevel) : async [LootItem] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view loot items");
    };

    let filtered = worldLootTable.values().filter(
      func(item : LootItem) : Bool {
        let tierMatches = switch (item.tier, tier) {
          case (#common, #common) { true };
          case (#rare, #rare) { true };
          case (#epic, #epic) { true };
          case (#legendary, #legendary) { true };
          case (_, _) { false };
        };
        let ownerMatches = Principal.equal(item.owner, caller) or AccessControl.isAdmin(accessControlState, caller);
        tierMatches and ownerMatches;
      }
    ).toArray();

    filtered;
  };

  public query ({ caller }) func getAllLootItems() : async [LootItem] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view loot items");
    };

    if (AccessControl.isAdmin(accessControlState, caller)) {
      worldLootTable.values().toArray();
    } else {
      worldLootTable.values().filter(
        func(item : LootItem) : Bool {
          Principal.equal(item.owner, caller);
        }
      ).toArray();
    };
  };

  public query ({ caller }) func getLootItemsPaginated(
    offset : Nat,
    limit : Nat,
  ) : async [LootItem] {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view loot items");
    };

    let allItems = if (AccessControl.isAdmin(accessControlState, caller)) {
      worldLootTable.values().toArray();
    } else {
      worldLootTable.values().filter(
        func(item : LootItem) : Bool {
          Principal.equal(item.owner, caller);
        }
      ).toArray();
    };

    let totalItems = allItems.size();
    if (offset >= totalItems) {
      return [];
    };

    let endIndex = Nat.min(offset + limit, totalItems);
    let result = Array.tabulate(
      endIndex - offset,
      func(i : Nat) : LootItem {
        allItems[offset + i];
      },
    );

    result;
  };

  public shared ({ caller }) func deleteLootItem(id : Blob) : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can delete loot items");
    };

    switch (worldLootTable.get(id)) {
      case (null) {
        Runtime.trap("Loot item not found");
      };
      case (?item) {
        if (item.owner != caller and not AccessControl.isAdmin(accessControlState, caller)) {
          Runtime.trap("Unauthorized: You can only delete your own loot items");
        };
        worldLootTable.remove(id);
      };
    };
  };

  public shared ({ caller }) func clearAllLootItems() : async () {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can clear loot items");
    };

    let itemsToRemove = worldLootTable.toArray().filter(
      func((id : Blob, item : LootItem)) : Bool {
        Principal.equal(item.owner, caller) or AccessControl.isAdmin(accessControlState, caller);
      }
    );

    for ((id, item) in itemsToRemove.vals()) {
      worldLootTable.remove(id);
    };
  };

  public query ({ caller }) func getLootTableStats() : async {
    totalItems : Nat;
    commonCount : Nat;
    rareCount : Nat;
    epicCount : Nat;
    legendaryCount : Nat;
    excellentCount : Nat;
  } {
    if (not (AccessControl.hasPermission(accessControlState, caller, #user))) {
      Runtime.trap("Unauthorized: Only authenticated users can view loot table statistics");
    };

    let userItems = if (AccessControl.isAdmin(accessControlState, caller)) {
      worldLootTable.values().toArray();
    } else {
      worldLootTable.values().filter(
        func(item : LootItem) : Bool {
          Principal.equal(item.owner, caller);
        }
      ).toArray();
    };

    var commonCount = 0;
    var rareCount = 0;
    var epicCount = 0;
    var legendaryCount = 0;
    var excellentCount = 0;

    for (item in userItems.vals()) {
      switch (item.tier) {
        case (#common) { commonCount += 1 };
        case (#rare) { rareCount += 1 };
        case (#epic) { epicCount += 1 };
        case (#legendary) { legendaryCount += 1 };
      };
      if (item.isExcellent) {
        excellentCount += 1;
      };
    };

    {
      totalItems = userItems.size();
      commonCount;
      rareCount;
      epicCount;
      legendaryCount;
      excellentCount;
    };
  };
};

