using BlazorState;
using Farkle.Ui.Telemetry;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Ui.Features;

public partial class GameState
{
    public static class RemoteTurnChanged
    {
        public record Action(PassTurnResponse Payload, string? CausedByOperationId = null)
            : IAction, ICausedByBroadcast;

        public class Handler(IStore store) : ActionHandler<Action>(store)
        {
            private GameState State => Store.GetState<GameState>();

            public override Task Handle(Action action, CancellationToken aCancellationToken)
            {
                var p = action.Payload;
                State.CurrentPlayerId = p.CurrentPlayerId;
                State.TurnNumber = p.TurnNumber; // #244
                State.Scoreboard = (p.Scoreboard ?? [])
                    .Select(s => new PlayerStanding(s.PlayerId, s.Name, s.Score, s.Color))
                    .ToList();
                State.WinnerName = p.Winner?.Name;
                State.DiceInPlay.Clear();
                State.TurnScore = new(0);
                // #286 — the turn moved on; reset the local stage to a fresh awaiting-roll turn.
                State.PlayStage        = Farkle.SharedKernel.Turns.GameStage.Rolling;
                State.HasActedThisTurn = false;
                return Task.CompletedTask;
            }
        }
    }
}
