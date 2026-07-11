using Farkle.Features;

namespace Farkle.Application;

// #303 — Wolverine message handler that turns a post-commit broadcast notification (cascaded by an
// endpoint) into the SignalR push, delegating to GameNotifier (which loads the up-to-date Marten
// snapshot and calls the broadcaster). Lives in the application layer, so it may depend on the
// broadcaster port; Wolverine discovers it by convention (Handle) in the Farkle assembly.
public static class GameBroadcastHandler
{
  public static Task Handle(GameNotifications.LobbyChanged msg, GameNotifier notifier, CancellationToken ct)
    => notifier.LobbyChangedAsync(msg.GameId, ct);

  public static Task Handle(GameNotifications.GameBegan msg, GameNotifier notifier, CancellationToken ct)
    => notifier.GameBeganAsync(msg.GameId, ct);

  public static Task Handle(GameNotifications.DiceRolled msg, GameNotifier notifier, CancellationToken ct)
    => notifier.DiceRolledAsync(msg.GameId, ct);

  public static Task Handle(GameNotifications.TableChanged msg, GameNotifier notifier, CancellationToken ct)
    => notifier.TableChangedAsync(msg.GameId, ct);

  public static Task Handle(GameNotifications.TurnChanged msg, GameNotifier notifier, CancellationToken ct)
    => notifier.TurnChangedAsync(msg.GameId, msg.PlayerId, ct);
}
