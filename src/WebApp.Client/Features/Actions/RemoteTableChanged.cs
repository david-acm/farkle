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

                // TableCenter is the full roll in a stable order. A set-aside is a non-destructive
                // overlay (#159) so the centre is unchanged; a keep, however, REMOVES the kept
                // dice, shrinking the centre. We mark each die selected ("SetAside") by consuming
                // the set-aside multiset, and assign each die a `@key` (DiceInfo.Index) that stays
                // stable across these table changes so the spectator's board never reorders,
                // relocates or re-spins on a select/keep (#186, #204):
                //   • A fresh roll (Animate) re-rolls every die, so use the table position as the
                //     index — the dice SHOULD re-mount and spin (#167).
                //   • Otherwise (select / keep / put-back) inherit each surviving die's PRIOR
                //     index by matching the previous board in value order. On a keep this keeps
                //     survivors after the kept dice on their original key, so their Die component
                //     is reused (not re-mounted) and never replays the mount spin (#204) —
                //     mirroring how the in-turn player's KeepDice retains its DiceInfo objects.
                var setAside  = new List<int>(p.DiceSetAside);
                // Prior board, consumed as we match survivors so each value is reused at most once.
                var available = new List<DiceInfo>(State.DiceInPlay);
                var nextIndex = available.Count == 0 ? 0 : available.Max(d => d.Index) + 1;

                var dice = new List<DiceInfo>(p.TableCenter.Count);
                for (var position = 0; position < p.TableCenter.Count; position++)
                {
                    var v     = p.TableCenter[position];
                    var isSet = setAside.Remove(v);

                    int index;
                    if (action.Animate)
                    {
                        index = position;
                    }
                    else
                    {
                        // Same value → same physical die for the spectator; reusing any prior
                        // instance of that value is visually identical, so duplicate values are safe.
                        var match = available.FirstOrDefault(d => (int)d.Value == v);
                        if (match is not null)
                        {
                            available.Remove(match);
                            index = match.Index;
                        }
                        else
                        {
                            index = nextIndex++;
                        }
                    }

                    var die = isSet
                        ? DiceInfo.Selected(index, DieValue.FromValue(v))
                        : DiceInfo.Unselected(index, DieValue.FromValue(v));
                    // Only a fresh roll animates; selected dice never do (#167).
                    if (isSet || !action.Animate) die.DisableAnimation();
                    dice.Add(die);
                }

                State.DiceInPlay = dice;
                State.TurnScore       = new(p.TurnScore);
                State.CurrentPlayerId = p.CurrentPlayerId;
                State.Scoreboard = (p.Scoreboard ?? [])
                    .Select(s => new PlayerStanding(s.PlayerId, s.Name, s.Score, s.Color))
                    .ToList();

                return Task.CompletedTask;
            }
        }
    }
}
