#if !UNITY_5_3_OR_NEWER
using System;
using System.Linq;
using Grpc.Core;

namespace Authoritative.Services.Grpc
{
    /// <summary>
    /// Pure, dependency-free authorization decision for the admin gRPC surface so
    /// the deny-by-default policy is unit-testable without instantiating a MagicOnion
    /// <see cref="MagicOnion.Server.ServiceContext"/>.
    /// </summary>
    public static class AdminAuth
    {
        /// <summary>
        /// Returns <see langword="true"/> only when an admin key is configured and the
        /// presented header value matches it exactly. When no key is configured the
        /// surface is denied closed, and a missing/mismatched header is rejected.
        /// </summary>
        public static bool Evaluate(string expectedKey, Metadata? headers)
        {
            if (string.IsNullOrWhiteSpace(expectedKey))
                return false;

            var headerValue = headers
                ?.FirstOrDefault(h => string.Equals(h.Key, "x-admin-api-key", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return string.Equals(headerValue, expectedKey, StringComparison.Ordinal);
        }
    }
}
#endif
