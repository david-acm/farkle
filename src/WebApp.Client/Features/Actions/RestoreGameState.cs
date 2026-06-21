using BlazorState;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  // Restores the full game view after a page refresh / reconnect.
  // Identity (playerId, name) comes from the browser session (the server snapshot is
  // identity-free); the rest comes from GET /api/games/{id}. If the game no longer
  // exists the service returns null and state is left untouched (HasJoined stays false),
  // which the page uses to fall back to the join form and clear the stale session.
  public static class RestoreGameState
  {
    public record Action(int PlayerId, string PlayerName) : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var snapshot = await service.GetGameStateAsync(State.GameId);
        if (snapshot is null) return; // game gone — leave state empty, page handles fallback

        State.GameId          = new GameId(snapshot.GameId);
        State.PlayerId        = new PlayerId(action.PlayerId);
        State.PlayerName      = new PlayerName(action.PlayerName);
        State.CurrentPlayerId = snapshot.CurrentPlayerId;
        State.GameStage       = snapshot.Stage;
        State.HostPlayerId    = snapshot.HostPlayerId;
        State.WinnerName      = snapshot.Winner?.Name;

        var standings = snapshot.Scoreboard
          .Select(p => new PlayerStanding(p.PlayerId, p.Name, p.Score, p.Color))
          .ToList();
        // Never wipe a known roster to empty: the read model is eventually consistent, so a
        // reconcile right after joining (#236) can read a view that exists but hasn't projected
        // the just-joined player yet. Keep what we have; the PlayerJoined broadcast fills it in.
        if (standings.Count > 0)
        {
          State.Scoreboard = standings;
          State.Roster     = standings;
        }

        // Dice and the in-progress turn score belong to the player whose turn it is.
        // In live play other players never see the active player's dice, so a restoring
        // non-active player gets an empty board — matching what they'd otherwise see.
        var myTurn = snapshot.CurrentPlayerId == action.PlayerId;
        State.TurnScore = new TurnScore(myTurn ? snapshot.TurnScore : 0);
        State.DiceInPlay = myTurn
          ? snapshot.TableCenter
              .Select((v, i) => DiceInfo.Unselected(i, DieValue.FromValue(v)))
              .ToList()
          : [];
      }
    }
  }
}
