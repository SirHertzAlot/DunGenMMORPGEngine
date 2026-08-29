using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DunGen.ECS.Models
{
    [Flags]
    public enum ModelPartCategory : ulong
    {
        None = 0,
        Body = 1UL << 0,
        Head = 1UL << 1,
        Hair = 1UL << 2,
        Helmet = 1UL << 3,
        Torso = 1UL << 4,
        Legs = 1UL << 5,
        Feet = 1UL << 6,
        Hands = 1UL << 7,
        MainHand = 1UL << 8,
        OffHand = 1UL << 9,
        Back = 1UL << 10,
        Accessory = 1UL << 11
    }

    public static class ModelPartPresets
    {
        public const ulong CoreCharacter =
            (ulong)(ModelPartCategory.Body |
                    ModelPartCategory.Head |
                    ModelPartCategory.Torso |
                    ModelPartCategory.Legs |
                    ModelPartCategory.Feet |
                    ModelPartCategory.Hands);

        public const ulong EquippedHumanoid =
            CoreCharacter |
            (ulong)(ModelPartCategory.MainHand |
                    ModelPartCategory.OffHand |
                    ModelPartCategory.Back |
                    ModelPartCategory.Accessory);
    }

    public static class PolygonFantasyHeroAssetSource
    {
        public const string SourceFolderAbsolutePath =
            @"C:\Users\user\Projects\MMOs\CN-TOR-P.A.v00\TOR-MMO-ENGINE\Assets\Models\POLYGON_Modular_Fantasy_Hero_SourceFiles_v2\Source_Files\FBX";

        public const string PrimaryModularCharactersFbx = "ModularCharactersFixedScale";
        public const string OriginalModularCharactersFbx = "ModularCharacters";
        public const string ModularPartsFolder = "ModularParts_Unreal";
        public const string StaticMeshesFolder = "StaticMeshes";
        public const string WeaponsFolder = "Weapons";
        public const string ManifestResourcePath = "Models/PolygonFantasyHeroManifest";

        public const string HeroVariant = "Hero_Adventurer";
        public const string KnightVariant = "Knight_HeavyArmor";
        public const string BarbarianVariant = "Barbarian_Rugged";
        public const string RangerVariant = "Ranger_Hooded";
        public const string MageVariant = "Mage_Robed";
        public const string RogueVariant = "Rogue_LightArmor";
        public const string BanditVariant = "Bandit_LightArmor";
    }

    /// <summary>Deterministic reference from ECS entity data to a visual model assembled by name.</summary>
    public struct VisualModelComponent : IComponentData
    {
        public FixedString64Bytes ModelName;
        public FixedString64Bytes VariantName;
        public ulong RequiredParts;
        public ulong HiddenParts;
    }

    public readonly struct ModelPartSelection
    {
        public readonly string ModelName;
        public readonly ModelPartCategory RequiredParts;
        public readonly ModelPartCategory HiddenParts;

        public ModelPartSelection(string modelName, ModelPartCategory requiredParts, ModelPartCategory hiddenParts = ModelPartCategory.None)
        {
            ModelName = modelName;
            RequiredParts = requiredParts;
            HiddenParts = hiddenParts;
        }
    }

    public enum CharacterArchetype : byte
    {
        Hero = 0,
        Knight = 1,
        Barbarian = 2,
        Ranger = 3,
        Mage = 4,
        Rogue = 5,
        Bandit = 6
    }

    public readonly struct ModularCharacterRecipe
    {
        public readonly CharacterArchetype Archetype;
        public readonly string VariantName;
        public readonly string[] RequiredObjectNames;
        public readonly string[] HiddenObjectNames;

        public ModularCharacterRecipe(
            CharacterArchetype archetype,
            string variantName,
            string[] requiredObjectNames,
            string[] hiddenObjectNames = null)
        {
            Archetype = archetype;
            VariantName = variantName;
            RequiredObjectNames = requiredObjectNames ?? Array.Empty<string>();
            HiddenObjectNames = hiddenObjectNames ?? Array.Empty<string>();
        }
    }

    [Serializable]
    public sealed class ModelAssetEntry
    {
        public string name;
        public string fileName;
        public string relativePath;
        public string sourceFolder;
        public string category;
        public string gender;
        public int variantIndex;
        public long byteLength;
        public bool isCombinedFbx;
    }

    [Serializable]
    public sealed class ModelAssetManifest
    {
        public int schemaVersion;
        public string sourcePack;
        public string generatedUtc;
        public string sourceFolderAbsolutePath;
        public string primaryModularCharactersFbx;
        public string originalModularCharactersFbx;
        public int assetCount;
        public ModelAssetEntry[] assets = Array.Empty<ModelAssetEntry>();

        public bool ContainsAsset(string assetName)
        {
            return TryGetAsset(assetName, out _);
        }

        public bool TryGetAsset(string assetName, out ModelAssetEntry asset)
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(assetName) || assets == null)
                return false;

            string normalizedName = Normalize(assetName);
            for (int i = 0; i < assets.Length; i++)
            {
                var candidate = assets[i];
                if (candidate != null && Normalize(candidate.name) == normalizedName)
                {
                    asset = candidate;
                    return true;
                }
            }

            return false;
        }

        public List<string> GetMissingRecipeParts(ModularCharacterRecipe recipe)
        {
            var missing = new List<string>();
            for (int i = 0; i < recipe.RequiredObjectNames.Length; i++)
            {
                string objectName = recipe.RequiredObjectNames[i];
                if (!ContainsAsset(objectName))
                    missing.Add(objectName);
            }

            return missing;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }
    }

    public static class PolygonFantasyHeroManifest
    {
        public static ModelAssetManifest LoadFromResources()
        {
            var manifestAsset = Resources.Load<TextAsset>(PolygonFantasyHeroAssetSource.ManifestResourcePath);
            return manifestAsset == null ? null : FromJson(manifestAsset.text);
        }

        public static ModelAssetManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var manifest = JsonUtility.FromJson<ModelAssetManifest>(json);
            if (manifest.assets == null)
                manifest.assets = Array.Empty<ModelAssetEntry>();

            return manifest;
        }
    }

    public static class VisualModelCatalog
    {
        public static VisualModelComponent CreateHero()
        {
            return Create(CharacterArchetype.Hero);
        }

        public static VisualModelComponent CreateKnight()
        {
            return Create(CharacterArchetype.Knight);
        }

        public static VisualModelComponent CreateBarbarian()
        {
            return Create(CharacterArchetype.Barbarian);
        }

        public static VisualModelComponent CreateRanger()
        {
            return Create(CharacterArchetype.Ranger);
        }

        public static VisualModelComponent CreateMage()
        {
            return Create(CharacterArchetype.Mage);
        }

        public static VisualModelComponent CreateRogue()
        {
            return Create(CharacterArchetype.Rogue);
        }

        public static VisualModelComponent CreateBandit()
        {
            return Create(CharacterArchetype.Bandit);
        }

        public static VisualModelComponent Create(CharacterArchetype archetype)
        {
            var recipe = GetRecipe(archetype);
            var hiddenParts = archetype == CharacterArchetype.Hero ||
                              archetype == CharacterArchetype.Barbarian ||
                              archetype == CharacterArchetype.Ranger ||
                              archetype == CharacterArchetype.Rogue
                ? (ulong)ModelPartCategory.Helmet
                : 0;

            return new VisualModelComponent
            {
                ModelName = PolygonFantasyHeroAssetSource.PrimaryModularCharactersFbx,
                VariantName = recipe.VariantName,
                RequiredParts = ModelPartPresets.EquippedHumanoid,
                HiddenParts = hiddenParts
            };
        }

        public static ModularCharacterRecipe GetRecipe(CharacterArchetype archetype)
        {
            switch (archetype)
            {
                case CharacterArchetype.Knight:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Knight,
                        PolygonFantasyHeroAssetSource.KnightVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_00",
                            "SK_Chr_Torso_Male_18",
                            "SK_Chr_Hips_Male_18",
                            "SK_Chr_ArmUpperLeft_Male_18",
                            "SK_Chr_ArmUpperRight_Male_18",
                            "SK_Chr_ArmLowerLeft_Male_18",
                            "SK_Chr_ArmLowerRight_Male_18",
                            "SK_Chr_HandLeft_Male_17",
                            "SK_Chr_HandRight_Male_17",
                            "SK_Chr_LegLeft_Male_18",
                            "SK_Chr_LegRight_Male_18",
                            "SK_Chr_HeadCoverings_No_Hair_03",
                            "SK_Wep_Sword_01",
                            "SK_Wep_Shield_01"
                        });
                case CharacterArchetype.Barbarian:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Barbarian,
                        PolygonFantasyHeroAssetSource.BarbarianVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_04",
                            "SK_Chr_Hair_13",
                            "SK_Chr_FacialHair_Male_08",
                            "SK_Chr_Torso_Male_04",
                            "SK_Chr_Hips_Male_04",
                            "SK_Chr_ArmUpperLeft_Male_04",
                            "SK_Chr_ArmUpperRight_Male_04",
                            "SK_Chr_ArmLowerLeft_Male_04",
                            "SK_Chr_ArmLowerRight_Male_04",
                            "SK_Chr_HandLeft_Male_04",
                            "SK_Chr_HandRight_Male_04",
                            "SK_Chr_LegLeft_Male_04",
                            "SK_Chr_LegRight_Male_04",
                            "SK_Wep_Axe_01"
                        });
                case CharacterArchetype.Ranger:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Ranger,
                        PolygonFantasyHeroAssetSource.RangerVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_02",
                            "SK_Chr_HeadCoverings_No_Hair_01",
                            "SK_Chr_Torso_Male_08",
                            "SK_Chr_Hips_Male_08",
                            "SK_Chr_ArmUpperLeft_Male_08",
                            "SK_Chr_ArmUpperRight_Male_08",
                            "SK_Chr_ArmLowerLeft_Male_08",
                            "SK_Chr_ArmLowerRight_Male_08",
                            "SK_Chr_HandLeft_Male_08",
                            "SK_Chr_HandRight_Male_08",
                            "SK_Chr_LegLeft_Male_08",
                            "SK_Chr_LegRight_Male_08",
                            "SK_Chr_BackAttachment_02",
                            "SK_Wep_Dagger_01"
                        });
                case CharacterArchetype.Mage:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Mage,
                        PolygonFantasyHeroAssetSource.MageVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_01",
                            "SK_Chr_Hair_05",
                            "SK_Chr_Torso_Male_12",
                            "SK_Chr_Hips_Male_12",
                            "SK_Chr_ArmUpperLeft_Male_12",
                            "SK_Chr_ArmUpperRight_Male_12",
                            "SK_Chr_ArmLowerLeft_Male_12",
                            "SK_Chr_ArmLowerRight_Male_12",
                            "SK_Chr_HandLeft_Male_12",
                            "SK_Chr_HandRight_Male_12",
                            "SK_Chr_LegLeft_Male_12",
                            "SK_Chr_LegRight_Male_12",
                            "SK_Wep_Staff_01"
                        });
                case CharacterArchetype.Rogue:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Rogue,
                        PolygonFantasyHeroAssetSource.RogueVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_03",
                            "SK_Chr_HeadCoverings_No_Hair_02",
                            "SK_Chr_Torso_Male_06",
                            "SK_Chr_Hips_Male_06",
                            "SK_Chr_ArmUpperLeft_Male_06",
                            "SK_Chr_ArmUpperRight_Male_06",
                            "SK_Chr_ArmLowerLeft_Male_06",
                            "SK_Chr_ArmLowerRight_Male_06",
                            "SK_Chr_HandLeft_Male_06",
                            "SK_Chr_HandRight_Male_06",
                            "SK_Chr_LegLeft_Male_06",
                            "SK_Chr_LegRight_Male_06",
                            "SK_Wep_Dagger_01"
                        });
                case CharacterArchetype.Bandit:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Bandit,
                        PolygonFantasyHeroAssetSource.BanditVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_05",
                            "SK_Chr_Torso_Male_09",
                            "SK_Chr_Hips_Male_09",
                            "SK_Chr_ArmUpperLeft_Male_09",
                            "SK_Chr_ArmUpperRight_Male_09",
                            "SK_Chr_ArmLowerLeft_Male_09",
                            "SK_Chr_ArmLowerRight_Male_09",
                            "SK_Chr_HandLeft_Male_09",
                            "SK_Chr_HandRight_Male_09",
                            "SK_Chr_LegLeft_Male_09",
                            "SK_Chr_LegRight_Male_09",
                            "SK_Wep_Mace_01",
                            "SK_Wep_Shield_Buckler_01"
                        });
                default:
                    return new ModularCharacterRecipe(
                        CharacterArchetype.Hero,
                        PolygonFantasyHeroAssetSource.HeroVariant,
                        new[]
                        {
                            "SK_Chr_Head_Male_00",
                            "SK_Chr_Hair_01",
                            "SK_Chr_Torso_Male_00",
                            "SK_Chr_Hips_Male_00",
                            "SK_Chr_ArmUpperLeft_Male_00",
                            "SK_Chr_ArmUpperRight_Male_00",
                            "SK_Chr_ArmLowerLeft_Male_00",
                            "SK_Chr_ArmLowerRight_Male_00",
                            "SK_Chr_HandLeft_Male_00",
                            "SK_Chr_HandRight_Male_00",
                            "SK_Chr_LegLeft_Male_00",
                            "SK_Chr_LegRight_Male_00",
                            "SK_Wep_Sword_01",
                            "SK_Wep_Shield_02"
                        });
            }
        }
    }

    /// <summary>
    /// Selects mesh-bearing children from a larger modular FBX using stable model and part names.
    /// Expected names can be pack-native, for example SK_Chr_Torso_Male_04 or SK_Wep_Sword_01.
    /// </summary>
    public static class ModularModelPartResolver
    {
        private static readonly (ModelPartCategory Category, string[] Tokens)[] PartTokens =
        {
            (ModelPartCategory.Body, new[] { "body", "base", "skin", "chrbody" }),
            (ModelPartCategory.Head, new[] { "head", "face", "chrhead", "headnoelements" }),
            (ModelPartCategory.Hair, new[] { "hair", "beard", "brow" }),
            (ModelPartCategory.Helmet, new[] { "helmet", "helm", "hat", "hood", "headcoverings" }),
            (ModelPartCategory.Torso, new[] { "torso", "chest", "shirt", "armor", "robe", "chrtorso" }),
            (ModelPartCategory.Legs, new[] { "legs", "legleft", "legright", "hips", "pants", "greaves" }),
            (ModelPartCategory.Feet, new[] { "feet", "boot", "shoe" }),
            (ModelPartCategory.Hands, new[] { "hands", "handleft", "handright", "armlower", "armupper", "gloves", "gauntlets" }),
            (ModelPartCategory.MainHand, new[] { "mainhand", "main_hand", "weapon", "skwep", "sword", "axe", "staff", "mace", "dagger", "joust", "bow", "throwingknife", "thowingknife" }),
            (ModelPartCategory.OffHand, new[] { "offhand", "off_hand", "shield", "buckler" }),
            (ModelPartCategory.Back, new[] { "back", "backattachment", "cape", "cloak", "pack" }),
            (ModelPartCategory.Accessory, new[] { "accessory", "attachment", "helmetattachment", "hipsattachment", "shoulderattach", "elbowattach", "kneeattach", "ear", "amulet", "ring", "belt", "pouch" })
        };

        public static List<Transform> SelectParts(Transform root, VisualModelComponent visualModel)
        {
            return SelectParts(
                root,
                visualModel.ModelName.ToString(),
                (ModelPartCategory)visualModel.RequiredParts,
                (ModelPartCategory)visualModel.HiddenParts);
        }

        public static List<Transform> SelectParts(Transform root, ModelPartSelection selection)
        {
            return SelectParts(root, selection.ModelName, selection.RequiredParts, selection.HiddenParts);
        }

        public static List<Transform> SelectParts(Transform root, ModularCharacterRecipe recipe)
        {
            var selected = new List<Transform>();
            if (root == null)
                return selected;

            for (int i = 0; i < recipe.RequiredObjectNames.Length; i++)
            {
                var match = FindByNormalizedName(root, recipe.RequiredObjectNames[i]);
                if (match != null && !ContainsName(recipe.HiddenObjectNames, match.name))
                    selected.Add(match);
            }

            return selected;
        }

        public static List<Transform> SelectParts(
            Transform root,
            string modelName,
            ModelPartCategory requiredParts,
            ModelPartCategory hiddenParts = ModelPartCategory.None)
        {
            var selected = new List<Transform>();
            if (root == null || string.IsNullOrWhiteSpace(modelName))
                return selected;

            bool rootMatchesModel = NameMatches(root.name, modelName);
            CollectMatchingParts(root, modelName, rootMatchesModel, requiredParts, hiddenParts, selected);
            return selected;
        }

        public static bool TryInferPartCategory(string objectName, out ModelPartCategory category)
        {
            category = ModelPartCategory.None;
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            string normalizedName = Normalize(objectName);
            for (int i = 0; i < PartTokens.Length; i++)
            {
                var (partCategory, tokens) = PartTokens[i];
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    if (normalizedName.Contains(Normalize(tokens[tokenIndex])))
                    {
                        category |= partCategory;
                        break;
                    }
                }
            }

            return category != ModelPartCategory.None;
        }

        private static void CollectMatchingParts(
            Transform current,
            string modelName,
            bool rootMatchesModel,
            ModelPartCategory requiredParts,
            ModelPartCategory hiddenParts,
            List<Transform> selected)
        {
            bool matchesModel = rootMatchesModel || NameMatches(current.name, modelName);

            if (matchesModel && TryInferPartCategory(current.name, out var category))
            {
                bool isRequired = requiredParts == ModelPartCategory.None || (requiredParts & category) != 0;
                bool isHidden = (hiddenParts & category) != 0;
                if (isRequired && !isHidden && HasRenderablePart(current))
                    selected.Add(current);
            }

            for (int i = 0; i < current.childCount; i++)
                CollectMatchingParts(current.GetChild(i), modelName, matchesModel, requiredParts, hiddenParts, selected);
        }

        private static bool HasRenderablePart(Transform transform)
        {
            return transform.GetComponent<Renderer>() != null || transform.GetComponent<MeshFilter>() != null;
        }

        private static Transform FindByNormalizedName(Transform current, string objectName)
        {
            if (NameEquals(current.name, objectName) && HasRenderablePart(current))
                return current;

            for (int i = 0; i < current.childCount; i++)
            {
                var match = FindByNormalizedName(current.GetChild(i), objectName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static bool ContainsName(string[] objectNames, string objectName)
        {
            for (int i = 0; i < objectNames.Length; i++)
            {
                if (NameEquals(objectNames[i], objectName))
                    return true;
            }

            return false;
        }

        private static bool NameMatches(string objectName, string modelName)
        {
            return Normalize(objectName).Contains(Normalize(modelName));
        }

        private static bool NameEquals(string left, string right)
        {
            return Normalize(left) == Normalize(right);
        }

        private static string Normalize(string value)
        {
            return value.Replace("_", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace(" ", "")
                .ToLowerInvariant();
        }
    }
}
