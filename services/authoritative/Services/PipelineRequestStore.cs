#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Authoritative.Services
{
    public interface IPipelineRequestStore
    {
        PipelineRequestRecord Create(PipelineCreateRequest request, string submittedBy, string submittedFrom);
        IReadOnlyCollection<PipelineRequestRecord> GetAll();
        PipelineRequestRecord? Get(string requestId);
        PipelineRequestRecord MarkApproved(string requestId, string approvedBy, string definitionPath, string definitionHash);
        PipelineRequestRecord MarkRejected(string requestId, string rejectedBy, string reason);
    }

    public sealed class PipelineRequestStore : IPipelineRequestStore
    {
        private readonly ConcurrentDictionary<string, PipelineRequestRecord> _requests = new();
        private readonly object _lock = new();
        private readonly string _filePath;

        public PipelineRequestStore()
            : this(Path.Combine(AppContext.BaseDirectory, "data"))
        {
        }

        public PipelineRequestStore(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            _filePath = Path.Combine(dataDirectory, "pipeline-requests.json");
            Load();
        }

        public PipelineRequestRecord Create(PipelineCreateRequest request, string submittedBy, string submittedFrom)
        {
            var now = DateTime.UtcNow;
            var record = new PipelineRequestRecord
            {
                RequestId = "req_" + Guid.NewGuid().ToString("N"),
                Status = PipelineRequestStatus.Pending,
                RequestedConfig = request,
                SubmittedBy = submittedBy,
                SubmittedFrom = submittedFrom,
                SubmittedAtUtc = now
            };

            _requests[record.RequestId] = record;
            Persist();
            return record;
        }

        public IReadOnlyCollection<PipelineRequestRecord> GetAll()
        {
            return _requests.Values
                .OrderByDescending(r => r.SubmittedAtUtc)
                .ToArray();
        }

        public PipelineRequestRecord? Get(string requestId)
        {
            return _requests.TryGetValue(requestId, out var found) ? found : null;
        }

        public PipelineRequestRecord MarkApproved(string requestId, string approvedBy, string definitionPath, string definitionHash)
        {
            if (!_requests.TryGetValue(requestId, out var found))
                throw new InvalidOperationException($"Request {requestId} not found.");

            found.Status = PipelineRequestStatus.Approved;
            found.ReviewedBy = approvedBy;
            found.ReviewedAtUtc = DateTime.UtcNow;
            found.ReviewReason = "Approved";
            found.GeneratedDefinitionPath = definitionPath;
            found.GeneratedDefinitionHash = definitionHash;

            Persist();
            return found;
        }

        public PipelineRequestRecord MarkRejected(string requestId, string rejectedBy, string reason)
        {
            if (!_requests.TryGetValue(requestId, out var found))
                throw new InvalidOperationException($"Request {requestId} not found.");

            found.Status = PipelineRequestStatus.Rejected;
            found.ReviewedBy = rejectedBy;
            found.ReviewedAtUtc = DateTime.UtcNow;
            found.ReviewReason = reason;

            Persist();
            return found;
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
                return;

            var raw = File.ReadAllText(_filePath);
            var list = JsonConvert.DeserializeObject<List<PipelineRequestRecord>>(raw) ?? new List<PipelineRequestRecord>();
            foreach (var item in list)
                _requests[item.RequestId] = item;
        }

        private void Persist()
        {
            lock (_lock)
            {
                var ordered = _requests.Values
                    .OrderByDescending(r => r.SubmittedAtUtc)
                    .ToArray();

                var raw = JsonConvert.SerializeObject(ordered, Formatting.Indented);
                File.WriteAllText(_filePath, raw);
            }
        }
    }
}
#endif
