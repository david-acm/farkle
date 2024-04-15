using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class Score : ComponentBase
{
  [Parameter]
  public IDictionary<int, int> GameScore { get; set; } = null!;
  
  public int ForPlayer(int playerId)
  {
    GameScore.TryGetValue(playerId, out var score);
    return score;
  }
}

