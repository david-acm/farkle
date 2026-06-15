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
    // kept for callers (and tests) that already encode the identifier on Die.
    public record Action(DraggableDie Die, string Identifier) : IAction
    {
      public Action(DraggableDie die) : this(die, die.Identifier) { }
    }

    public class Handler(IStore store, IGameService service, ILogger<Handler> logger)
      : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // DiceInPlay is the single source of truth; the set-aside payload is
        // derived from it. Sync the dropped die's zone identifier.
        var die = State.DiceInPlay.First(d => d.Index == action.Die.Index);
        die.Identifier = action.Identifier;

        // Setting a die aside means the roll is over — clear Animate on every die so
        // none re-spins when the drop container recreates their components for the
        // zone change (covers a move made before the roll animation's own consume
        // fires). A move is never a roll (#139).
        foreach (DraggableDie d in State.DiceInPlay)
          d.Animate = false;

        // Promote the (formerly UI-only) selection to a domain command so it's persisted
        // and broadcast to spectators (#159). The local move already happened above for
        // instant feedback; the server broadcast won't echo back to us (RemoteTableChanged
        // is guarded by IsMyTurn). Best-effort: a failed sync must not break the local drag.
        try
        {
          if (action.Identifier == "SetAside")
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
