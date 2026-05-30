using Eventuous;
using Eventuous.Subscriptions.Context;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Farkle.Endpoints;
using EvtHandler = Eventuous.Subscriptions.EventHandler;

namespace Farkle.Subscriptions;

internal sealed class GameBroadcastHandler : EvtHandler
{
  private readonly IGameEventBroadcaster _broadcaster;
  private readonly IAggregateStore       _store;

  public GameBroadcastHandler(IGameEventBroadcaster broadcaster, IAggregateStore store)
  {
    _broadcaster = broadcaster;
    _store       = store;

    On<GameEvents.V1.PlayerJoined>(async ctx =>
    {
      var state = await LoadState(ctx);
      await _broadcaster.BroadcastPlayerJoinedAsync(LobbyMapper.ToLobbyState(state), ctx.CancellationToken);
    });

    On<GameEvents.V1.GamePlayStarted>(async ctx =>
    {
      var state = await LoadState(ctx);
      await _broadcaster.BroadcastGameBeganAsync(LobbyMapper.ToLobbyState(state), ctx.CancellationToken);
    });

    On<GameEvents.V1.TurnPassed>(async ctx =>
    {
      var state = await LoadState(ctx);
      await _broadcaster.BroadcastTurnChangedAsync(
        PassTurnMapper.ToPassTurn(state, ctx.Message.PlayerId), ctx.CancellationToken);
    });
  }

  private async Task<GameState> LoadState(IMessageConsumeContext ctx)
  {
    var game = await _store.Load<Game>(ctx.Stream, ctx.CancellationToken);
    return game.State;
  }
}
