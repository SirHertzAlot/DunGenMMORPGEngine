#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace Authoritative.Multiplayer
{
    public sealed class AuthoritativeGameStateDto
    {
        public int Turn { get; set; }
        public bool GameOver { get; set; }
        public string? GameOverReason { get; set; }
        public bool InCombat { get; set; }
        public bool PlayerAlive { get; set; }
        public AuthoritativePlayerDto Player { get; set; } = new();
        public List<AuthoritativeEnemyDto> Enemies { get; set; } = new();
        public IReadOnlyList<AuthoritativeWorldEventDto> RecentEvents { get; set; } = Array.Empty<AuthoritativeWorldEventDto>();
    }

    public sealed class AuthoritativePlayerDto
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Gold { get; set; }
        public int Experience { get; set; }
        public int XpToNextLevel { get; set; }
        public int ArmorClass { get; set; }
        public int AttackModifier { get; set; }
        public int MovementSpeed { get; set; }
        public bool IsDead { get; set; }
        public bool InCombat { get; set; }
    }

    public sealed class AuthoritativeEnemyDto
    {
        public int Id { get; set; }
        public string Archetype { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int ArmorClass { get; set; }
        public int AttackModifier { get; set; }
        public bool IsDead { get; set; }
        public bool InCombat { get; set; }
    }

    /// <summary>
    /// Deterministic event emitted during turn resolution. Event ids are stable
    /// per session replay sequence (<c>evt_{seq}</c>).
    /// </summary>
    public sealed class AuthoritativeWorldEventDto
    {
        public string EventId { get; set; } = string.Empty;
        public int Turn { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AuthoritativeTimelineEnvelope
    {
        public string SessionId { get; set; } = string.Empty;
        public IReadOnlyCollection<AuthoritativeWorldEventDto> Events { get; set; } = Array.Empty<AuthoritativeWorldEventDto>();
    }
}
#endif