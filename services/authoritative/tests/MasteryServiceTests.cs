using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if !UNITY_5_3_OR_NEWER
using Authoritative.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Tests
{
    public class MasteryServiceTests
    {
        private static MasteryService CreateService()
        {
            var config = new ConfigurationBuilder().Build();
            var logger = new TestLogger<MasteryService>();
            var persistence = new FakeMasteryPersistenceService();
            return new MasteryService(persistence, config, logger);
        }

        [FactAttribute]
        public async Task GenerateOffer_ReturnsThreeUniqueOptions()
        {
            var service = CreateService();

            var offer = await service.GenerateOfferAsync("user-1", "sword", "master", CancellationToken.None);

            Assert.NotNull(offer);
            Assert.Equal(3, offer.Options.Count);
            Assert.Equal(3, offer.Options.Select(x => x.SkillId).Distinct(StringComparer.Ordinal).Count());
        }

        [FactAttribute]
        public async Task GenerateOffer_MatchesItemTypeAndTier()
        {
            var service = CreateService();

            var offer = await service.GenerateOfferAsync("user-2", "bow", "legendary", CancellationToken.None);

            Assert.All(offer.Options, opt =>
            {
                Assert.Equal("bow", opt.ItemType);
                Assert.Equal("legendary", opt.MasteryTier);
            });
        }

        [FactAttribute]
        public async Task GenerateOffer_HasThreeDifferentEffectKinds()
        {
            var service = CreateService();

            var offer = await service.GenerateOfferAsync("user-3", "staff", "journeyman", CancellationToken.None);
            var distinctKinds = offer.Options.Select(x => x.EffectKind).Distinct().Count();

            Assert.Equal(3, distinctKinds);
        }

        [FactAttribute]
        public async Task SelectOption_UnlocksSkillForUserAndItemType()
        {
            var service = CreateService();
            var offer = await service.GenerateOfferAsync("user-4", "shield", "craftsman", CancellationToken.None);
            var selectedSkill = offer.Options[0];

            var result = await service.SelectOptionAsync("user-4", offer.OfferId, selectedSkill.SkillId, CancellationToken.None);
            var progress = await service.GetProgressAsync("user-4", "shield", CancellationToken.None);

            Assert.Equal("shield", result.ItemType);
            Assert.Equal(selectedSkill.SkillId, result.SelectedOption.SkillId);
            Assert.Equal(1, progress.UnlockedCount);
            Assert.Contains(progress.UnlockedOptions, x => string.Equals(x.SkillId, selectedSkill.SkillId, StringComparison.Ordinal));
        }

        [FactAttribute]
        public async Task SelectOption_InvalidOffer_Throws()
        {
            var service = CreateService();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.SelectOptionAsync("user-5", "missing-offer", "missing-skill", CancellationToken.None));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class FakeMasteryPersistenceService : IMasteryPersistenceService
        {
            private readonly Dictionary<string, MasteryOffer> _offers = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<MasterySkillOption>> _skills = new(StringComparer.Ordinal);

            public Task UpsertOfferAsync(MasteryOffer offer, CancellationToken ct)
            {
                _offers[offer.OfferId] = offer;
                return Task.CompletedTask;
            }

            public Task<MasteryOffer?> GetOfferAsync(string offerId, CancellationToken ct)
            {
                _offers.TryGetValue(offerId, out var offer);
                return Task.FromResult(offer);
            }

            public Task DeleteOfferAsync(string offerId, CancellationToken ct)
            {
                _offers.Remove(offerId);
                return Task.CompletedTask;
            }

            public Task AddUnlockedSkillAsync(string userId, string itemType, MasterySkillOption skill, CancellationToken ct)
            {
                var key = $"{userId}|{itemType}";
                if (!_skills.TryGetValue(key, out var list))
                {
                    list = new List<MasterySkillOption>();
                    _skills[key] = list;
                }

                if (!list.Any(x => string.Equals(x.SkillId, skill.SkillId, StringComparison.Ordinal)))
                    list.Add(skill);

                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<MasterySkillOption>> GetUnlockedSkillsAsync(string userId, string itemType, CancellationToken ct)
            {
                var key = $"{userId}|{itemType}";
                if (!_skills.TryGetValue(key, out var list))
                    return Task.FromResult<IReadOnlyList<MasterySkillOption>>(Array.Empty<MasterySkillOption>());

                return Task.FromResult<IReadOnlyList<MasterySkillOption>>(list.ToArray());
            }
        }

        private sealed class TestLogger<T> : ILogger<T>
        {
            public bool IsEnabled(LogLevel logLevel) => true;
            IDisposable? ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
#endif