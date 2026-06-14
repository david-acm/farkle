using BlazorState;
using WebApp.Client.Pages.Game.Components;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Features;

public partial class GameState
{
    // Applies a live table snapshot (#158) pushed over SignalR when the in-turn player rolls
    // or keeps dice, so off-turn players watch the shared table update in real time.
    public static class RemoteTableChanged
    {
        public record Action(GameStateResponse Payload) : IAction;

        public class Handler(IStore store) : ActionHandler<Action>(store)
        {
            private GameState State => Store.GetState<GameState>();

            public override Task Handle(Action action, CancellationToken aCancellationToken)
            {
                // Never clobber the active player's local drag state — they are the source of
                // truth for their own turn and already see their own rolls/keeps locally.
                if (State.IsMyTurn) return Task.CompletedTask;

                var p = action.Payload;

                // Rebuild the read-only table from the snapshot: centre dice in "Rolled",
                // kept dice in "SetAside". Animate=false — a spectator sees a settled snapshot,
                // not a re-roll spin.
                var dice = p.TableCenter
                    .Select((v, i) => new DraggableDie(i, DieValue.FromValue(v), "Rolled") { Animate = false })
                    .Concat(p.DiceKept
                        .Select((v, i) => new DraggableDie(p.TableCenter.Count + i, DieValue.FromValue(v), "SetAside") { Animate = false }))
                    .ToList();

                State.DiceInPlay = dice;
                State.TurnScore       = new(p.TurnScore);
                State.CurrentPlayerId = p.CurrentPlayerId;
                State.Scoreboard = (p.Scoreboard ?? [])
                    .Select(s => new PlayerStanding(s.PlayerId, s.Name, s.Score))
                    .ToList();

                return Task.CompletedTask;
            }
        }
    }
}
