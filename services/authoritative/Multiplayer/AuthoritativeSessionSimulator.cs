#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Authoritative.Domain;
using Authoritative.Services;

namespace Authoritative.Multiplayer
{
    /// <summary>
    /// Server-authoritative turn engine that mirrors the Unity GameSession
    /// drive loop (ExecuteTurn → exploration/moves → combat → rewards → state):
    ///   • Move actions VALIDATE + QUEUE against the current turn and do NOT
    ///     advance it (deviation from the Unity client drive loop, where every
    ///     accepted command resolves a turn immediately). The queue is applied
    ///     when the next attack action resolves the turn.
    ///   • Attack actions resolve one full turn: apply queued moves, move
    ///     enemies, start nearby combats, resolve combat with the exact Unity
    ///     seeding (two RNG streams both seeded with
    ///     (uint)(seed + turn + combatSessionId)), then award gold/XP.
    /// All randomness flows through <see cref="DeterministicRng"/> so a session
    /// replays byte-for-byte for the same world and command sequence.
    /// </summary>
    public sealed class AuthoritativeSessionSimulator
    {
        private const int InternalPlayerId = 0;
        private const int DisplayPlayerId = 1;
        private const int EnemyMoveSpeed = 3;
        private const int PlayerExperiencePerKill = 50;
        private const int MaxRecentEvents = 256;

        private sealed class SimEnemy
        {
            public int Id;
            public string Archetype = string.Empty;
            public int X;
            public int Y;
            public int Level;
            public int Health;
            public int MaxHealth;
            public int ArmorClass;
            public int AttackModifier;
            public int GoldDropMin;
            public int GoldDropMax;
            public bool DropOnDeath = true;
            public int TilesMovedThisTurn;
            public bool IsDead;
            public bool InCombat;
            public int CombatSessionId = -1;
            public int InitiativeScore;
        }

        private sealed class SimPlayer
        {
            public int X;
            public int Y;
            public int Level = 1;
            public int Health = 100;
            public int MaxHealth = 100;
            public int ArmorClass = 12;
            public int StrengthModifier = 2;
            public int DexterityModifier = 1;
            public int ProficiencyBonus = 2;
            public int MoveSpeed = 5;
            public int Gold;
            public int Experience;
            public int XpToNextLevel = 100;
            public int TilesMovedThisTurn;
            public bool IsDead;
            public bool InCombat;
            public int CombatSessionId = -1;
            public int InitiativeScore;
        }

        private readonly object _gate = new();
        private readonly int _seed;
        private readonly int _width;
        private readonly int _height;
        private readonly bool[,] _walkable;
        private readonly DeterministicRng _sessionRng;
        private readonly SimPlayer _player = new();
        private readonly List<SimEnemy> _enemies = new();
        private readonly List<(int Dx, int Dy)> _queuedMoves = new();
        private readonly List<AuthoritativeWorldEventDto> _events = new();
        private int _nextCombatSessionId = 100;
        private int _eventSeq;

        public AuthoritativeSessionSimulator(GeneratedWorldArtifact world)
        {
            _seed = world.Seed;
            _width = System.Math.Max(8, world.Width);
            _height = System.Math.Max(8, world.Height);
            _walkable = new bool[_width, _height];
            _sessionRng = new DeterministicRng((ulong)(uint)world.Seed);

            var rooms = world.Rooms.OrderBy(r => r.Id).ToList();
            if (rooms.Count == 0)
            {
                rooms.Add(new WorldRoom
                {
                    Id = 1,
                    X = 1,
                    Y = 1,
                    Width = System.Math.Min(12, _width - 2),
                    Height = System.Math.Min(10, _height - 2)
                });
            }

            BuildWalkableGrid(rooms);
            PlacePlayer(rooms[0].X, rooms[0].Y, rooms[0].Width, rooms[0].Height);
            SpawnEnemies(world, rooms);
        }

        public object SyncRoot => _gate;
        public int TurnCount { get; private set; }
        public bool IsGameOver { get; private set; }
        public string GameOverReason { get; private set; } = string.Empty;
        public bool IsAvailable => !IsGameOver;

