#if !UNITY_5_3_OR_NEWER
using Microsoft.Extensions.Configuration;

namespace Authoritative.Security
{
    /// <summary>
    /// Central gate for the development-only credential fallbacks (Postgres
    /// mmouser/mmopass, dev-client-pepper, dev-admin-key). Production deployments
    /// must set the real secrets directly; the fallbacks only apply when the
    /// compose dev stack signal is present or ALLOW_DEV_CREDENTIALS is enabled.
    /// </summary>
    public static class DevCredentials
    {
        public static bool AreEnabled(IConfiguration configuration)
        {
            var flag = configuration["ALLOW_DEV_CREDENTIALS"];
            if (!string.IsNullOrWhiteSpace(flag))
            {
                return flag is "1" or "true" or "TRUE" or "yes" or "YES" or "on" or "ON";
            }

            // The unmodifiable docker-compose.yml dev stack does not pass the
            // flag itself; the base Dockerfile and both compose services always
            // set RABBITMQ_HOST=rabbitmq, which identifies the compose container.
            return string.Equals(configuration["RABBITMQ_HOST"], "rabbitmq", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class PostgresConnectionString
    {
        private const string DevFallback = "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb";

        public static string? Resolve(IConfiguration configuration)
        {
            var configured = configuration["POSTGRES_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            return DevCredentials.AreEnabled(configuration) ? DevFallback : null;
        }
    }
}
#endif