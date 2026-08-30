#if !UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Authoritative.Security;

namespace Authoritative.Services
{
    /// <summary>
    /// Simple migration runner that applies SQL files from
    /// Assets/DunGenMMORPGEngine/db/migrations in filename order and
    /// records applied files in `schema_migrations`.
    /// </summary>
    public class MigrationHostedService : IHostedService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MigrationHostedService> _log;

        public MigrationHostedService(IConfiguration config, ILogger<MigrationHostedService> log)
        {
            _config = config;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var connStr = PostgresConnectionString.Resolve(_config);
            if (connStr == null)
            {
                _log.LogWarning("POSTGRES_CONNECTION_STRING is not configured and development credentials are not enabled. Skipping database migrations.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_config["POSTGRES_CONNECTION_STRING"]))
                _log.LogWarning("POSTGRES_CONNECTION_STRING is not configured; using development database credentials for migrations.");

            string migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "DunGenMMORPGEngine", "db", "migrations");
            if (!Directory.Exists(migrationsPath))
            {
                // Try relative to base directory (when running from build output)
                migrationsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "DunGenMMORPGEngine", "db", "migrations");
                migrationsPath = Path.GetFullPath(migrationsPath);
            }

            if (!Directory.Exists(migrationsPath))
            {
                _log.LogWarning("Migrations directory not found: {Path}. Skipping migrations.", migrationsPath);
                return;
            }

            try
            {
                await using var conn = new NpgsqlConnection(connStr);
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS schema_migrations (
                        id TEXT PRIMARY KEY,
                        applied_at TIMESTAMPTZ NOT NULL
                    )";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                var files = Directory.GetFiles(migrationsPath, "*.sql")
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = Path.GetFileName(file);

                    // Check if already applied
                    await using (var check = conn.CreateCommand())
                    {
                        check.CommandText = "SELECT 1 FROM schema_migrations WHERE id = @id";
                        check.Parameters.AddWithValue("id", id);
                        var exists = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        if (exists != null) continue;
                    }

                    _log.LogInformation("Applying migration {File}", id);
                    var sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);

                    await using (var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
                    {
                        try
                        {
                            await using (var apply = conn.CreateCommand())
                            {
                                apply.Transaction = tx;
                                apply.CommandText = sql;
                                await apply.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                            }

                            await using (var ins = conn.CreateCommand())
                            {
                                ins.Transaction = tx;
                                ins.CommandText = "INSERT INTO schema_migrations (id, applied_at) VALUES (@id, now())";
                                ins.Parameters.AddWithValue("id", id);
                                await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                            }

                            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                            _log.LogInformation("Migration applied: {File}", id);
                        }
                        catch (Exception ex)
                        {
                            try { await tx.RollbackAsync(cancellationToken).ConfigureAwait(false); } catch { }
                            _log.LogError(ex, "Failed to apply migration {File}", id);
                            throw; // stop processing further migrations
                        }
                    }
                }

                _log.LogInformation("Postgres migrations complete (dir={Dir}).", migrationsPath);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Migration runner failed");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
#endif
