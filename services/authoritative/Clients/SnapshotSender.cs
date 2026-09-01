#if !UNITY_5_3_OR_NEWER
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Authoritative.Services.Clients
{
    public sealed class SnapshotSender
    {
        private readonly HttpClient _http;

        public SnapshotSender(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<bool> SendSnapshotAsync(string baseUrl, string sessionId, string entityId, string entityType, string snapshotJson, int version = 1, int? ttlSeconds = null, CancellationToken ct = default)
        {
            var url = $"{baseUrl.TrimEnd('/')}/client/world/sessions/{sessionId}/snapshots/{entityId}";
            var body = new
            {
                EntityType = entityType,
                SnapshotJson = snapshotJson
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body)
            };

            // Optionally send TTL/version in headers so receiver can apply them (receiver currently supports version via body parameter)
            if (ttlSeconds.HasValue)
                req.Headers.Add("X-Snapshot-TTL", ttlSeconds.Value.ToString());
            req.Headers.Add("X-Snapshot-Version", version.ToString());

            var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }

        public async Task<bool> UpsertMetadataAsync(string baseUrl, string sessionId, IDictionary<string,string> properties, CancellationToken ct = default)
        {
            var url = $"{baseUrl.TrimEnd('/')}/client/world/sessions/{sessionId}/metadata";
            var res = await _http.PostAsJsonAsync(url, properties, ct).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }
    }
}
#endif

