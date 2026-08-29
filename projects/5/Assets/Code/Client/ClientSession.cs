using System;
using System.Collections.Generic;
using DunGen.Gameplay;

namespace DunGen.Client
{
    public enum ClientCommandType
    {
        Wait = 0,
        Move = 1,
        AttackNearest = 2
    }

    public struct ClientCommand
    {
        public ClientCommandType Type;
        public int DeltaX;
        public int DeltaY;

        public static ClientCommand Wait() => new() { Type = ClientCommandType.Wait };
        public static ClientCommand AttackNearest() => new() { Type = ClientCommandType.AttackNearest };
        public static ClientCommand Move(int deltaX, int deltaY) => new()
        {
            Type = ClientCommandType.Move,
            DeltaX = deltaX,
            DeltaY = deltaY
        };
    }

    public sealed class ClientCommandEnvelope
    {
        public const int CurrentContractVersion = 1;

        public int ContractVersion { get; set; } = CurrentContractVersion;
        public string CommandId { get; set; } = "";
        public int Sequence { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAtUtc { get; set; }
        public ClientCommand Command { get; set; }

        public static ClientCommandEnvelope Create(string commandId, int sequence, ClientCommand command)
        {
            return new ClientCommandEnvelope
            {
                ContractVersion = CurrentContractVersion,
                CommandId = commandId,
                Sequence = sequence,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(30),
                Command = command
            };
        }
    }

    public readonly struct GameSnapshot
    {
        public readonly int PlayerX;
        public readonly int PlayerY;
        public readonly int PlayerHealth;
        public readonly int PlayerMaxHealth;
        public readonly int PlayerLevel;
        public readonly int PlayerXP;
        public readonly int PlayerGold;
        public readonly int CurrentLevel;
        public readonly int TurnCount;
        public readonly int LivingEnemyCount;
        public readonly bool IsGameOver;
        public readonly string GameOverReason;

        public GameSnapshot(GameState state, int livingEnemyCount, bool isGameOver, string gameOverReason)
        {
            PlayerX = state.PlayerX;
            PlayerY = state.PlayerY;
            PlayerHealth = state.PlayerHealth;
            PlayerMaxHealth = state.PlayerMaxHealth;
            PlayerLevel = state.PlayerLevel;
            PlayerXP = state.PlayerXP;
            PlayerGold = state.PlayerGold;
            CurrentLevel = state.CurrentLevel;
            TurnCount = state.TurnCount;
            LivingEnemyCount = livingEnemyCount;
            IsGameOver = isGameOver;
            GameOverReason = gameOverReason ?? "";
        }

        public override string ToString()
        {
            return $"Level {CurrentLevel} | HP: {PlayerHealth}/{PlayerMaxHealth} | Lvl: {PlayerLevel} | XP: {PlayerXP} | Gold: {PlayerGold} | Enemies: {LivingEnemyCount} | Turn: {TurnCount}";
        }
    }

    public readonly struct ClientCommandResult
    {
        public readonly bool Accepted;
        public readonly bool Duplicate;
        public readonly string Message;
        public readonly GameSnapshot Snapshot;

        public ClientCommandResult(bool accepted, bool duplicate, string message, GameSnapshot snapshot)
        {
            Accepted = accepted;
            Duplicate = duplicate;
            Message = message ?? "";
            Snapshot = snapshot;
        }
    }

    public interface IClientSession
    {
        GameSnapshot GetSnapshot();
        ClientCommandResult Submit(ClientCommandEnvelope envelope);
    }

    public sealed class LocalGameSessionClient : IClientSession
    {
        readonly GameSession _gameSession;
        readonly HashSet<string> _processedCommandIds = new(StringComparer.Ordinal);
        int _lastAcceptedSequence;

        public LocalGameSessionClient(GameSession gameSession)
        {
            _gameSession = gameSession ?? throw new ArgumentNullException(nameof(gameSession));
        }

        public GameSnapshot GetSnapshot()
        {
            return new GameSnapshot(
                _gameSession.GetGameState(),
                _gameSession.GetLivingEnemyCountForClient(),
                _gameSession.IsGameOver,
                _gameSession.GameOverReason);
        }

        public ClientCommandResult Submit(ClientCommandEnvelope envelope)
        {
            if (!ValidateEnvelope(envelope, DateTime.UtcNow, out var validationError))
                return new ClientCommandResult(false, false, validationError, GetSnapshot());

            if (_processedCommandIds.Contains(envelope.CommandId))
                return new ClientCommandResult(true, true, "Duplicate command ignored.", GetSnapshot());

            if (envelope.Sequence <= _lastAcceptedSequence)
                return new ClientCommandResult(false, false, "Command sequence is stale.", GetSnapshot());

            if (envelope.Sequence != _lastAcceptedSequence + 1)
                return new ClientCommandResult(false, false, "Command sequence is not contiguous.", GetSnapshot());

            var playerCommand = ToPlayerCommand(envelope.Command);
            if (!_gameSession.TryExecutePlayerCommand(playerCommand, out var resultMessage))
                return new ClientCommandResult(false, false, resultMessage, GetSnapshot());

            _processedCommandIds.Add(envelope.CommandId);
            _lastAcceptedSequence = envelope.Sequence;
            return new ClientCommandResult(true, false, resultMessage, GetSnapshot());
        }

        static PlayerTurnCommand ToPlayerCommand(ClientCommand command)
        {
            return command.Type switch
            {
                ClientCommandType.Move => PlayerTurnCommand.Move(command.DeltaX, command.DeltaY),
                ClientCommandType.AttackNearest => PlayerTurnCommand.AttackNearest(),
                _ => PlayerTurnCommand.Wait()
            };
        }

        static bool ValidateEnvelope(ClientCommandEnvelope envelope, DateTime nowUtc, out string failureReason)
        {
            failureReason = "";

            if (envelope == null)
            {
                failureReason = "Command envelope is required.";
                return false;
            }

            if (envelope.ContractVersion != ClientCommandEnvelope.CurrentContractVersion)
            {
                failureReason = $"Unsupported command contract version '{envelope.ContractVersion}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(envelope.CommandId))
            {
                failureReason = "Command id is required.";
                return false;
            }

            if (envelope.Sequence <= 0)
            {
                failureReason = "Command sequence must be positive.";
                return false;
            }

            if (envelope.CreatedAtUtc == default)
            {
                failureReason = "Command creation time is required.";
                return false;
            }

            if (envelope.ExpiresAtUtc.HasValue && envelope.ExpiresAtUtc.Value <= nowUtc)
            {
                failureReason = "Command is stale.";
                return false;
            }

            if (envelope.Command.Type == ClientCommandType.Move &&
                Math.Abs(envelope.Command.DeltaX) + Math.Abs(envelope.Command.DeltaY) != 1)
            {
                failureReason = "Move command must target one cardinal tile.";
                return false;
            }

            return true;
        }
    }
}
