using BlazorState;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HotDice.Ui.Features;
using HotDice.Ui.Services;
using HotDice.Ui.Telemetry;
using Xunit;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.SpaTests.Handlers;

/// <summary>
/// #225 — the pipeline behavior emits one UI intent custom event per dispatched action,
/// automatically (no per-handler wiring), tracking user-initiated and remote actions alike,
/// and skipping actions opted out via <see cref="IInternalAction"/>.
///
/// The two "real action" tests dispatch through the full BlazorState/MediatR pipeline (proving
/// the behavior is wired and names + state are captured). Exclusion and failure semantics are
/// unit-tested by invoking the behavior directly, so they don't need pipeline-registered handlers
/// or BlazorState's state-nesting convention for the test-only fakes.
/// </summary>
public class UiTelemetryBehaviorShould : IDisposable
{
  private readonly IServiceScope _scope;
  private readonly Mock<IUiTelemetry> _telemetry = new();
  private readonly List<(string Name, IReadOnlyDictionary<string, string?> Props)> _tracked = [];

  public UiTelemetryBehaviorShould()
  {
    _telemetry
      .Setup(t => t.TrackIntentAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
      .Callback<string, IReadOnlyDictionary<string, string?>>((n, p) => _tracked.Add((n, p)))
      .Returns(Task.CompletedTask);

    var services = new ServiceCollection();
    services.AddLogging(b => b.AddDebug());
    services.AddScoped(_ => _telemetry.Object);
    services.AddBlazorState(o => o.Assemblies = [typeof(GameState).Assembly]);
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UiTelemetryBehavior<,>));

    _scope = services.BuildServiceProvider().CreateScope();
  }

  private ISender Sender => _scope.ServiceProvider.GetRequiredService<ISender>();
  private IStore  Store  => _scope.ServiceProvider.GetRequiredService<IStore>();

  [Fact]
  public async Task EmitAUiEventNamedAfterTheAction_WithPostActionState()
  {
    await Sender.Send(new GameState.SetGameId.Action(new GameId(42)));

    var entry = _tracked.Should().ContainSingle().Subject;
    entry.Name.Should().Be("UI.SetGameId");
    // Post-action state: the id the action just set is captured for correlation.
    entry.Props["GameId"].Should().Be("42");
    entry.Props["Failed"].Should().Be("false");
  }

  [Fact]
  public async Task TrackRemoteActionsToo()
  {
    var payload = new PassTurnResponse(GameId: 7, PlayerId: 1, NewScore: 0, Winner: null, CurrentPlayerId: 2);

    await Sender.Send(new GameState.RemoteTurnChanged.Action(payload));

    _tracked.Should().ContainSingle(e => e.Name == "UI.RemoteTurnChanged");
  }

  [Fact]
  public async Task StampTheTurnEntityKey()
  {
    // #244 — a turn-boundary action updates State.TurnNumber, which the behavior stamps as turnId.
    var payload = new PassTurnResponse(GameId: 7, PlayerId: 1, NewScore: 0, Winner: null,
      CurrentPlayerId: 2, Scoreboard: null, TurnNumber: 5);

    await Sender.Send(new GameState.RemoteTurnChanged.Action(payload));

    var entry = _tracked.Should().ContainSingle().Subject;
    entry.Props["turnId"].Should().Be("5");
  }

  [Fact]
  public async Task LinkBroadcastActionsToTheOriginatingCommandTrace()
  {
    // #221 — a broadcast-triggered action carries the originating command's trace id; the UI event
    // links back to it via causedByOperationId (without overwriting this client's own operation id).
    var payload = new PassTurnResponse(GameId: 7, PlayerId: 1, NewScore: 0, Winner: null, CurrentPlayerId: 2);

    await Sender.Send(new GameState.RemoteTurnChanged.Action(payload, CausedByOperationId: "abc123trace"));

    var entry = _tracked.Should().ContainSingle(e => e.Name == "UI.RemoteTurnChanged").Subject;
    entry.Props["causedByOperationId"].Should().Be("abc123trace");
  }

  [Fact]
  public async Task OmitTheCorrelationProp_WhenNoOriginatingTrace()
  {
    // Tracing off server-side → null trace id → the property is simply absent (not empty).
    await Sender.Send(new GameState.SetGameId.Action(new GameId(42)));

    var entry = _tracked.Should().ContainSingle().Subject;
    entry.Props.Should().NotContainKey("causedByOperationId");
  }

  [Fact]
  public async Task NotTrackActionsMarkedInternal()
  {
    var behavior = new UiTelemetryBehavior<InternalNoiseAction, Unit>(_telemetry.Object, Store);

    await behavior.Handle(new InternalNoiseAction(), () => Task.FromResult(Unit.Value), default);

    _tracked.Should().BeEmpty();
  }

  [Fact]
  public async Task RecordAFailedHandlerAndRethrow()
  {
    var behavior = new UiTelemetryBehavior<ThrowingAction, Unit>(_telemetry.Object, Store);

    var act = async () => await behavior.Handle(
      new ThrowingAction(),
      () => throw new InvalidOperationException("boom"),
      default);

    await act.Should().ThrowAsync<InvalidOperationException>();
    var entry = _tracked.Should().ContainSingle().Subject;
    entry.Name.Should().Be("UI.ThrowingAction");
    entry.Props["Failed"].Should().Be("true");
  }

  [Fact]
  public async Task StartANewOperation_ForServerBoundCommands()
  {
    // #244 — a server-bound command resets the App Insights operation before dispatch, so each
    // command becomes its own transaction.
    var behavior = new UiTelemetryBehavior<ServerCommandAction, Unit>(_telemetry.Object, Store);

    await behavior.Handle(new ServerCommandAction(), () => Task.FromResult(Unit.Value), default);

    _telemetry.Verify(t => t.StartOperationAsync("ServerCommandAction"), Times.Once);
  }

  [Fact]
  public async Task NotStartANewOperation_ForNonCommandActions()
  {
    await Sender.Send(new GameState.SetGameId.Action(new GameId(1)));

    _telemetry.Verify(t => t.StartOperationAsync(It.IsAny<string>()), Times.Never);
  }

  public void Dispose() => _scope.Dispose();

  // Test-only fakes for the direct-invocation tests above. They are never dispatched through the
  // pipeline, so BlazorState's "actions must be nested in their State" analyzer (TW0001) doesn't
  // apply — suppress it for these.
#pragma warning disable TW0001
  public record InternalNoiseAction : IAction, IInternalAction;

  public record ThrowingAction : IAction;

  public record ServerCommandAction : IAction, IServerCommandIntent;
#pragma warning restore TW0001
}
