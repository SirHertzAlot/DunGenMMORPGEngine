using Authoritative.Diagnostics;
using Authoritative.Services;
using Authoritative.Services.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Authoritative.Tests;

public sealed class AdminGrpcTests
{
    public sealed class AdminAuthEvaluate
    {
        [Fact]
        public void Denies_when_no_admin_key_is_configured()
        {
            Assert.False(AdminAuth.Evaluate(string.Empty, Headers("anything")));
            Assert.False(AdminAuth.Evaluate("   ", Headers("anything")));
        }

        [Fact]
        public void Denies_when_header_is_missing()
        {
            Assert.False(AdminAuth.Evaluate("secret", null));
            Assert.False(AdminAuth.Evaluate("secret", new Metadata()));
        }

        [Fact]
        public void Denies_when_header_does_not_match()
        {
            var headers = new Metadata();
            headers.Add("x-admin-api-key", "wrong");
            Assert.False(AdminAuth.Evaluate("secret", headers));
        }

        [Fact]
        public void Allows_when_header_matches_exact_case()
        {
            var headers = new Metadata();
            headers.Add("x-admin-api-key", "secret");
            Assert.True(AdminAuth.Evaluate("secret", headers));
        }

        [Fact]
        public void Denies_when_case_differs()
        {
            var headers = new Metadata();
            headers.Add("x-admin-api-key", "Secret");
            Assert.False(AdminAuth.Evaluate("secret", headers));
        }

        [Fact]
        public void Header_lookup_is_case_insensitive_on_key_name()
        {
            var headers = new Metadata();
            headers.Add("X-Admin-Api-Key", "secret");
            Assert.True(AdminAuth.Evaluate("secret", headers));
        }

        static Metadata Headers(string value)
        {
            var headers = new Metadata();
            headers.Add("x-admin-api-key", value);
            return headers;
        }
    }

    public sealed class AdminGrpcServiceTests
    {
        static (AdminGrpcService Service, DiagnosticLogStore Logs, GeneratedItemStore Items) Build()
        {
            var root = Path.Combine(Path.GetTempPath(), "admin-grpc-tests-" + Guid.NewGuid().ToString("N"));
            var logs = new DiagnosticLogStore(root);
            var items = new GeneratedItemStore(root);
            logs.Record(new DiagnosticLogWriteRequest
            {
                Level = "Information",
                Category = "grpc.test",
                EventName = "test.event",
                Message = "hello",
                EntityId = "entity-1",
                SessionId = "session-1"
            });
            var service = new AdminGrpcService(logs, items, NullAuthoritativeMetrics.Instance, NullLogger<AdminGrpcService>.Instance);
            return (service, logs, items);
        }

        [Fact]
        public async Task QueryDiagnostics_returns_mapped_entries()
        {
            var (service, _, _) = Build();
            var reply = await service.QueryDiagnostics(new GrpcDiagnosticQueryRequest { Take = 10 });

            Assert.Single(reply.Entries);
            var entry = reply.Entries[0];
            Assert.Equal("grpc.test", entry.Category);
            Assert.Equal("test.event", entry.EventName);
            Assert.Equal("hello", entry.Message);
            Assert.Equal("entity-1", entry.EntityId);
            Assert.Equal("session-1", entry.SessionId);
            Assert.Equal(1, reply.Total);
        }

        [Fact]
        public async Task QueryDiagnostics_clamps_bounds()
        {
            var (service, _, _) = Build();
            var large = await service.QueryDiagnostics(new GrpcDiagnosticQueryRequest { Take = 100000 });
            Assert.Equal(1000, large.Take);
        }

        [Fact]
        public async Task ListGeneratedItems_returns_bounded_empty_when_none()
        {
            var (service, _, _) = Build();
            var reply = await service.ListGeneratedItems(new GrpcGeneratedItemsRequest { Take = 50 });
            Assert.Empty(reply.Items);
            Assert.Equal(0, reply.Total);
        }

        [Fact]
        public async Task GetHealth_returns_ok_for_authoritative()
        {
            var (service, _, _) = Build();
            var reply = await service.GetHealth();
            Assert.Equal("ok", reply.Status);
            Assert.Equal("authoritative", reply.Service);
        }
    }
}
