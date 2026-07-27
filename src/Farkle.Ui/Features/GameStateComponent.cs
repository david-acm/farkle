using BlazorState;

namespace Farkle.Ui.Features;

// Shared base for components that read the Farkle GameState. Centralises the
// `GameState => GetState<GameState>()` accessor that every game component otherwise
// repeats, so the typed-state lookup lives in one place.
public abstract class GameStateComponent : BlazorStateComponent
{
  protected GameState GameState => GetState<GameState>();
}
