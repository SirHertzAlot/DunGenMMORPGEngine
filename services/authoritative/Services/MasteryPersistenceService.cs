#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cassandra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authoritative.Services
{
    public interface IMasteryPersistenceService
    {
        Task UpsertOfferAsync(MasteryOffer offer, CancellationToken ct);
        Task<MasteryOffer?> GetOfferAsync(string offerId, CancellationToken ct);
        Task DeleteOfferAsync(string offerId, CancellationToken ct);
        Task AddUnlockedSkillAsync(string userId, string itemType, MasterySkillOption skill, CancellationToken ct);
        Task<IReadOnlyList<MasterySkillOption>> GetUnlockedSkillsAsync(string userId, string itemType, CancellationToken ct);
    }

    public sealed class MasteryPersistenceService : BackgroundService, IMasteryPersistenceService
    {
        private readonly string _contactPoint;
        private readonly ILogger<MasteryPersistenceService> _log;

        private ICluster? _cluster;
        private ISession? _session;
        private volatile bool _schemaReady;

        private PreparedStatement? _psUpsertOffer;
        private PreparedStatement? _psGetOffer;
        private PreparedStatement? _psDeleteOffer;
        private PreparedStatement? _psInsertUnlocked;
        private PreparedStatement? _psGetUnlocked;

        public MasteryPersistenceService(IConfiguration config, ILogger<MasteryPersistenceService> log)
        {
            _contactPoint = config["SCYLLA_CONTACT_POINT"] ?? "scylla";
            _log = log;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSchemaAsync(cancellationToken).ConfigureAwait(false);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public override void Dispose()
        {
            _session?.Dispose();
            _cluster?.Dispose();
            base.Dispose();
        }

        public async Task UpsertOfferAsync(MasteryOffer offer, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psUpsertOffer == null)
                return;

            var optionsJson = JsonSerializer.Serialize(offer.Options);
            await _session.ExecuteAsync(
                _psUpsertOffer.Bind(
                    offer.OfferId,
                    offer.UserId,
                    offer.ItemType,
                    offer.MasteryTier,
                    new DateTimeOffset(offer.CreatedAtUtc),
                    optionsJson))
                .ConfigureAwait(false);
        }

        public async Task<MasteryOffer?> GetOfferAsync(string offerId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psGetOffer == null)
                return null;

            var rs = await _session.ExecuteAsync(_psGetOffer.Bind(offerId)).ConfigureAwait(false);
            var row = rs.FirstOrDefault();
            if (row == null)
                return null;

            var optionsJson = row.GetValue<string>("options_json");
            var options = JsonSerializer.Deserialize<List<MasterySkillOption>>(optionsJson) ?? new List<MasterySkillOption>();

            return new MasteryOffer
            {
                OfferId = row.GetValue<string>("offer_id"),
                UserId = row.GetValue<string>("user_id"),
                ItemType = row.GetValue<string>("item_type"),
                MasteryTier = row.GetValue<string>("mastery_tier"),
                CreatedAtUtc = row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
                Options = options
            };
        }

        public async Task DeleteOfferAsync(string offerId, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psDeleteOffer == null)
                return;

            await _session.ExecuteAsync(_psDeleteOffer.Bind(offerId)).ConfigureAwait(false);
        }

        public async Task AddUnlockedSkillAsync(string userId, string itemType, MasterySkillOption skill, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psInsertUnlocked == null)
                return;

            var skillJson = JsonSerializer.Serialize(skill);
            await _session.ExecuteAsync(
                _psInsertUnlocked.Bind(
                    userId,
                    itemType,
                    skill.SkillId,
                    skillJson,
                    DateTimeOffset.UtcNow))
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MasterySkillOption>> GetUnlockedSkillsAsync(string userId, string itemType, CancellationToken ct)
        {
            if (_session == null || !_schemaReady || _psGetUnlocked == null)
                return Array.Empty<MasterySkillOption>();

            var rs = await _session.ExecuteAsync(_psGetUnlocked.Bind(userId, itemType)).ConfigureAwait(false);
            var result = new List<MasterySkillOption>();

            foreach (var row in rs)
            {
                var skillJson = row.GetValue<string>("skill_json");
                var skill = JsonSerializer.Deserialize<MasterySkillOption>(skillJson);
                if (skill != null)
                    result.Add(skill);
            }

            return result;
        }

        private async Task InitSchemaAsync(CancellationToken ct)
        {
            const int maxAttempts = 15;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _cluster = Cluster.Builder()
                        .AddContactPoint(_contactPoint)
                        .WithPort(9042)
                        .Build();

                    _session = await Task.Run(() => _cluster.Connect(), ct).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE KEYSPACE IF NOT EXISTS mmo_world
                        WITH replication = {'class':'SimpleStrategy','replication_factor':1}
                        AND durable_writes = true")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement("USE mmo_world")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS mastery_offers (
                            offer_id      TEXT,
                            user_id       TEXT,
                            item_type     TEXT,
                            mastery_tier  TEXT,
                            created_at    TIMESTAMP,
                            options_json  TEXT,
                            PRIMARY KEY (offer_id)
                        )")).ConfigureAwait(false);

                    await _session.ExecuteAsync(new SimpleStatement(@"
                        CREATE TABLE IF NOT EXISTS mastery_unlocked (
                            user_id      TEXT,
                            item_type    TEXT,
                            skill_id     TEXT,
                            skill_json   TEXT,
                            unlocked_at  TIMESTAMP,
                            PRIMARY KEY ((user_id, item_type), skill_id)
                        )")).ConfigureAwait(false);

                    _psUpsertOffer = await _session.PrepareAsync(@"
                        INSERT INTO mastery_offers (offer_id, user_id, item_type, mastery_tier, created_at, options_json)
                        VALUES (?, ?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psGetOffer = await _session.PrepareAsync(@"
                        SELECT offer_id, user_id, item_type, mastery_tier, created_at, options_json
                        FROM mastery_offers
                        WHERE offer_id = ?").ConfigureAwait(false);

                    _psDeleteOffer = await _session.PrepareAsync(@"
                        DELETE FROM mastery_offers WHERE offer_id = ?").ConfigureAwait(false);

                    _psInsertUnlocked = await _session.PrepareAsync(@"
                        INSERT INTO mastery_unlocked (user_id, item_type, skill_id, skill_json, unlocked_at)
                        VALUES (?, ?, ?, ?, ?)").ConfigureAwait(false);

                    _psGetUnlocked = await _session.PrepareAsync(@"
                        SELECT skill_json FROM mastery_unlocked WHERE user_id = ? AND item_type = ?").ConfigureAwait(false);

                    _schemaReady = true;
                    _log.LogInformation("Mastery persistence schema initialized on {ContactPoint}", _contactPoint);
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _log.LogWarning("Mastery schema init attempt {Attempt}/{Max} failed: {Message}", attempt, maxAttempts, ex.Message);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Mastery schema init failed after {Max} attempts. Falling back to no-op persistence.", maxAttempts);
                }
            }
        }
    }
}
#endif