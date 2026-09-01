using System;
using System.IO;
using System.Threading.Tasks;
using Authoritative.Diagnostics;
using Authoritative.Services;
using Authoritative.Services.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

namespace Authoritative.Tests
{
    public class AdminGrpcTests
    {
        public class AdminAuthEvaluate
        {
            [FactAttribute]
            public void DeniesWhenNoAdminKeyIsConfigured()
            {
                Assert.False(AdminAuth.Evaluate(string.Empty, Headers("anything")));
                Assert.False(AdminAuth.Evaluate("   ", Headers("anything")));
            }

            [FactAttribute]
            public void DeniesWhenHeaderIsMissing()
            {
                Assert.False(AdminAuth.Evaluate("secret", null));
                Assert.False(AdminAuth.Evaluate("secret", new Metadata()));
            }

            [FactAttribute]
            public void DeniesWhenHeaderDoesNotMatch()
            {
                var headers = new Metadata();
                headers.Add("x-admin-api-key", "wrong");
                Assert.False(AdminAuth.Evaluate("secret", headers));
            }

            [FactAttribute]
            public void AllowsWhenHeaderMatchesExactCase()
            {
                var headers = new Metadata();
                headers.Add("x-admin-api-key", "secret");
                Assert.True(AdminAuth.Evaluate("secret", headers));
            }

            [FactAttribute]
            public void DeniesWhenCaseDiffers()
            {
                var headers = new Metadata();
                headers.Add("x-admin-api-key", "Secret");
                Assert.False(AdminAuth.Evaluate("secret", headers));
            }

            [FactAttribute]
            public void HeaderLookupIsCaseInsensitiveOnKeyName()
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

        public class AdminGrpcServiceTests
        {
            static (AdminGrpcService Service, DiagnosticLogStore Logs, GeneratedItemStore Items) Build()
            {
                var root = Path.Combine(Path.GetTempPath(), "admin-grpc-tests-" + Guid.NewGuid().ToString("N"));
                var logs = new DiagnosticLogStore(root);
                var items = new GeneratedItemStore(root, persistToDisk: false);
                logs.Record(new DiagnosticLogWriteRequest
                {
                    Level = "Information",
                    Category = "grpc.test",
                    EventName = "test.event",
                    Message = "hello",
                    EntityId = "entity-1",
                    SessionId = "session-1"
                });
                var service = new AdminGrpcService(logs, items, NullLogger<AdminGrpcService>.Instance);
                return (service, logs, items);
            }

            [FactAttribute]
            public async Task QueryDiagnosticsReturnsMappedEntries()
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

            [FactAttribute]
            public async Task QueryDiagnosticsClampsBounds()
            {
                var (service, _, _) = Build();
                var large = await service.QueryDiagnostics(new GrpcDiagnosticQueryRequest { Take = 100000 });
                Assert.Equal(1000, large.Take);
            }

            [FactAttribute]
            public async Task ListGeneratedItemsReturnsBoundedEmptyWhenNone()
            {
                var (service, _, _) = Build();
                var reply = await service.ListGeneratedItems(new GrpcGeneratedItemsRequest { Take = 50 });
                Assert.Empty(reply.Items);
                Assert.Equal(0, reply.Total);
            }

            [FactAttribute]
            public async Task GetHealthReturnsOkForAuthoritative()
            {
                var (service, _, _) = Build();
                var reply = await service.GetHealth();
                Assert.Equal("ok", reply.Status);
                Assert.Equal("authoritative", reply.Service);
            }
        }
    }
}
