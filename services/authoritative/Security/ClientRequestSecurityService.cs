#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Authoritative.Security;

public interface IClientRequestSecurityService
{
    ClientAuthSession CreateSession(string userId);
    bool ValidateToken(string token, out ClientAuthSession? session);
    bool ValidateRequest(ClientRequestValidationInput input, out string error);
}

public sealed class ClientRequestSecurityService : IClientRequestSecurityService
{
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NonceTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(30);

    private readonly string _payloadPepper;
    private readonly ConcurrentDictionary<string, ClientAuthSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenNonces = new(StringComparer.Ordinal);

    public ClientRequestSecurityService(IConfiguration configuration)
    {
        _payloadPepper = configuration["CLIENT_PAYLOAD_PEPPER"] ?? "dev-client-pepper";
    }

    public ClientAuthSession CreateSession(string userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var canary = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(TokenTtl);

        var session = new ClientAuthSession(userId.Trim(), token, canary, expiresAtUtc);
        _sessions[token] = session;
        TrimExpiredState(DateTimeOffset.UtcNow);
        return session;
    }

    public bool ValidateToken(string token, out ClientAuthSession? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (!_sessions.TryGetValue(token, out var found))
            return false;

        if (found.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        session = found;
        return true;
    }

    public bool ValidateRequest(ClientRequestValidationInput input, out string error)
    {
        error = string.Empty;

        if (!ValidateToken(input.Token, out var session) || session == null)
        {
            error = "invalid_token";
            return false;
        }

        if (!string.Equals(input.UserId, session.UserId, StringComparison.Ordinal))
        {
            error = "user_mismatch";
            return false;
        }

        if (!string.Equals(input.Canary, session.Canary, StringComparison.Ordinal))
        {
            error = "invalid_canary";
            return false;
        }

        if (!long.TryParse(input.TimestampUnixSeconds, out var unixSeconds))
        {
            error = "invalid_timestamp";
            return false;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var now = DateTimeOffset.UtcNow;
        if (requestTime < now.Subtract(MaxClockSkew) || requestTime > now.Add(MaxClockSkew))
        {
            error = "timestamp_out_of_window";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.Nonce))
        {
            error = "missing_nonce";
            return false;
        }

        var nonceKey = $"{input.Token}:{input.Nonce}";
        if (!_seenNonces.TryAdd(nonceKey, now.Add(NonceTtl)))
        {
            error = "replayed_nonce";
            return false;
        }

        var expectedChecksum = ComputeChecksum(
            input.Token,
            input.UserId,
            input.Canary,
            input.TimestampUnixSeconds,
            input.Nonce,
            input.Method,
            input.PathAndQuery,
            input.Body,
            _payloadPepper);

        if (!ConstantTimeEqualsHex(expectedChecksum, input.Checksum))
        {
            _seenNonces.TryRemove(nonceKey, out _);
            error = "checksum_mismatch";
            return false;
        }

        TrimExpiredState(now);
        return true;
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
            pepper ?? string.Empty,
        });

        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ConstantTimeEqualsHex(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private void TrimExpiredState(DateTimeOffset now)
    {
        foreach (var entry in _sessions)
        {
            if (entry.Value.ExpiresAtUtc <= now)
                _sessions.TryRemove(entry.Key, out _);
        }

        foreach (var entry in _seenNonces)
        {
            if (entry.Value <= now)
                _seenNonces.TryRemove(entry.Key, out _);
        }
    }
}

public sealed record ClientAuthSession(string UserId, string Token, string Canary, DateTimeOffset ExpiresAtUtc);

public sealed record ClientRequestValidationInput(
    string Token,
    string UserId,
    string Canary,
    string TimestampUnixSeconds,
    string Nonce,
    string Checksum,
    string Method,
    string PathAndQuery,
    string Body);

public sealed class ClientLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ClientLoginResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Canary { get; set; } = string.Empty;
    public string ExpiresAtUtc { get; set; } = string.Empty;
}
#endif
