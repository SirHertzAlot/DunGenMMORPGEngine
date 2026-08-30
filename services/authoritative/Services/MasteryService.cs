#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services
{
    public enum MasteryEffectKind
    {
        Buff,
        EnemyDebuff,
        Attack,
        SpecialAction
    }

    public sealed class MasterySkillOption
    {
        public string SkillId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string MasteryTier { get; set; } = string.Empty;
        public MasteryEffectKind EffectKind { get; set; }
        public int Power { get; set; }
    }

    public sealed class MasteryOffer
    {
        public string OfferId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string MasteryTier { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public IReadOnlyList<MasterySkillOption> Options { get; set; } = Array.Empty<MasterySkillOption>();
    }

    public sealed class MasterySelectionResult
    {
        public string OfferId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string MasteryTier { get; set; } = string.Empty;
        public MasterySkillOption SelectedOption { get; set; } = new();
        public IReadOnlyList<MasterySkillOption> UnlockedForItemType { get; set; } = Array.Empty<MasterySkillOption>();
    }

    public sealed class MasteryProgressSnapshot
    {
        public string UserId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int UnlockedCount { get; set; }
        public IReadOnlyList<MasterySkillOption> UnlockedOptions { get; set; } = Array.Empty<MasterySkillOption>();
    }

    public interface IMasteryService
    {
        Task<MasteryOffer> GenerateOfferAsync(string userId, string itemType, string masteryTier, CancellationToken ct);
        Task<MasterySelectionResult> SelectOptionAsync(string userId, string offerId, string skillId, CancellationToken ct);
        Task<MasteryProgressSnapshot> GetProgressAsync(string userId, string itemType, CancellationToken ct);
    }

    public sealed class MasteryService : IMasteryService
    {
        private static readonly string[] DefaultItemTypes = PersistenceTagCatalog.LootItemTypes
            .Concat(PersistenceTagCatalog.MasteryExtraItemTypes)
            .ToArray();

        private static readonly Dictionary<string, double> TierMultipliers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["apprentice"] = 1.00,
            ["journeyman"] = 1.15,
            ["craftsman"] = 1.35,
            ["master"] = 1.60,
            ["grandmaster"] = 1.90,
            ["legendary"] = 2.30,
            ["god"] = 2.80,
        };

        private readonly IMasteryPersistenceService _store;
        private readonly ILogger<MasteryService> _log;
        private readonly Dictionary<string, List<MasterySkillTemplate>> _templatesByKey;

        public MasteryService(
            IMasteryPersistenceService store,
            IConfiguration configuration,
            ILogger<MasteryService> log)
        {
            _store = store;
            _log = log;
            _templatesByKey = LoadTemplates(configuration, log);
        }

        public async Task<MasteryOffer> GenerateOfferAsync(string userId, string itemType, string masteryTier, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            itemType = NormalizeItemType(itemType);
            masteryTier = NormalizeTier(masteryTier);

            var pool = BuildPool(itemType, masteryTier);

            // Guarantee variety: present 3 options from 3 distinct families.
            var byKind = pool
                .GroupBy(x => x.EffectKind)
                .ToDictionary(g => g.Key, g => g.ToList());

            var kinds = byKind.Keys.ToList();
            ShuffleInPlace(kinds);

            var picked = new List<MasterySkillOption>(3);
            foreach (var kind in kinds.Take(3))
            {
                var options = byKind[kind];
                picked.Add(options[RandomNumberGenerator.GetInt32(options.Count)]);
            }

            var offer = new MasteryOffer
            {
                OfferId = $"ms-offer-{Guid.NewGuid():N}",
                UserId = userId,
                ItemType = itemType,
                MasteryTier = masteryTier,
                CreatedAtUtc = DateTime.UtcNow,
                Options = picked
            };

            await _store.UpsertOfferAsync(offer, ct).ConfigureAwait(false);
            return offer;
        }

        public async Task<MasterySelectionResult> SelectOptionAsync(string userId, string offerId, string skillId, CancellationToken ct)
        {
            var offer = await _store.GetOfferAsync(offerId, ct).ConfigureAwait(false);
            if (offer == null)
                throw new InvalidOperationException($"Offer '{offerId}' not found.");

            if (!string.Equals(offer.UserId, userId, StringComparison.Ordinal))
                throw new InvalidOperationException("Offer does not belong to this user.");

            var selected = offer.Options.FirstOrDefault(x => string.Equals(x.SkillId, skillId, StringComparison.Ordinal));
            if (selected == null)
                throw new InvalidOperationException($"Skill '{skillId}' was not part of offer '{offerId}'.");

            await _store.AddUnlockedSkillAsync(userId, offer.ItemType, selected, ct).ConfigureAwait(false);
            await _store.DeleteOfferAsync(offerId, ct).ConfigureAwait(false);
            var unlocked = await _store.GetUnlockedSkillsAsync(userId, offer.ItemType, ct).ConfigureAwait(false);

            return new MasterySelectionResult
            {
                OfferId = offer.OfferId,
                UserId = userId,
                ItemType = offer.ItemType,
                MasteryTier = offer.MasteryTier,
                SelectedOption = selected,
                UnlockedForItemType = unlocked
            };
        }

        public async Task<MasteryProgressSnapshot> GetProgressAsync(string userId, string itemType, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            itemType = NormalizeItemType(itemType);
            var unlocked = await _store.GetUnlockedSkillsAsync(userId, itemType, ct).ConfigureAwait(false);

            return new MasteryProgressSnapshot
            {
                UserId = userId,
                ItemType = itemType,
                UnlockedCount = unlocked.Count,
                UnlockedOptions = unlocked
            };
        }

        private static string NormalizeItemType(string itemType)
        {
            itemType = (itemType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(itemType))
                throw new ArgumentException("itemType is required", nameof(itemType));

            if (DefaultItemTypes.Contains(itemType))
                return itemType;

            return "sword";
        }

        private static string NormalizeTier(string masteryTier)
        {
            masteryTier = (masteryTier ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(masteryTier))
                throw new ArgumentException("masteryTier is required", nameof(masteryTier));

            return TierMultipliers.ContainsKey(masteryTier) ? masteryTier : "apprentice";
        }

        private static int Scale(string masteryTier, int baseValue)
        {
            var multiplier = TierMultipliers.TryGetValue(masteryTier, out var m) ? m : 1.0;
            return Math.Max(1, (int)Math.Ceiling(baseValue * multiplier));
        }

        private static List<MasterySkillOption> BuildFallbackPool(string itemType, string masteryTier)
        {
            return new List<MasterySkillOption>
            {
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.buff.fortify",
                    Name = "Fortify Stance",
                    Description = $"Gain +{Scale(masteryTier, 8)}% defense for 3 turns.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.Buff,
                    Power = Scale(masteryTier, 8)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.buff.focus",
                    Name = "Precision Focus",
                    Description = $"Gain +{Scale(masteryTier, 10)}% hit chance for 2 turns.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.Buff,
                    Power = Scale(masteryTier, 10)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.debuff.sunder",
                    Name = "Armor Sunder",
                    Description = $"Reduce enemy armor by {Scale(masteryTier, 6)} for 2 turns.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.EnemyDebuff,
                    Power = Scale(masteryTier, 6)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.debuff.weaken",
                    Name = "Weaken Resolve",
                    Description = $"Reduce enemy damage by {Scale(masteryTier, 7)}% for 2 turns.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.EnemyDebuff,
                    Power = Scale(masteryTier, 7)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.attack.burst",
                    Name = "Burst Strike",
                    Description = $"Deal +{Scale(masteryTier, 18)}% bonus damage.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.Attack,
                    Power = Scale(masteryTier, 18)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.attack.cleave",
                    Name = "Cleave Arc",
                    Description = $"Deal {Scale(masteryTier, 12)}% splash damage to adjacent enemies.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.Attack,
                    Power = Scale(masteryTier, 12)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.special.phase-step",
                    Name = "Phase Step",
                    Description = "Reposition up to 2 tiles and ignore one opportunity attack.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.SpecialAction,
                    Power = Scale(masteryTier, 1)
                },
                new()
                {
                    SkillId = $"{itemType}.{masteryTier}.special.echo-guard",
                    Name = "Echo Guard",
                    Description = $"The next hit against you is reduced by {Scale(masteryTier, 22)}%.",
                    ItemType = itemType,
                    MasteryTier = masteryTier,
                    EffectKind = MasteryEffectKind.SpecialAction,
                    Power = Scale(masteryTier, 22)
                }
            };
        }

        private Dictionary<string, List<MasterySkillTemplate>> LoadTemplates(IConfiguration configuration, ILogger<MasteryService> log)
        {
            var path = configuration["MASTERY_SKILL_POOL_PATH"];
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(AppContext.BaseDirectory, "config", "mastery-skill-pools.json");

            try
            {
                if (!File.Exists(path))
                {
                    log.LogWarning("Mastery skill pool config not found at {Path}. Falling back to built-in templates.", path);
                    return new Dictionary<string, List<MasterySkillTemplate>>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<MasterySkillPoolConfig>(json);
                if (config?.Skills == null || config.Skills.Count == 0)
                    return new Dictionary<string, List<MasterySkillTemplate>>(StringComparer.OrdinalIgnoreCase);

                var map = new Dictionary<string, List<MasterySkillTemplate>>(StringComparer.OrdinalIgnoreCase);
                foreach (var skill in config.Skills)
                {
                    var item = NormalizeItemType(skill.ItemType);
                    var tier = NormalizeTier(skill.MasteryTier);
                    var key = BuildTemplateKey(item, tier);
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<MasterySkillTemplate>();
                        map[key] = list;
                    }

                    list.Add(skill);
                }

                log.LogInformation("Loaded mastery skill pool config from {Path} with {Count} entries.", path, config.Skills.Count);
                return map;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to load mastery config. Using fallback templates.");
                return new Dictionary<string, List<MasterySkillTemplate>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string BuildTemplateKey(string itemType, string tier)
            => $"{itemType}|{tier}";

        private List<MasterySkillOption> BuildPoolFromTemplates(string itemType, string masteryTier)
        {
            var key = BuildTemplateKey(itemType, masteryTier);
            if (!_templatesByKey.TryGetValue(key, out var templates) || templates.Count == 0)
                return new List<MasterySkillOption>();

            return templates.Select(t => new MasterySkillOption
            {
                SkillId = string.IsNullOrWhiteSpace(t.SkillId)
                    ? $"{itemType}.{masteryTier}.{t.EffectKind.ToString().ToLowerInvariant()}.{Guid.NewGuid():N}"
                    : t.SkillId,
                Name = t.Name,
                Description = t.Description,
                ItemType = itemType,
                MasteryTier = masteryTier,
                EffectKind = t.EffectKind,
                Power = Scale(masteryTier, Math.Max(1, t.BasePower))
            }).ToList();
        }

        private sealed class MasterySkillPoolConfig
        {
            public List<MasterySkillTemplate> Skills { get; set; } = new();
        }

        private sealed class MasterySkillTemplate
        {
            public string SkillId { get; set; } = string.Empty;
            public string ItemType { get; set; } = string.Empty;
            public string MasteryTier { get; set; } = string.Empty;
            public MasteryEffectKind EffectKind { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int BasePower { get; set; }
        }

        private static void ShuffleInPlace<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private List<MasterySkillOption> BuildPool(string itemType, string masteryTier)
        {
            var configured = BuildPoolFromTemplates(itemType, masteryTier);
            if (configured.Count > 0)
                return configured;

            return BuildFallbackPool(itemType, masteryTier);
        }
    }
}
#endif