#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Authoritative.Multiplayer;

namespace Authoritative.Domain
{
    public sealed class CharacterGenerationRequest
    {
        public int Level { get; set; } = 1;
        public string? Class { get; set; }
        public string? Race { get; set; }
        public int? Seed { get; set; }
        public int Count { get; set; } = 1;
    }

    public sealed class CharacterStats
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public int Constitution { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }
    }

    public sealed class CharacterEquipmentSlot
    {
        public string ItemId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CharacterEquipment
    {
        public CharacterEquipmentSlot? MainHand { get; set; }
        public CharacterEquipmentSlot? OffHand { get; set; }
        public CharacterEquipmentSlot? Armor { get; set; }
        public CharacterEquipmentSlot? Accessory { get; set; }
    }

    public sealed class GeneratedCharacter
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public string Race { get; set; } = string.Empty;
        public int Level { get; set; }
        public CharacterStats Stats { get; set; } = new();
        public int HitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int ArmorClass { get; set; }
        public int Speed { get; set; }
        public List<string> Skills { get; set; } = new();
        public List<string> Abilities { get; set; } = new();
        public CharacterEquipment Equipment { get; set; } = new();
        public int Gold { get; set; }
        public string Background { get; set; } = string.Empty;
        public string Alignment { get; set; } = string.Empty;
        public int Seed { get; set; }
        public List<CharacterAssetPart> AssetParts { get; set; } = new();
    }

    public sealed class CharacterAssetPart
    {
        public string PartId { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string? AttachBone { get; set; }
        public string? Gender { get; set; }
        public int? Variant { get; set; }
        public int? Priority { get; set; }
        public bool IsSample { get; set; }
    }

    public sealed class CharacterGenerator
    {
        private const int DefaultBaseSeed = 4217;

        private static readonly string[] Classes = { "Warrior", "Mage", "Rogue", "Priest", "Ranger", "Paladin", "Warlock", "Druid" };
        private static readonly string[] Races = { "Human", "Elf", "Dwarf", "Orc", "Halfling", "Gnome", "Tiefling", "Dragonborn" };
        private static readonly string[] Backgrounds = { "Soldier", "Scholar", "Outlander", "Criminal", "Noble", "Acolyte", "Guild Artisan", "Hermit" };
        private static readonly string[] Alignments = { "Lawful Good", "Neutral Good", "Chaotic Good", "Lawful Neutral", "True Neutral", "Chaotic Neutral", "Lawful Evil", "Neutral Evil", "Chaotic Evil" };

        private static readonly string[] FirstNames = {
            "Arath", "Lyra", "Dorn", "Sera", "Kael", "Mira", "Thane", "Zara",
            "Aldric", "Nessa", "Bran", "Calla", "Edric", "Fiora", "Gareth", "Halla",
            "Ivar", "Jessa", "Koras", "Lena", "Mael", "Nira", "Osric", "Pella",
            "Quill", "Rhea", "Sorn", "Tara", "Ulric", "Vael", "Wren", "Xara",
            "Yorn", "Zela", "Arion", "Brynn", "Caius", "Delia", "Eron", "Fey"
        };

        private static readonly string[] LastNames = {
            "Thornwood", "Ironforge", "Shadowmere", "Brightblade", "Coldwind",
            "Stoneheart", "Ravensong", "Darkwater", "Goldenleaf", "Silvermark",
            "Ashburn", "Blackthorn", "Crystalveil", "Dawnfire", "Embercrest",
            "Frostfall", "Grimwood", "Hollowbrook", "Ironside", "Jadespire"
        };

        private static readonly Dictionary<string, string[]> ClassSkills = new(StringComparer.Ordinal)
        {
            ["Warrior"]  = new[] { "Heavy Armor", "Two-Handed Weapons", "Battle Cry", "Shield Block", "Weapon Mastery", "Combat Surge", "War Stomp", "Berserker Rage" },
            ["Mage"]     = new[] { "Arcane Blast", "Mana Shield", "Fireball", "Ice Lance", "Teleport", "Arcane Intellect", "Polymorph", "Counterspell" },
            ["Rogue"]    = new[] { "Backstab", "Shadowstep", "Evasion", "Poison Blade", "Pick Lock", "Smoke Bomb", "Ambush", "Deadly Momentum" },
            ["Priest"]   = new[] { "Holy Light", "Resurrection", "Smite", "Power Word Shield", "Prayer of Healing", "Divine Hymn", "Mind Control", "Dispel Magic" },
            ["Ranger"]   = new[] { "Aimed Shot", "Multi-Shot", "Track", "Hunter's Mark", "Rapid Fire", "Trap Setting", "Eagle Eye", "Camouflage" },
            ["Paladin"]  = new[] { "Holy Strike", "Lay on Hands", "Consecration", "Blessing of Protection", "Avenging Wrath", "Divine Shield", "Hammer of Justice", "Beacon of Light" },
            ["Warlock"]  = new[] { "Shadow Bolt", "Summon Demon", "Curse of Agony", "Drain Life", "Hellfire", "Soul Harvest", "Dark Pact", "Demonic Empowerment" },
            ["Druid"]    = new[] { "Shapeshift", "Regrowth", "Moonfire", "Entangling Roots", "Barkskin", "Wild Growth", "Force of Nature", "Innervate" }
        };

        private static readonly Dictionary<string, string> ClassWeaponType = new(StringComparer.Ordinal)
        {
            ["Warrior"] = "sword", ["Mage"] = "staff", ["Rogue"] = "dagger",
            ["Priest"] = "staff", ["Ranger"] = "bow", ["Paladin"] = "sword",
            ["Warlock"] = "staff", ["Druid"] = "staff"
        };

        private static readonly string[] Tiers = { "common", "uncommon", "rare", "epic", "legendary" };

        public List<GeneratedCharacter> Generate(CharacterGenerationRequest request)
        {
            int count   = Math.Clamp(request.Count, 1, 20);
            int level   = Math.Clamp(request.Level, 1, 60);
            int baseSeed = request.Seed ?? DefaultBaseSeed;
            var result  = new List<GeneratedCharacter>(count);
            for (int i = 0; i < count; i++)
            {
                var rng = new DeterministicRng((ulong)(uint)(baseSeed + i * 7919));
                result.Add(GenerateOne(rng, level, request.Class, request.Race, baseSeed + i * 7919));
            }
            return result;
        }

        private GeneratedCharacter GenerateOne(DeterministicRng rng, int level, string? classOverride, string? raceOverride, int seed)
        {
            var cls  = string.IsNullOrWhiteSpace(classOverride) ? Classes[rng.Next(Classes.Length)] : classOverride.Trim();
            var race = string.IsNullOrWhiteSpace(raceOverride)  ? Races[rng.Next(Races.Length)]   : raceOverride.Trim();
            var name = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}";

            var stats    = RollStats(rng, cls, race, level);
            int hp       = CalcHp(rng, cls, stats.Constitution, level);
            int ac       = CalcAc(cls, stats, level);
            var skills   = PickSkills(rng, cls, level);
            var abilities = PickAbilities(cls, level);
            var equip    = GenEquipment(rng, cls, level, seed);
            int gold     = rng.Next(level * 10, level * 100 + 50);

            var generated = new GeneratedCharacter
            {
                CharacterId = $"char_{seed}",
                Name        = name,
                Class       = cls,
                Race        = race,
                Level       = level,
                Stats       = stats,
                HitPoints   = hp,
                MaxHitPoints = hp,
                ArmorClass  = ac,
                Speed       = race is "Halfling" or "Gnome" ? 25 : 30,
                Skills      = skills,
                Abilities   = abilities,
                Equipment   = equip,
                Gold        = gold,
                Background  = Backgrounds[rng.Next(Backgrounds.Length)],
                Alignment   = Alignments[rng.Next(Alignments.Length)],
                Seed        = seed
            };

            // Select asset parts from catalog (best-effort). For now pick one file randomly (non-deterministic).
            try
            {
                foreach (var kv in CharacterPartsCatalog.Parts)
                {
                    var part = kv.Value;
                    if (part.Files == null || part.Files.Count == 0) continue;
                    var idx = rng.NextInt(part.Files.Count);
                    generated.AssetParts.Add(new CharacterAssetPart
                    {
                        PartId = part.Id,
                        AssetPath = part.Files[idx],
                        AttachBone = part.AttachBone,
                        Gender = part.Gender,
                        Variant = part.Variant,
                        Priority = part.Priority,
                        IsSample = false
                    });
                }
            }
            catch
            {
                // ignore catalog errors
            }

            return generated;
        }

        private static CharacterStats RollStats(DeterministicRng rng, string cls, string race, int level)
        {
            int Roll()
            {
                var d = new[] { rng.Next(1, 7), rng.Next(1, 7), rng.Next(1, 7), rng.Next(1, 7) };
                Array.Sort(d);
                return d[1] + d[2] + d[3];
            }

            var s = new CharacterStats
            {
                Strength     = Roll(),
                Dexterity    = Roll(),
                Intelligence = Roll(),
                Constitution = Roll(),
                Wisdom       = Roll(),
                Charisma     = Roll()
            };

            int b = Math.Max(0, (level - 1) / 4);
            switch (cls)
            {
                case "Warrior":  s.Strength     += b + 2; s.Constitution  += b;     break;
                case "Mage":     s.Intelligence  += b + 3; s.Wisdom        += b;     break;
                case "Rogue":    s.Dexterity     += b + 3; s.Charisma      += b;     break;
                case "Priest":   s.Wisdom        += b + 3; s.Charisma      += b;     break;
                case "Ranger":   s.Dexterity     += b + 2; s.Wisdom        += b + 1; break;
                case "Paladin":  s.Strength      += b + 1; s.Charisma      += b + 2; break;
                case "Warlock":  s.Charisma      += b + 3; s.Intelligence   += b;    break;
                case "Druid":    s.Wisdom        += b + 3; s.Constitution   += b;    break;
            }

            switch (race)
            {
                case "Elf":        s.Dexterity    += 2; s.Intelligence += 1; break;
                case "Dwarf":      s.Constitution += 2; s.Strength     += 1; break;
                case "Orc":        s.Strength     += 3; s.Constitution += 1; break;
                case "Halfling":   s.Dexterity    += 2; s.Charisma     += 1; break;
                case "Gnome":      s.Intelligence += 2; s.Dexterity    += 1; break;
                case "Tiefling":   s.Charisma     += 2; s.Intelligence += 1; break;
                case "Dragonborn": s.Strength     += 2; s.Charisma     += 1; break;
                default:           s.Strength     += 1; s.Constitution += 1; break;
            }
            return s;
        }

        private static int CalcHp(DeterministicRng rng, string cls, int con, int level)
        {
            int conMod = (con - 10) / 2;
            int die = cls is "Warrior" or "Paladin" ? 10 : cls is "Mage" ? 6 : 8;
            int hp = die + conMod;
            for (int i = 2; i <= level; i++) hp += rng.Next(1, die + 1) + conMod;
            return Math.Max(1, hp);
        }

        private static int CalcAc(string cls, CharacterStats s, int level)
        {
            int dex = (s.Dexterity - 10) / 2;
            int lb  = level / 5;
            return cls switch
            {
                "Warrior" or "Paladin" => 16 + lb,
                "Rogue"   or "Ranger"  => 13 + dex + lb,
                "Mage"    or "Warlock" => 12 + dex + lb,
                _                      => 13 + lb
            };
        }

        private static List<string> PickSkills(DeterministicRng rng, string cls, int level)
        {
            if (!ClassSkills.TryGetValue(cls, out var pool)) pool = new[] { "Attack", "Defend" };
            int take = Math.Min(pool.Length, 2 + level / 5);
            return pool.OrderBy(_ => rng.Next()).Take(take).ToList();
        }

        private static List<string> PickAbilities(string cls, int level)
        {
            var result = new List<string>();
            if (level >= 5)  result.Add($"{cls} Mastery I");
            if (level >= 10) result.Add($"{cls} Mastery II");
            if (level >= 10) result.Add("Extra Attack");
            if (level >= 20) result.Add($"{cls} Grandmaster");
            if (level >= 15) result.Add("Epic Resilience");
            return result;
        }

        private static CharacterEquipment GenEquipment(DeterministicRng rng, string cls, int level, int seed)
        {
            int tierIdx = Math.Clamp(level / 12 + rng.Next(-1, 2), 0, Tiers.Length - 1);
            string tier = Tiers[tierIdx];
            ClassWeaponType.TryGetValue(cls, out var wType);
            wType ??= "sword";

            var eq = new CharacterEquipment
            {
                MainHand = new CharacterEquipmentSlot { ItemId = $"item_w_{seed}", Type = wType, Tier = tier, Name = $"{Cap(tier)} {Cap(wType)}" },
                Armor    = new CharacterEquipmentSlot
                {
                    ItemId = $"item_a_{seed}", Type = "armor", Tier = tier,
                    Name = $"{Cap(tier)} {(cls is "Warrior" or "Paladin" ? "Plate Armor" : cls == "Rogue" ? "Leather Armor" : "Robe")}"
                }
            };

            if (rng.NextDouble() > 0.5 && cls is not "Mage" and not "Warlock" and not "Ranger")
                eq.OffHand = new CharacterEquipmentSlot { ItemId = $"item_s_{seed}", Type = "shield", Tier = tier, Name = $"{Cap(tier)} Shield" };

            if (rng.NextDouble() > 0.4)
            {
                var accs = new[] { "Amulet", "Ring", "Belt", "Cloak", "Boots" };
                eq.Accessory = new CharacterEquipmentSlot
                {
                    ItemId = $"item_ac_{seed}", Type = "accessory", Tier = tier,
                    Name = $"{Cap(tier)} {accs[rng.Next(accs.Length)]}"
                };
            }
            return eq;
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
#endif