        // ── Command surface (caller must hold _gate) ────────────────────────

        public AuthoritativeMoveOutcome QueueMove(int deltaX, int deltaY, int expectedTurn)
        {
            if (!IsAvailable)
                return Outcome(AuthoritativeActionStatus.SessionUnavailable, "Session is closed.", accepted: false);

            if (expectedTurn >= 0 && expectedTurn != TurnCount)
                return Outcome(AuthoritativeActionStatus.Stale,
                    $"Rejected stale command for turn {expectedTurn}; current turn is {TurnCount}.", false);

            if (System.Math.Abs(deltaX) + System.Math.Abs(deltaY) != 1)
                return Outcome(AuthoritativeActionStatus.Invalid,
                    "Invalid movement command. Use exactly one tile per command (deltaX/deltaY of ±1).", false);

            if (_queuedMoves.Any(m => m.Dx == deltaX && m.Dy == deltaY))
                return Outcome(AuthoritativeActionStatus.Duplicate,
                    $"Duplicate movement command ({deltaX}, {deltaY}) rejected; the same move is already queued this turn.", false);

            if (_queuedMoves.Count >= _player.MoveSpeed)
                return Outcome(AuthoritativeActionStatus.Stale,
                    "Movement budget exhausted for this turn.", false);

            int projectedX = _player.X;
            int projectedY = _player.Y;
            foreach (var queued in _queuedMoves)
            {
                projectedX += queued.Dx;
                projectedY += queued.Dy;
            }

            int nextX = projectedX + deltaX;
            int nextY = projectedY + deltaY;
            if (!IsWalkable(nextX, nextY))
                return Outcome(AuthoritativeActionStatus.Blocked, $"Blocked tile at ({nextX}, {nextY}).", false);

            if (IsOccupiedByLivingEnemy(nextX, nextY))
                return Outcome(AuthoritativeActionStatus.Occupied,
                    $"Occupied tile at ({nextX}, {nextY}) by a living enemy.", false);

            _queuedMoves.Add((deltaX, deltaY));
            return Outcome(AuthoritativeActionStatus.Accepted,
                $"Queued movement command ({deltaX}, {deltaY}) for turn {TurnCount}.", accepted: true);
        }

        /// <summary>
        /// Resolves one full turn (the attack action). Assumes the caller holds
        /// <see cref="SyncRoot"/>.
        /// </summary>
        public AuthoritativeActionResponse ResolveTurn(int expectedTurn)
        {
            if (!IsAvailable)
                return Response(AuthoritativeActionStatus.SessionUnavailable,
                    "Session is closed.", accepted: false, turn: TurnCount, gameOver: true,
                    GameOverReason, BuildState());

            if (expectedTurn >= 0 && expectedTurn != TurnCount)
                return Response(AuthoritativeActionStatus.Stale,
                    $"Rejected stale command for turn {expectedTurn}; current turn is {TurnCount}.",
                    accepted: false, turn: TurnCount, gameOver: IsGameOver, GameOverReason, BuildState());

            ExecuteTurn();

            return Response(AuthoritativeActionStatus.Accepted,
                $"Turn {TurnCount} resolved.", accepted: true, turn: TurnCount,
                gameOver: IsGameOver, GameOverReason, BuildState());
        }

        // ── End-of-turn state surface (caller must hold _gate) ───────────────

