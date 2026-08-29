using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;

namespace DunGen.Networking
{
    public static class ClientInteractionSecurityLayer
    {
        public static void ApplySecurityHeaders(UnityWebRequest request, BackendConnectionConfig config)
        {
            if (request == null || config == null)
                return;

            var token = Startup.ClientAuthState.AuthToken;
            var canary = Startup.ClientAuthState.RequestCanary;
            var userId = Startup.ClientAuthState.AuthenticatedUsername;
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(canary) || string.IsNullOrWhiteSpace(userId))
                return;

            var method = request.method ?? "GET";
            var uri = request.uri;
            var pathAndQuery = uri != null ? (uri.PathAndQuery ?? "/") : "/";
            var body = string.Empty;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");

            var checksum = ComputeChecksum(
                token,
                userId,
                canary,
                timestamp,
                nonce,
                method,
                pathAndQuery,
                body,
                config.ClientPayloadPepper);

            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("X-Client-User", userId);
            request.SetRequestHeader("X-Client-Canary", canary);
            request.SetRequestHeader("X-Client-Timestamp", timestamp);
            request.SetRequestHeader("X-Client-Nonce", nonce);
            request.SetRequestHeader("X-Client-Checksum", checksum);
        }

        public static string ComputeChecksum(
            string token,
            string userId,
            string canary,
            string timestamp,
            string nonce,
            string method,
            string pathAndQuery,
            string body,
            string pepper)
        {
            var payload = string.Join("|", new[]
            {
                token ?? string.Empty,
                userId ?? string.Empty,
                canary ?? string.Empty,
                timestamp ?? string.Empty,
                nonce ?? string.Empty,
                method ?? string.Empty,
                pathAndQuery ?? string.Empty,
                body ?? string.Empty,
                pepper ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
