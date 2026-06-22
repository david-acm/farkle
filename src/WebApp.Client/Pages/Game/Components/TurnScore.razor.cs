using BlazorState;
using Farkle.SharedKernel.Scoring;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class TurnScore : GameStateComponent
{
  public int Value => GameState.TurnScore.Value;

  // Live preview of the current selection (#182) — shown only while dice are selected.
  private bool HasSelection => GameState.DiceSetAside.Count > 0;

  private ScoreBreakdown Preview => GameState.SelectionPreview;

  // True when the selection forms a scoring trick worth keeping.
  private bool PreviewScores => Preview.CanKeep && Preview.Points > 0;

  // Lists every component trick in the selection (e.g. "Three of a kind + Ones & fives", #270).
  private string PreviewLabel => Preview.ToDisplayName();
}
