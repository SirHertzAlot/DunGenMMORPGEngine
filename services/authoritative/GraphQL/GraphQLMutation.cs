#if !UNITY_5_3_OR_NEWER
using System.Threading;
using System.Threading.Tasks;
using Authoritative.Multiplayer;
using HotChocolate;
using HotChocolate.Types;

namespace Authoritative.GraphQL
{
    public sealed class GraphQLMutation
    {
        public async Task<SubmitActionResult> SubmitAction(
            SubmitActionInput input,
            [Service] IAuthoritativeActionService service,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.SessionId))
                return SubmitActionResult.Rejected("sessionId is required.");

            var request = new AuthoritativeActionRequest
            {
                ActionId = input.ActionId,
                SessionId = input.SessionId,
                SourcePlayerId = input.SourcePlayerId,
                ActionType = input.ActionType,
                DeltaX = input.DeltaX,
                DeltaY = input.DeltaY,
                ExpectedTurn = input.ExpectedTurn
            };

            var response = await service.SubmitActionAsync(request, cancellationToken).ConfigureAwait(false);
            return new SubmitActionResult
            {
                Accepted = response.Accepted,
                Status = response.Status,
                Message = response.Message,
                Turn = response.Turn,
                GameOver = response.GameOver,
                State = response.State
            };
        }
    }

    [InputObjectType]
    public sealed class SubmitActionInput
    {
        public string ActionId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SourcePlayerId { get; set; } = string.Empty;
        public string ActionType { get; set; } = "move";
        public int DeltaX { get; set; }
        public int DeltaY { get; set; }
        public int ExpectedTurn { get; set; } = -1;
    }

    [ObjectType("SubmitActionResult")]
    public sealed class SubmitActionResult
    {
        public bool Accepted { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Turn { get; set; }
        public bool GameOver { get; set; }
        public AuthoritativeGameStateDto? State { get; set; }

        internal static SubmitActionResult Rejected(string message)
        {
            return new SubmitActionResult
            {
                Accepted = false,
                Status = AuthoritativeActionStatus.SessionUnavailable,
                Message = message,
                Turn = 0
            };
        }
    }
}
#endif
