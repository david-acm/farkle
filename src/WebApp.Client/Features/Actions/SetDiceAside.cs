using BlazorState;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class SetDiceAside
  {
    // Carries the target zone explicitly so the UI never mutates the dropped
    // die outside an action handler. Backwards-compatible single-arg form is
    // kept for callers (and tests) that already encode the zone on the die.
    public record Action(TrayDie Die, DiceZone Zone) : IAction
    {
      public Action(TrayDie die) : this(die, die.Zone) { }
    }

    public class Handler(IStore store, IGameService service, ILogger<Handler> logger)
      : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // DiceInPlay is the single source of truth; the set-aside payload is
        // derived from it. Sync the dropped die's zone.
        var die = State.DiceInPlay.First(d => d.Index == action.Die.Index);
        if (action.Zone == DiceZone.SetAside) die.SetAside();
        else die.ReturnToRolled();

        // Setting a die aside means the roll is over — clear the animation on every die so
        // none re-spins when the drop container recreates their components for the
        // zone change (covers a move made before the roll animation's own consume
        // fires). A move is never a roll (#139).
        foreach (TrayDie d in State.DiceInPlay)
          d.DisableAnimation();

        // Promote the (formerly UI-only) selection to a domain command so it's persisted
        // and broadcast to spectators (#159). The local move already happened above for
        // instant feedback; the server broadcast won't echo back to us (RemoteTableChanged
        // is guarded by IsMyTurn). Best-effort: a failed sync must not break the local selection.
        try
        {
          if (action.Zone == DiceZone.SetAside)
            await service.SetDiceAsideAsync(State.GameId, State.PlayerId, (int)die.Value);
          else
            await service.ReturnDiceAsync(State.GameId, State.PlayerId, (int)die.Value);
        }
        catch (Exception ex)
        {
          logger.LogWarning(ex, "Set-aside sync failed for game {GameId}", State.GameId.Value);
        }
      }
    }
  }
}
