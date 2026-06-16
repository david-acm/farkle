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
        // Animate is true only for a roll snapshot (#167) so spectators see the dice spin;
        // keeps / set-aside / put-back update the table without re-spinning.
        public record Action(GameStateResponse Payload, bool Animate = false) : IAction;

        public class Handler(IStore store) : ActionHandler<Action>(store)
        {
            private GameState State => Store.GetState<GameState>();

            public override Task Handle(Action action, CancellationToken aCancellationToken)
            {
                // Never clobber the active player's local selection state — they are the source of
                // truth for their own turn and already see their own rolls/keeps locally.
                if (State.IsMyTurn) return Task.CompletedTask;

                var p = action.Payload;

                // TableCenter is the full roll in a stable order and is NOT changed by a
                // set-aside (it's a non-destructive overlay, #159). Render it in place, with a
                // stable index = position, marking each die selected ("SetAside") by consuming
                // the set-aside multiset. A selection then only flips a die's highlight — every
                // die keeps its grid cell and component identity (stable @key), so the
                // spectator's board never reorders, relocates or re-spins on a select (#186).
                // Only a fresh roll animates (#167); selected dice never do.
                var setAside = new List<int>(p.DiceSetAside);
                var dice = p.TableCenter
                    .Select((v, i) =>
                    {
                        var isSet = setAside.Remove(v);
                        return new TrayDie(i, DieValue.FromValue(v), isSet ? "SetAside" : "Rolled")
                        {
                            Animate = action.Animate && !isSet
                        };
                    })
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
