using BlazorState;
using Farkle.SharedKernel.Scoring;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class TurnScore : BlazorStateComponent
{
  private GameState GameState => GetState<GameState>();

  public int Value => GameState.TurnScore.Value;

  // Live preview of the current selection (#182) — shown only while dice are selected.
  private bool HasSelection => GameState.DiceSetAside.Count > 0;

  private ScoreBreakdown Preview => GameState.SelectionPreview;

  // True when the selection forms a scoring trick worth keeping.
  private bool PreviewScores => Preview.CanKeep && Preview.Points > 0;

  private string PreviewLabel => Preview.Trick switch
  {
    ScoringTrick.FourOfAKind  => "Four of a kind",
    ScoringTrick.ThreeOfAKind => "Three of a kind",
    ScoringTrick.OnesAndFives => "Ones & fives",
    ScoringTrick.Run          => "Run 1-6",
    _                         => "No score"
  };
}