        public AuthoritativeGameStateDto BuildState()
        {
            var state = new AuthoritativeGameStateDto
            {
                Turn = TurnCount,
                GameOver = IsGameOver,
                GameOverReason = IsGameOver ? GameOverReason : null,
                InCombat = _player.InCombat,
                PlayerAlive = !_player.IsDead,
                Player = new AuthoritativePlayerDto
                {
                    Id = DisplayPlayerId,
                    X = _player.X,
                    Y = _player.Y,
                    Level = _player.Level,
                    Health = _player.Health,
                    MaxHealth = _player.MaxHealth,
                    Gold = _player.Gold,
                    Experience = _player.Experience,
                    XpToNextLevel = _player.XpToNextLevel,
                    ArmorClass = _player.ArmorClass,
                    AttackModifier = _player.StrengthModifier + _player.ProficiencyBonus,
                    MovementSpeed = _player.MoveSpeed,
                    IsDead = _player.IsDead,
                    InCombat = _player.InCombat
                }
            };

            foreach (var enemy in _enemies.OrderBy(e => e.Id))
            {
                state.Enemies.Add(new AuthoritativeEnemyDto
                {
                    Id = enemy.Id,
                    Archetype = enemy.Archetype,
                    X = enemy.X,
                    Y = enemy.Y,
                    Level = enemy.Level,
                    Health = enemy.Health,
                    MaxHealth = enemy.MaxHealth,
                    ArmorClass = enemy.ArmorClass,
                    AttackModifier = enemy.AttackModifier,
                    IsDead = enemy.IsDead,
                    InCombat = enemy.InCombat
                });
            }

            state.RecentEvents = _events.Skip(System.Math.Max(0, _events.Count - 8)).ToArray();
            return state;
        }

        public IReadOnlyList<AuthoritativeWorldEventDto> GetRecentEvents(int take)
        {
            return _events.Skip(System.Math.Max(0, _events.Count - take)).ToArray();
        }

        // ── Turn pipeline (mirrors Unity GameSession.ExecuteTurn exactly) ────

        private void ExecuteTurn()
        {
            TurnCount++;

            ResetMovementForNewTurn();
            ResolveExplorationStep();
            ResolveCombatStep();
            ResolveRewardsAndProgression();
            CheckGameState();
        }

        private void ResetMovementForNewTurn()
        {
            _player.TilesMovedThisTurn = 0;
            foreach (var enemy in _enemies)
                enemy.TilesMovedThisTurn = 0;
        }

        private void ResolveExplorationStep()
        {
            if (_player.IsDead)
                return;

            if (!_player.InCombat)
                ApplyQueuedMoves();

            MoveEnemies();
            StartNearbyCombats();
        }

        /// <summary>
        /// Applies every move queued this turn. Mirrors Unity's
        /// TryConsumePendingPlayerMove guard (walkability + budget) while
        /// refusing to walk into a living enemy.
        /// </summary>
        private void ApplyQueuedMoves()
        {
            foreach (var (dx, dy) in _queuedMoves)
            {
                if (_player.TilesMovedThisTurn >= _player.MoveSpeed)
                    break;

                int nextX = _player.X + dx;
                int nextY = _player.Y + dy;
                if (!IsWalkable(nextX, nextY))
                    continue;
                if (IsOccupiedByLivingEnemy(nextX, nextY))
                    continue;

                _player.X = nextX;
                _player.Y = nextY;
                _player.TilesMovedThisTurn++;
                Emit("move_applied", $"Player moved to ({nextX}, {nextY}).");
            }

            _queuedMoves.Clear();
        }

        private void MoveEnemies()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.InCombat)
                    continue;
                if (enemy.TilesMovedThisTurn >= EnemyMoveSpeed)
                    continue;

                int dx = System.Math.Sign(_player.X - enemy.X);
                int dy = System.Math.Sign(_player.Y - enemy.Y);
                if (dx == 0 && dy == 0)
                    continue;

                int nextX = enemy.X + dx;
                int nextY = enemy.Y + dy;
                if (!IsWalkable(nextX, nextY))
                    continue;

