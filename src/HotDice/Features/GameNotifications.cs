namespace HotDice.Features;

// #303 — post-commit broadcast notifications. A Wolverine.HTTP endpoint returns one of these as a
// cascading message alongside its events; Wolverine's Marten outbox publishes it only after the event
// append commits, and GameBroadcastHandler (application layer) turns it into the SignalR push. This
// replaces the old inline "call GameNotifier after InvokeAsync" step and moves the broadcast onto the
// outbox — delivering the broadcast half of #305 as a side effect of the #303 endpoint collapse.
//
// They live in HotDice.Features (not Application) so the slice endpoints can return them without the
// slices depending on the application layer (the vertical-slice guardrail).
public static class GameNotifications
{
  public record LobbyChanged(int GameId);
  public record GameBegan(int GameId);
  public record DiceRolled(int GameId);
  public record TableChanged(int GameId);
  public record TurnChanged(int GameId, int PlayerId);
}
