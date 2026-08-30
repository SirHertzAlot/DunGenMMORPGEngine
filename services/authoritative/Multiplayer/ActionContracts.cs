#if !UNITY_5_3_OR_NEWER
using System;

namespace Authoritative.Multiplayer
{
    /// <summary>
    /// Client-to-server authoritative action payload. <c>ActionId</c> is the
    /// idempotency key: replaying the same request returns the stored response
    /// without re-mutating the session.
    /// </summary>
    public sealed class AuthoritativeActionRequest
    {
        public string ActionId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SourcePlayerId { get; set; } = string.Empty;
        public string ActionType { get; set; } = "move";
        public int DeltaX { get; set; }
        public int DeltaY { get; set; }
        public int ExpectedTurn { get; set; } = -1;
    }

    /// <summary>
    /// Unified server response for any authoritative action.
    /// <c>Accepted == true</c> means the action mutated the session.
    /// </summary>
    public sealed class AuthoritativeActionResponse
    {
        public bool Accepted { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Turn { get; set; }
        public bool GameOver { get; set; }
        public string? GameOverReason { get; set; }
        public AuthoritativeGameStateDto? State { get; set; }
    }

    public static class AuthoritativeActionStatus
    {
        public const string Accepted = "accepted";
        public const string Invalid = "invalid";
        public const string Stale = "stale";
        public const string Duplicate = "duplicate";
        public const string Blocked = "blocked";
        public const string Occupied = "occupied";
        public const string SessionUnavailable = "session_unavailable";
    }

    public static class AuthoritativeActionTypes
    {
        public const string Move = "move";
        public const string Attack = "attack";
    }

    /// <summary>
    /// Result of queueing a move against a session (used internally and exposed
    /// to tests). Status is one of <see cref="AuthoritativeActionStatus"/>.
    /// </summary>
    public sealed record AuthoritativeMoveOutcome(string Status, string Message, bool Accepted, AuthoritativeGameStateDto State);
}
#endif