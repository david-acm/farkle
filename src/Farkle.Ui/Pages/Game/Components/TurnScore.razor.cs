using Farkle.Ui.Features;

namespace Farkle.Ui.Pages.Game.Components;

public partial class TurnScore : GameStateComponent
{
  // The score banked so far this turn. The live preview of the CURRENT selection moved to its
  // own component under the dice (#274) — see SelectionScore.
  public int Value => GameState.TurnScore.Value;
}