                enemy.X = nextX;
                enemy.Y = nextY;
                enemy.TilesMovedThisTurn++;
            }
        }

        private void StartNearbyCombats()
        {
            if (_player.IsDead || _player.InCombat)
                return;

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.InCombat)
                    continue;

                int distance = Manhattan(enemy.X, enemy.Y, _player.X, _player.Y);
                if (distance <= 1)
                {
                    StartCombat(enemy);
                    return;
                }
            }
        }

        private void StartCombat(SimEnemy enemy)
        {
            int combatSessionId = _nextCombatSessionId++;
            int playerInitiative = _sessionRng.RollD20() + _player.DexterityModifier;
            int enemyInitiative = _sessionRng.RollD20();

            _player.InCombat = true;
            _player.CombatSessionId = combatSessionId;
            _player.InitiativeScore = playerInitiative;

            enemy.InCombat = true;
            enemy.CombatSessionId = combatSessionId;
            enemy.InitiativeScore = enemyInitiative;

            Emit("combat_started",
                $"Combat {combatSessionId} started with enemy {enemy.Id}. Player initiative {playerInitiative}, enemy initiative {enemyInitiative}.");
        }

        private void ResolveCombatStep()
        {
            if (!_player.InCombat || _player.IsDead)
                return;

            int combatSessionId = _player.CombatSessionId;

            var participants = new List<(int Id, int Initiative)>
            {
                (InternalPlayerId, _player.InitiativeScore)
            };
            foreach (var enemy in _enemies)
            {
                if (enemy.CombatSessionId != combatSessionId || enemy.IsDead)
                    continue;
                participants.Add((enemy.Id, enemy.InitiativeScore));
            }

            participants.Sort((a, b) =>
            {
                int scoreCompare = b.Initiative.CompareTo(a.Initiative);
                return scoreCompare != 0 ? scoreCompare : a.Id.CompareTo(b.Id);
            });

            uint combatSeed = (uint)(_seed + TurnCount + combatSessionId);
            var attackRng = new DeterministicRng(combatSeed);
            var damageRng = new DeterministicRng(combatSeed);

            foreach (var (actorId, _) in participants)
            {
                if (actorId == InternalPlayerId)
                {
                    if (_player.IsDead || !_player.InCombat)
                        continue;

                    var target = FindNearestHostile(combatSessionId);
                    if (target == null)
                        continue;

                    ExecuteAttack(isPlayer: true, target, attackRng, damageRng);
                }
                else
                {
                    var enemy = FindEnemy(actorId);
                    if (enemy == null || enemy.IsDead || !enemy.InCombat)
                        continue;
                    if (_player.IsDead)
                        continue;

                    ExecuteAttack(isPlayer: false, enemy, attackRng, damageRng);
                }
            }

            FinalizeCombatState(combatSessionId);
        }

        private void ExecuteAttack(bool isPlayer, SimEnemy target, DeterministicRng attackRng, DeterministicRng damageRng)
        {
            int attackModifier;
            string attackerName;
            if (isPlayer)
            {
                attackModifier = _player.StrengthModifier + _player.ProficiencyBonus;
                attackerName = "Player";
            }
            else
            {
                attackModifier = target.AttackModifier;
                attackerName = $"Enemy {target.Id}";
            }

            int targetArmorClass = isPlayer ? target.ArmorClass : _player.ArmorClass;

            int d20 = attackRng.RollD20();
            bool isCritical = d20 == 20;
            bool isFumble = d20 == 1;
            bool isHit;

            if (isFumble)
                isHit = false;
            else if (isCritical)
                isHit = true;
            else
                isHit = (d20 + attackModifier) >= targetArmorClass;

            int damage = 0;
            if (isHit)
            {
                int d8 = damageRng.RollDice(8);
                damage = System.Math.Max(1, (d8 + attackModifier) * (isCritical ? 2 : 1));
            }

            string defenderName;
            if (isPlayer)
            {
                target.Health = System.Math.Max(0, target.Health - damage);
                defenderName = $"Enemy {target.Id}";
                if (target.Health == 0 && !target.IsDead)
                {
                    target.IsDead = true;
                    target.InCombat = false;
                    Emit("enemy_died", $"Enemy {target.Id} was defeated by the player.");
                }
            }
            else
            {
                _player.Health = System.Math.Max(0, _player.Health - damage);
                defenderName = "Player";
                if (_player.Health == 0 && !_player.IsDead)
                {
                    _player.IsDead = true;
                    _player.InCombat = false;
                    Emit("player_died", "The player was defeated.");
                }
            }

            if (damage > 0)
                Emit("attack", $"{attackerName} hit {defenderName} for {damage} damage (d20={d20}).");
            else
                Emit("attack", $"{attackerName} attacked {defenderName} (d20={d20}).");
        }

        private void FinalizeCombatState(int combatSessionId)
        {
            int livingCount = 0;
            if (!_player.IsDead && _player.CombatSessionId == combatSessionId)
                livingCount++;

            foreach (var enemy in _enemies)
            {
                if (enemy.CombatSessionId == combatSessionId && !enemy.IsDead)
                    livingCount++;
            }

            if (livingCount > 1)
                return;

            foreach (var enemy in _enemies)
            {
                if (enemy.CombatSessionId != combatSessionId)
                    continue;
                enemy.InCombat = false;
                enemy.CombatSessionId = -1;
            }

            _player.InCombat = false;
            _player.CombatSessionId = -1;
            Emit("combat_end", $"Combat {combatSessionId} ended.");
        }

        private void ResolveRewardsAndProgression()
        {
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsDead || !enemy.DropOnDeath)
                    continue;

                int goldAward = System.Math.Max(enemy.GoldDropMin, enemy.GoldDropMax);
                _player.Gold += goldAward;
                _player.Experience += PlayerExperiencePerKill;
                Emit("loot_granted", $"Enemy {enemy.Id} awarded {goldAward} gold and {PlayerExperiencePerKill} XP.");

                while (_player.Experience >= _player.XpToNextLevel)
                {
                    _player.Experience -= _player.XpToNextLevel;
                    _player.Level++;
                    _player.XpToNextLevel = _player.Level * 100;
                    Emit("level_up", $"Player reached level {_player.Level}.");
                }

                enemy.DropOnDeath = false;
            }

            if (GetLivingEnemyCount() > 0)
                return;

            IsGameOver = true;
            GameOverReason = "Authoritative world cleared.";
            Emit("game_over", "Authoritative world cleared. Session complete.");
        }

        private void CheckGameState()
        {
            if (!_player.IsDead)
                return;

            IsGameOver = true;
            GameOverReason = "Player defeated!";
            Emit("game_over", "GAME OVER: Player defeated!");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private SimEnemy? FindNearestHostile(int combatSessionId)
        {
            SimEnemy? nearest = null;
            int bestDistance = int.MaxValue;

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead)
                    continue;
                if (enemy.CombatSessionId != combatSessionId)
                    continue;

                int distance = Manhattan(enemy.X, enemy.Y, _player.X, _player.Y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private SimEnemy? FindEnemy(int id)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.Id == id)
                    return enemy;
            }
            return null;
        }

        private int GetLivingEnemyCount()
        {
            int count = 0;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsDead)
                    count++;
            }
            return count;
        }

        private bool IsOccupiedByLivingEnemy(int x, int y)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead)
                    continue;
                if (enemy.X == x && enemy.Y == y)
                    return true;
            }
            return false;
        }

        private bool IsWalkable(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return false;
            return _walkable[x, y];
        }

        private static int Manhattan(int x1, int y1, int x2, int y2)
        {
            return System.Math.Abs(x1 - x2) + System.Math.Abs(y1 - y2);
        }

        private void BuildWalkableGrid(List<WorldRoom> rooms)
        {
            foreach (var room in rooms)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                    {
                        if (x >= 0 && x < _width && y >= 0 && y < _height)
                            _walkable[x, y] = true;
                    }
                }
            }

            // L-shaped corridors between consecutive room centers.
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                int x1 = rooms[i].X + rooms[i].Width / 2;
                int y1 = rooms[i].Y + rooms[i].Height / 2;
                int x2 = rooms[i + 1].X + rooms[i + 1].Width / 2;
                int y2 = rooms[i + 1].Y + rooms[i + 1].Height / 2;

                for (int x = System.Math.Min(x1, x2); x <= System.Math.Max(x1, x2); x++)
                    MarkWalkable(x, y1);
                for (int y = System.Math.Min(y1, y2); y <= System.Math.Max(y1, y2); y++)
                    MarkWalkable(x2, y);
            }
        }

        private void MarkWalkable(int x, int y)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
                _walkable[x, y] = true;
        }

        private void PlacePlayer(int roomX, int roomY, int roomWidth, int roomHeight)
        {
            _player.X = System.Math.Clamp(roomX + roomWidth / 2, 0, _width - 1);
            _player.Y = System.Math.Clamp(roomY + roomHeight / 2, 0, _height - 1);

            if (!IsWalkable(_player.X, _player.Y))
            {
                var snapped = SnapToWalkable(_player.X, _player.Y);
                _player.X = snapped.X;
                _player.Y = snapped.Y;
            }
        }

        private void SpawnEnemies(GeneratedWorldArtifact world, List<WorldRoom> rooms)
        {
            var orderedEnemies = world.Enemies.OrderBy(e => e.Id).ToList();
            for (int i = 0; i < orderedEnemies.Count; i++)
            {
                var source = orderedEnemies[i];
                var snapped = SnapToWalkable(
                    System.Math.Clamp(source.X, 0, _width - 1),
                    System.Math.Clamp(source.Y, 0, _height - 1));

                int level = System.Math.Max(1, source.Level);
                int strengthModifier = 1 + System.Math.Clamp(level / 4, 0, 4);
                int proficiencyBonus = 2 + System.Math.Clamp((level - 1) / 4, 0, 4);

                var (goldDropMin, goldDropMax) = ResolveGoldDrop(world, i);

                _enemies.Add(new SimEnemy
                {
                    Id = source.Id,
                    Archetype = string.IsNullOrWhiteSpace(source.Archetype) ? "enemy" : source.Archetype,
                    X = snapped.X,
                    Y = snapped.Y,
                    Level = level,
                    MaxHealth = 35 + (level * 15),
                    Health = 35 + (level * 15),
                    ArmorClass = 10 + System.Math.Clamp(level / 3, 0, 6),
                    AttackModifier = strengthModifier + proficiencyBonus,
                    GoldDropMin = goldDropMin,
                    GoldDropMax = goldDropMax
                });
            }
        }

        private static (int Min, int Max) ResolveGoldDrop(GeneratedWorldArtifact world, int enemyIndex)
        {
            // Mirrors Unity: enemy i reads the loot blueprint at index i (if any).
            if (enemyIndex >= 0 && enemyIndex < world.Loot.Count)
            {
                string tier = (world.Loot[enemyIndex].Tier ?? string.Empty).ToLowerInvariant();
                switch (tier)
                {
                    case "legendary":
                        return (80, 140);
                    case "epic":
                        return (50, 90);
                    case "rare":
                        return (25, 55);
                }
            }

            return (10, 50);
        }

        /// <summary>
        /// Deterministic snap: nearest walkable tile, ties broken by X then Y.
        /// </summary>
        private (int X, int Y) SnapToWalkable(int x, int y)
        {
            if (IsWalkable(x, y))
                return (x, y);

            for (int radius = 1; radius <= System.Math.Max(_width, _height); radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (System.Math.Abs(dx) + System.Math.Abs(dy) != radius)
                            continue;

                        int candidateX = x + dx;
                        int candidateY = y + dy;
                        if (IsWalkable(candidateX, candidateY))
                            return (candidateX, candidateY);
                    }
                }
            }

            return (0, 0);
        }

        private void Emit(string type, string message)
        {
            _events.Add(new AuthoritativeWorldEventDto
            {
                EventId = $"evt_{_eventSeq++}",
                Turn = TurnCount,
                Type = type,
                Message = message
            });

            if (_events.Count > MaxRecentEvents)
                _events.RemoveAt(0);
        }

        private static AuthoritativeMoveOutcome Outcome(string status, string message, bool accepted)
        {
            return new AuthoritativeMoveOutcome(status, message, accepted, new AuthoritativeGameStateDto());
        }

        private static AuthoritativeActionResponse Response(
            string status,
            string message,
            bool accepted,
            int turn,
            bool gameOver,
            string? gameOverReason,
            AuthoritativeGameStateDto state)
        {
            return new AuthoritativeActionResponse
            {
                Accepted = accepted,
                Status = status,
                Message = message,
                Turn = turn,
                GameOver = gameOver,
                GameOverReason = gameOverReason,
                State = state
            };
        }
    }
}
#endif